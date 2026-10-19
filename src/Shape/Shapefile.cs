using System.Buffers.Binary;
using System.Collections;
using System.Collections.Immutable;
using System.Text;
using DBase;
using DotNext.Buffers;
using Shape.Geometries;
using Shape.Serialization;

namespace Shape;

/// <summary>Owns a shapefile's geometry, index, and attribute files.</summary>
/// <remarks>
/// Record types are selected per operation. Access uses shared stream positions and is not thread safe.
/// Keep this instance open while consuming its lazy record sequences.
/// </remarks>
public sealed class Shapefile : IDisposable, IEnumerable<ShapeRecord<Geometry, DbfRecord>>
{
    private const int HeaderLength = 100;
    private readonly Stream _shp;
    private readonly ShapeIndex _shx;
    private readonly Dbf _dbf;
    private bool _dirty;
    private bool _disposed;
    private bool? _hasBounds;
    private string? _temporaryDirectory;

    private Shapefile(Stream shp, ShapeIndex shx, Dbf dbf, ShapeType shapeType, BoundingBox boundingBox)
    {
        _shp = shp;
        _shx = shx;
        _dbf = dbf;
        ShapeType = shapeType;
        BoundingBox = boundingBox;
        _hasBounds = shx.RecordCount == 0 ? false : null;

        if (shx.RecordCount != dbf.RecordCount)
            throw new InvalidDataException("The SHX and DBF record counts do not match.");
    }

    // JSON has no destination path. Own in-memory geometry/index streams and a temporary
    // DBF table, since DBase currently exposes table creation through paths only.
    internal static Shapefile CreateTemporary(ShapeType shapeType, ImmutableArray<DbfFieldDescriptor> descriptors)
    {
        var directory = Path.Combine(Path.GetTempPath(), "Shape.GeoJson-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var shp = new MemoryStream();
        var shxStream = new MemoryStream();
        ShapeIndex? shx = null;
        Dbf? dbf = null;
        try
        {
            shp.SetLength(HeaderLength);
            shxStream.SetLength(HeaderLength);
            WriteHeader(shp, shapeType, BoundingBox.Empty);
            WriteHeader(shxStream, shapeType, BoundingBox.Empty);
            shx = ShapeIndex.Open(shxStream);
            dbf = Dbf.Create(Path.Combine(directory, "attributes.dbf"), descriptors, new DbfCreateOptions
            {
                Encoding = Encoding.UTF8,
                Version = descriptors.Any(descriptor => descriptor.Type == DbfFieldType.Memo)
                    ? DbfVersion.DBase8B : DbfVersion.DBase03
            });
            return new Shapefile(shp, shx, dbf, shapeType, BoundingBox.Empty) { _temporaryDirectory = directory };
        }
        catch
        {
            try { dbf?.Dispose(); }
            finally
            {
                try { shx?.Dispose(); }
                finally
                {
                    shp.Dispose();
                    shxStream.Dispose();
                    DeleteTemporaryDirectory(directory);
                }
            }
            throw;
        }
    }

    private static void DeleteTemporaryDirectory(string directory)
    {
        // Only files in this owner's newly-created directory are removed; no recursion.
        foreach (var file in Directory.EnumerateFiles(directory)) File.Delete(file);
        Directory.Delete(directory);
    }

    /// <summary>Opens an existing shapefile and its sibling SHX and DBF files for reading and writing.</summary>
    /// <param name="fileName">The SHP file path.</param>
    /// <returns>The owner of the opened files.</returns>
    public static Shapefile Open(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        FileStream? shp = null;
        ShapeIndex? shx = null;
        Dbf? dbf = null;
        try
        {
            shp = new FileStream(fileName, FileMode.Open, FileAccess.ReadWrite);
            var (shapeType, boundingBox) = ReadHeader(shp);
            shx = ShapeIndex.Open(Path.ChangeExtension(fileName, ".shx"));
            dbf = Dbf.Open(Path.ChangeExtension(fileName, ".dbf"));
            return new Shapefile(shp, shx, dbf, shapeType, boundingBox);
        }
        catch
        {
            try { dbf?.Dispose(); }
            finally
            {
                try { shx?.Dispose(); }
                finally { shp?.Dispose(); }
            }
            throw;
        }
    }

    /// <summary>Gets the shape type declared by the file header.</summary>
    public ShapeType ShapeType { get; }

    /// <summary>Gets the extent of the geometries in the file.</summary>
    public BoundingBox BoundingBox { get; private set; }

    /// <summary>Gets the number of physical geometry and attribute record pairs.</summary>
    public int RecordCount
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _shx.RecordCount;
        }
    }

    /// <summary>Reads a record as a base geometry and a DBF record.</summary>
    /// <param name="index">The zero-based physical record index, including deleted DBF rows.</param>
    /// <returns>The geometry and attributes at the requested index.</returns>
    public ShapeRecord<Geometry, DbfRecord> GetRecord(int index) => GetRecord<Geometry, DbfRecord>(index);

    /// <summary>Reads a record with a typed geometry and a DBF record.</summary>
    /// <typeparam name="TGeometry">A geometry family compatible with the file, or <see cref="Geometry"/>.</typeparam>
    /// <param name="index">The zero-based physical record index.</param>
    /// <returns>The geometry and attributes at the requested index.</returns>
    public ShapeRecord<TGeometry, DbfRecord> GetRecord<TGeometry>(int index)
        where TGeometry : Geometry => GetRecord<TGeometry, DbfRecord>(index);

    /// <summary>Reads a record using the requested geometry and attribute representations.</summary>
    /// <typeparam name="TGeometry">A geometry family compatible with the file, or <see cref="Geometry"/>.</typeparam>
    /// <typeparam name="TAttributes">The CLR type mapped to the DBF schema, or <see cref="DbfRecord"/>.</typeparam>
    /// <param name="index">The zero-based physical record index.</param>
    /// <returns>The geometry and attributes at the requested index.</returns>
    /// <remarks>Typed attributes do not expose DBF deletion status.</remarks>
    public ShapeRecord<TGeometry, TAttributes> GetRecord<TGeometry, TAttributes>(int index)
        where TGeometry : Geometry
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ShapeType.EnsureCompatibleWith<TGeometry>();
        return ReadRecord<TGeometry, TAttributes>(index, GeometryCodec<TGeometry>.Instance);
    }

    private ShapeRecord<TGeometry, TAttributes> ReadRecord<TGeometry, TAttributes>(
        int index, GeometryCodec<TGeometry> codec) where TGeometry : Geometry
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var entry = _shx.GetRecord(index);
        if (entry.Offset < HeaderLength || entry.Length < 4 ||
            (long)entry.Offset + 8 + entry.Length > _shp.Length)
            throw new InvalidDataException("The SHX entry points outside a complete SHP record.");

        _shp.Position = entry.Offset;
        Span<byte> header = stackalloc byte[8];
        _shp.ReadExactly(header);
        if (BinaryPrimitives.ReadInt32BigEndian(header) != index + 1 ||
            BinaryPrimitives.ReadInt32BigEndian(header[4..]) != entry.Length / 2)
            throw new InvalidDataException("The SHP record header does not match its SHX entry.");

        using var buffer = entry.Length < 256
            ? new SpanOwner<byte>(stackalloc byte[entry.Length])
            : new SpanOwner<byte>(entry.Length);
        _shp.ReadExactly(buffer.Span);

        var recordShapeType = (ShapeType)BinaryPrimitives.ReadInt32LittleEndian(buffer.Span);
        if (recordShapeType != ShapeType.Null && recordShapeType != ShapeType)
            throw new InvalidDataException("The record's shape type does not match the file header.");
        if (recordShapeType == ShapeType.Null && entry.Length != 4)
            throw new InvalidDataException("A null shape record must contain only its shape type.");

        return new(codec.Deserialize(buffer.Span, ShapeType), _dbf.GetRecord<TAttributes>(index));
    }

    /// <summary>Lazily enumerates base geometries and DBF records in physical order.</summary>
    /// <returns>A sequence containing each physical record pair, including deleted DBF rows.</returns>
    public IEnumerable<ShapeRecord<Geometry, DbfRecord>> EnumerateRecords() => EnumerateRecords<Geometry, DbfRecord>();

    /// <summary>Lazily enumerates typed geometries and DBF records in physical order.</summary>
    /// <typeparam name="TGeometry">A geometry family compatible with the file, or <see cref="Geometry"/>.</typeparam>
    /// <returns>A sequence containing each physical record pair.</returns>
    public IEnumerable<ShapeRecord<TGeometry, DbfRecord>> EnumerateRecords<TGeometry>()
        where TGeometry : Geometry => EnumerateRecords<TGeometry, DbfRecord>();

    /// <summary>Lazily enumerates records using the requested geometry and attribute representations.</summary>
    /// <typeparam name="TGeometry">A geometry family compatible with the file, or <see cref="Geometry"/>.</typeparam>
    /// <typeparam name="TAttributes">The CLR type mapped to the DBF schema, or <see cref="DbfRecord"/>.</typeparam>
    /// <returns>A sequence containing each physical record pair.</returns>
    /// <remarks>
    /// Compatibility is checked and the geometry reader is resolved when enumeration starts.
    /// The record count is captured at that point, so subsequent appends are excluded.
    /// Keep the file open while enumerating. Typed attributes do not expose DBF deletion status.
    /// </remarks>
    public IEnumerable<ShapeRecord<TGeometry, TAttributes>> EnumerateRecords<TGeometry, TAttributes>()
        where TGeometry : Geometry
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ShapeType.EnsureCompatibleWith<TGeometry>();
        var codec = GeometryCodec<TGeometry>.Instance;
        var count = RecordCount;
        for (var index = 0; index < count; index++)
            yield return ReadRecord<TGeometry, TAttributes>(index, codec);
    }

    /// <summary>Enumerates records as base geometries and DBF records.</summary>
    public IEnumerator<ShapeRecord<Geometry, DbfRecord>> GetEnumerator() => EnumerateRecords().GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>Appends a geometry and its associated attribute record.</summary>
    /// <typeparam name="TGeometry">The geometry representation.</typeparam>
    /// <typeparam name="TAttributes">The attribute representation.</typeparam>
    /// <param name="record">The paired record to append.</param>
    public void Add<TGeometry, TAttributes>(ShapeRecord<TGeometry, TAttributes> record)
        where TGeometry : Geometry => Add(record.Geometry, record.Attributes);

    /// <summary>Appends a geometry and its associated attribute record.</summary>
    /// <typeparam name="TGeometry">The geometry representation.</typeparam>
    /// <typeparam name="TAttributes">The attribute representation.</typeparam>
    /// <param name="geometry">A geometry matching the file's shape type, or a null shape.</param>
    /// <param name="attributes">Attributes matching the DBF schema.</param>
    /// <remarks>Attribute serialization errors occur before the SHP and SHX files are changed.</remarks>
    public void Add<TGeometry, TAttributes>(TGeometry geometry, TAttributes attributes)
        where TGeometry : Geometry
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(geometry);
        ShapeType.EnsureCompatibleWith<TGeometry>();
        if (geometry.ShapeType != ShapeType.Null && geometry.ShapeType != ShapeType)
            throw new ArgumentException("The geometry's shape type must match the file header.", nameof(geometry));
        if (geometry.ShapeType != ShapeType.Null)
            EnsureGeometryFamily(geometry);
        if (_dbf.RecordCount != RecordCount)
            throw new InvalidDataException("The SHX and DBF record counts do not match.");

        var codec = GeometryCodec<TGeometry>.Instance;
        var length = codec.GetByteSize(geometry);
        var offset = checked((int)_shp.Length);
        _ = checked(offset + 8 + length);
        using var buffer = length < 256
            ? new SpanOwner<byte>(stackalloc byte[length])
            : new SpanOwner<byte>(length);
        buffer.Span.Clear();
        codec.Serialize(buffer.Span, geometry);

        var bounds = BoundingBox;
        if (geometry.ShapeType != ShapeType.Null)
        {
            var addedBounds = geometry.GetBoundingBox();
            bounds = HasGeometryBounds() ? Union(BoundingBox, addedBounds) : addedBounds;
        }

        var recordNumber = checked(RecordCount + 1);
        Span<byte> header = stackalloc byte[8];
        BinaryPrimitives.WriteInt32BigEndian(header, recordNumber);
        BinaryPrimitives.WriteInt32BigEndian(header[4..], length / 2);

        // Dbf validates and serializes before it writes. Do this before modifying the
        // geometry files so an incompatible attribute mapping cannot leave an orphan shape.
        _dbf.Add(attributes);
        _dirty = true;
        _shp.Position = offset;
        _shp.Write(header);
        _shp.Write(buffer.Span);
        _shx.Add(new ShapeIndexRecord(offset, length));
        BoundingBox = bounds;
        if (geometry.ShapeType != ShapeType.Null) _hasBounds = true;
    }

    private void EnsureGeometryFamily(Geometry geometry)
    {
        // A base-typed value must still agree with the file's geometry family.
        // Geometry records expose ShapeType as an init property, so its value alone is insufficient.
        switch (geometry)
        {
            case Point: ShapeType.EnsureCompatibleWith<Point>(); break;
            case PolyLine: ShapeType.EnsureCompatibleWith<PolyLine>(); break;
            case Polygon: ShapeType.EnsureCompatibleWith<Polygon>(); break;
            case MultiPoint: ShapeType.EnsureCompatibleWith<MultiPoint>(); break;
            case MultiPatch: ShapeType.EnsureCompatibleWith<MultiPatch>(); break;
        }
    }

    private bool HasGeometryBounds()
    {
        if (_hasBounds is { } value) return value;

        // Bounds in files containing only null shapes need not describe actual coordinates.
        // Check lazily before the first non-null append, stopping at the first non-null record.
        Span<byte> type = stackalloc byte[4];
        for (var index = 0; index < RecordCount; index++)
        {
            var entry = _shx.GetRecord(index);
            if (entry.Offset < HeaderLength || entry.Length < 4 ||
                (long)entry.Offset + 8 + entry.Length > _shp.Length)
                throw new InvalidDataException("The SHX entry points outside a complete SHP record.");
            _shp.Position = (long)entry.Offset + 8;
            _shp.ReadExactly(type);
            if ((ShapeType)BinaryPrimitives.ReadInt32LittleEndian(type) != ShapeType.Null)
            {
                _hasBounds = true;
                return true;
            }
        }
        _hasBounds = false;
        return false;
    }

    private static BoundingBox Union(BoundingBox first, BoundingBox second) => new(
        new Point(first.Min.ShapeType,
            Math.Min(first.MinX, second.MinX), Math.Min(first.MinY, second.MinY),
            Math.Min(first.MinZ, second.MinZ), MinMeasure(first.MinM, second.MinM)),
        new Point(first.Max.ShapeType,
            Math.Max(first.MaxX, second.MaxX), Math.Max(first.MaxY, second.MaxY),
            Math.Max(first.MaxZ, second.MaxZ), Math.Max(first.MaxM, second.MaxM)));

    private static double MinMeasure(double first, double second) =>
        first <= NumericLimits.MinValue ? second : second <= NumericLimits.MinValue ? first : Math.Min(first, second);

    /// <summary>Writes pending headers and flushes all three component files.</summary>
    public void Flush()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_dirty)
        {
            WriteHeader(_shp, ShapeType, BoundingBox);
            _shx.UpdateHeader(ShapeType, BoundingBox);
        }
        _dbf.Flush();
        _shp.Flush();
        _shx.Flush();
        _dirty = false;
    }

    /// <summary>Flushes pending changes and closes all owned files.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        try
        {
            Flush();
        }
        finally
        {
            _disposed = true;
            try { _dbf.Dispose(); }
            finally
            {
                try { _shx.Dispose(); }
                finally
                {
                    try { _shp.Dispose(); }
                    finally
                    {
                        if (_temporaryDirectory is not null) DeleteTemporaryDirectory(_temporaryDirectory);
                    }
                }
            }
        }
    }

    private static (ShapeType ShapeType, BoundingBox BoundingBox) ReadHeader(Stream stream)
    {
        stream.Position = 0;
        Span<byte> buffer = stackalloc byte[HeaderLength];
        stream.ReadExactly(buffer);
        if (BinaryPrimitives.ReadInt32BigEndian(buffer) != 9994)
            throw new InvalidDataException("Invalid file format. Not a shapefile.");
        if (BinaryPrimitives.ReadInt32LittleEndian(buffer[28..]) != 1000)
            throw new InvalidDataException("Invalid shapefile version. Expected 1000.");
        if ((long)BinaryPrimitives.ReadInt32BigEndian(buffer[24..]) * 2 != stream.Length)
            throw new InvalidDataException("The SHP file length does not match its header.");

        var shapeType = (ShapeType)BinaryPrimitives.ReadInt32LittleEndian(buffer[32..]);
        if (!Enum.IsDefined(shapeType))
            throw new InvalidDataException($"Unknown shape type: {shapeType}.");
        var xMin = BinaryPrimitives.ReadDoubleLittleEndian(buffer[36..]);
        var yMin = BinaryPrimitives.ReadDoubleLittleEndian(buffer[44..]);
        var xMax = BinaryPrimitives.ReadDoubleLittleEndian(buffer[52..]);
        var yMax = BinaryPrimitives.ReadDoubleLittleEndian(buffer[60..]);
        var zMin = BinaryPrimitives.ReadDoubleLittleEndian(buffer[68..]);
        var zMax = BinaryPrimitives.ReadDoubleLittleEndian(buffer[76..]);
        var mMin = BinaryPrimitives.ReadDoubleLittleEndian(buffer[84..]);
        var mMax = BinaryPrimitives.ReadDoubleLittleEndian(buffer[92..]);
        var bounds = shapeType.HasZ
            ? new BoundingBox(new Point(xMin, yMin, zMin, mMin), new Point(xMax, yMax, zMax, mMax))
            : shapeType.HasM
                ? new BoundingBox(new Point(xMin, yMin, mMin), new Point(xMax, yMax, mMax))
                : new BoundingBox(new Point(xMin, yMin), new Point(xMax, yMax));
        return (shapeType, bounds);
    }

    internal static void WriteHeader(Stream stream, ShapeType shapeType, BoundingBox boundingBox)
    {
        var position = stream.Position;
        Span<byte> buffer = stackalloc byte[HeaderLength];
        buffer.Clear();
        BinaryPrimitives.WriteInt32BigEndian(buffer, 9994);
        BinaryPrimitives.WriteInt32BigEndian(buffer[24..], checked((int)(stream.Length / 2)));
        BinaryPrimitives.WriteInt32LittleEndian(buffer[28..], 1000);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[32..], (int)shapeType);
        BinaryPrimitives.WriteDoubleLittleEndian(buffer[36..], boundingBox.MinX);
        BinaryPrimitives.WriteDoubleLittleEndian(buffer[44..], boundingBox.MinY);
        BinaryPrimitives.WriteDoubleLittleEndian(buffer[52..], boundingBox.MaxX);
        BinaryPrimitives.WriteDoubleLittleEndian(buffer[60..], boundingBox.MaxY);
        BinaryPrimitives.WriteDoubleLittleEndian(buffer[68..], shapeType.HasZ ? boundingBox.MinZ : 0.0);
        BinaryPrimitives.WriteDoubleLittleEndian(buffer[76..], shapeType.HasZ ? boundingBox.MaxZ : 0.0);
        BinaryPrimitives.WriteDoubleLittleEndian(buffer[84..], shapeType.HasM ? boundingBox.MinM : 0.0);
        BinaryPrimitives.WriteDoubleLittleEndian(buffer[92..], shapeType.HasM ? boundingBox.MaxM : 0.0);
        stream.Position = 0;
        stream.Write(buffer);
        stream.Position = position;
    }
}
