using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using DBase;
using DotNext.Buffers;
using Shape.Geometries;

namespace Shape;

public abstract class Shapefile<TGeometry, TAttributes>
    where TGeometry : Geometry
{
}

public sealed class Shapefile : IDisposable
{
    private readonly Stream _shp;
    private readonly ShapeIndex _shx;
    private readonly Dbf _dbf;
    private bool _dirty;

    public ShapeType ShapeType { get; }
    public BoundingBox BoundingBox { get; }

    public int RecordCount => _shx.RecordCount;

    private Shapefile(Stream shp, ShapeIndex shx, Dbf dbf, ShapeType shapeType, BoundingBox boundingBox)
    {
        _shp = shp;
        _shx = shx;
        ShapeType = shapeType;
        BoundingBox = boundingBox;
        _dbf = dbf;
    }

    public static Shapefile Open(string fileName)
    {
        return Open(
            new FileStream(fileName, FileMode.Open, FileAccess.ReadWrite),
            ShapeIndex.Open(Path.ChangeExtension(fileName, ".shx")),
            Dbf.Open(Path.ChangeExtension(fileName, ".dbf")));
    }

    internal static Shapefile Open(Stream shp, ShapeIndex shx, Dbf dbf)
    {
        ArgumentNullException.ThrowIfNull(shp);
        ArgumentNullException.ThrowIfNull(shx);
        ArgumentNullException.ThrowIfNull(dbf);

        var (shapeType, boundingBox) = ReadHeader(shp);
        return new Shapefile(shp, shx, dbf, shapeType, boundingBox);
    }

    public void Dispose()
    {
        Flush();
        _dbf.Dispose();
        _shp.Dispose();
        _shx.Dispose();
    }

    public void Flush()
    {
        if (_dirty)
        {
            _dirty = false;
            WriteHeader(_shp, ShapeType, BoundingBox);
        }
        _dbf.Flush();
        _shp.Flush();
        _shx.Flush();
    }

    private static (ShapeType ShapeType, BoundingBox BoundingBox) ReadHeader(Stream stream)
    {
        stream.Position = 0;
        Span<byte> buffer = stackalloc byte[100];
        stream.ReadExactly(buffer);

        if (BinaryPrimitives.ReadInt32BigEndian(buffer[0..]) != 9994)
        {
            throw new InvalidOperationException("Invalid file format. Not a shapefile.");
        }

        if (BinaryPrimitives.ReadInt32LittleEndian(buffer[28..]) != 1000)
        {
            throw new InvalidOperationException($"Invalid version. Expected: '1000'. Actual: '{BinaryPrimitives.ReadInt32BigEndian(buffer[28..])}'");
        }

        var shapeType = (ShapeType)BinaryPrimitives.ReadInt32LittleEndian(buffer[32..]);
        var xMin = BinaryPrimitives.ReadDoubleLittleEndian(buffer[36..]);
        var yMin = BinaryPrimitives.ReadDoubleLittleEndian(buffer[44..]);
        var xMax = BinaryPrimitives.ReadDoubleLittleEndian(buffer[52..]);
        var yMax = BinaryPrimitives.ReadDoubleLittleEndian(buffer[60..]);
        var zMin = BinaryPrimitives.ReadDoubleLittleEndian(buffer[68..]);
        var zMax = BinaryPrimitives.ReadDoubleLittleEndian(buffer[76..]);
        var mMin = BinaryPrimitives.ReadDoubleLittleEndian(buffer[84..]);
        var mMax = BinaryPrimitives.ReadDoubleLittleEndian(buffer[92..]);

        Unsafe.SkipInit(out BoundingBox boundingBox);
        if (shapeType.HasZ())
        {
            boundingBox = new BoundingBox(new Point(xMin, yMin, zMin, mMin), new Point(xMax, yMax, zMax, mMax));
        }
        else if (shapeType.HasM())
        {
            boundingBox = new BoundingBox(new Point(xMin, yMin, mMin), new Point(xMax, yMax, mMax));
        }
        else
        {
            boundingBox = new BoundingBox(new Point(xMin, yMin), new Point(xMax, yMax));
        }

        return (shapeType, boundingBox);
    }

    private static void WriteHeader(Stream stream, ShapeType shapeType, BoundingBox boundingBox)
    {
        stream.Position = 0;
        Span<byte> buffer = stackalloc byte[100];
        BinaryPrimitives.WriteInt32BigEndian(buffer[0..], 9994);
        BinaryPrimitives.WriteInt32BigEndian(buffer[24..], unchecked((int)(stream.Length / 2)));
        BinaryPrimitives.WriteInt32LittleEndian(buffer[28..], 1000);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[32..], (int)shapeType);
        BinaryPrimitives.WriteDoubleLittleEndian(buffer[36..], boundingBox.MinX);
        BinaryPrimitives.WriteDoubleLittleEndian(buffer[44..], boundingBox.MinY);
        BinaryPrimitives.WriteDoubleLittleEndian(buffer[52..], boundingBox.MaxX);
        BinaryPrimitives.WriteDoubleLittleEndian(buffer[60..], boundingBox.MaxY);
        BinaryPrimitives.WriteDoubleLittleEndian(buffer[68..], shapeType.HasZ() ? boundingBox.MinZ : 0.0);
        BinaryPrimitives.WriteDoubleLittleEndian(buffer[76..], shapeType.HasZ() ? boundingBox.MaxZ : 0.0);
        BinaryPrimitives.WriteDoubleLittleEndian(buffer[84..], shapeType.HasM() ? boundingBox.MinM : 0.0);
        BinaryPrimitives.WriteDoubleLittleEndian(buffer[92..], shapeType.HasM() ? boundingBox.MaxM : 0.0);
    }


    internal int PrepareStreamToReadRecord(int index)
    {
        var (offset, length) = _shx.GetRecord(index);
#if !DEBUG
        _shp.Position = offset + 8;
#else
        _shp.Position = offset;
        Span<byte> buffer = stackalloc byte[8];
        _shp.ReadExactly(buffer);
        Debug.Assert(BinaryPrimitives.ReadInt32BigEndian(buffer[0..]) == index + 1);
        Debug.Assert(BinaryPrimitives.ReadInt32BigEndian(buffer[4..]) == length / 2);
#endif
        return length;
    }


    public ShapeRecord<Geometry, DbfRecord> GetRecord(int index)
    {
        var length = PrepareStreamToReadRecord(index);

        using var buffer = length < 256
            ? new SpanOwner<byte>(stackalloc byte[length])
            : new SpanOwner<byte>(length);

        _shp.ReadExactly(buffer.Span);

        var geometry = Geometry.Read(buffer.Span, ShapeType);
        var attributes = _dbf.GetRecord(index);
        return new ShapeRecord<Geometry, DbfRecord>(geometry, attributes);
    }

    public ShapeRecord<TGeometry, DbfRecord> GetRecord<TGeometry>(int index)
        where TGeometry : Geometry, IBinaryGeometry<TGeometry>
    {
        // Move stream to the start of the record.
        // Read the record header.
        // Read the record data.
        // TGeometry geometry = TGeometry.Read(data);
        var attributes = _dbf.GetRecord(index);
        // return new ShapeRecord<TGeometry, DbfRecord>(geometry, attributes);
        throw new NotImplementedException();
    }

    public ShapeRecord<TGeometry, TAttributes> GetRecord<TGeometry, TAttributes>(int index)
        where TGeometry : Geometry
    {
        // Move stream to the start of the record.
        // Read the record header.
        // Read the record data.
        // TGeometry geometry = TGeometry.Read(data);
        var attributes = _dbf.GetRecord<TAttributes>(index);
        // return new ShapeRecord<TGeometry, TAttributes>(geometry, attributes);
        throw new NotImplementedException();
    }

    public IEnumerable<ShapeRecord<Geometry, DbfRecord>> EnumerateRecords()
    {
        for (var i = 0; i < RecordCount; ++i)
            yield return GetRecord(i);
    }

    public IEnumerable<ShapeRecord<TGeometry, DbfRecord>> EnumerateRecords<TGeometry>()
        where TGeometry : Geometry, IBinaryGeometry<TGeometry>
    {
        for (var i = 0; i < RecordCount; ++i)
            yield return GetRecord<TGeometry>(i);
    }

    public IEnumerable<ShapeRecord<TGeometry, TAttributes>> EnumerateRecords<TGeometry, TAttributes>()
        where TGeometry : Geometry
    {
        for (var i = 0; i < RecordCount; ++i)
            yield return GetRecord<TGeometry, TAttributes>(i);
    }
}

public sealed record class ShapeRecord<TGeometry, TAttributes>(TGeometry Geometry, TAttributes Attributes)
    where TGeometry : Geometry;

public sealed record class ShapeRecord<TGeometry>(TGeometry Geometry, DbfRecord Attributes)
    where TGeometry : Geometry;

public sealed record class ShapeRecord(Geometry Geometry, DbfRecord Attributes);
