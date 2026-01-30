using System.Buffers.Binary;
using System.Collections;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using DBase;
using DotNext.Buffers;
using Shape.Geometries;
using Shape.Serialization;

namespace Shape;

public sealed class Shapefile : Shapefile<Geometry, DbfRecord, GeometrySerializer>
{
    internal Shapefile(Stream shp, ShapeIndex shx, Dbf dbf, ShapeType shapeType, BoundingBox boundingBox)
        : base(shp, shx, dbf, shapeType, boundingBox) { }

    public static Shapefile Open(string fileName)
    {
        var (shp, shx, dbf, shapeType, boundingBox) = OpenCore(fileName);
        return new Shapefile(shp, shx, dbf, shapeType, boundingBox);
    }

    public static Shapefile<TGeometry> Open<TGeometry>(string fileName)
        where TGeometry : Geometry, IGeometrySerializer<TGeometry>
    {
        var (shp, shx, dbf, shapeType, boundingBox) = OpenCore(fileName);
        return new Shapefile<TGeometry>(shp, shx, dbf, shapeType, boundingBox);
    }

    public static Shapefile<TGeometry, TAttributes> Open<TGeometry, TAttributes>(string fileName)
        where TGeometry : Geometry, IGeometrySerializer<TGeometry>
    {
        var (shp, shx, dbf, shapeType, boundingBox) = OpenCore(fileName);
        return new Shapefile<TGeometry, TAttributes>(shp, shx, dbf, shapeType, boundingBox);
    }

    private static (FileStream shp, ShapeIndex shx, Dbf dbf, ShapeType shapeType, BoundingBox boundingBox) OpenCore(string fileName)
    {
        var shp = new FileStream(fileName, FileMode.Open, FileAccess.ReadWrite);
        var shx = ShapeIndex.Open(Path.ChangeExtension(fileName, ".shx"));
        var dbf = Dbf.Open(Path.ChangeExtension(fileName, ".dbf"));

        var (shapeType, boundingBox) = ReadHeader(shp);
        return (shp, shx, dbf, shapeType, boundingBox);
    }

    //public Shapefile<TGeometry> As<TGeometry>() where TGeometry : Geometry, IGeometrySerializer<TGeometry>
    //{
    //    return new Shapefile<TGeometry>()
    //}

    //public Shapefile<TGeometry, TAttributes> As<TGeometry, TAttributes>() where TGeometry : Geometry, IGeometrySerializer<TGeometry>
    //{
    //    return new Shapefile<TGeometry, TAttributes>()
    //}
}

public sealed class Shapefile<TGeometry> : Shapefile<TGeometry, DbfRecord, TGeometry>
    where TGeometry : Geometry, IGeometrySerializer<TGeometry>
{
    internal Shapefile(Stream shp, ShapeIndex shx, Dbf dbf, ShapeType shapeType, BoundingBox boundingBox)
        : base(shp, shx, dbf, shapeType, boundingBox) { }
}

public sealed class Shapefile<TGeometry, TAttributes> : Shapefile<TGeometry, TAttributes, TGeometry>
    where TGeometry : Geometry, IGeometrySerializer<TGeometry>
{
    internal Shapefile(Stream shp, ShapeIndex shx, Dbf dbf, ShapeType shapeType, BoundingBox boundingBox)
        : base(shp, shx, dbf, shapeType, boundingBox) { }
}

public abstract class Shapefile<TGeometry, TAttributes, TSerializer>
    : IShapefile, IDisposable, IEnumerable<ShapeRecord<TGeometry, TAttributes>>
    where TGeometry : Geometry
    where TSerializer : IGeometrySerializer<TGeometry>
{
    private bool _dirty;
    private readonly Stream _shp;
    private readonly ShapeIndex _shx;
    private readonly Dbf _dbf;

    protected Shapefile(Stream shp, ShapeIndex shx, Dbf dbf, ShapeType shapeType, BoundingBox boundingBox)
    {
        _shp = shp;
        _shx = shx;
        _dbf = dbf;
        ShapeType = shapeType;
        BoundingBox = boundingBox;

        ShapeType.EnsureCompatibleWith<TGeometry>();
    }

    public ShapeType ShapeType { get; }

    public BoundingBox BoundingBox { get; }

    public int RecordCount => _shx.RecordCount;

    internal int PrepareStreamToReadRecord(int index)
    {
        var (offset, length) = _shx.GetRecord(index);
#if !DEBUG
        shp.Position = offset + 8;
#else
        _shp.Position = offset;
        Span<byte> buffer = stackalloc byte[8];
        _shp.ReadExactly(buffer);
        Debug.Assert(BinaryPrimitives.ReadInt32BigEndian(buffer[0..]) == index + 1);
        Debug.Assert(BinaryPrimitives.ReadInt32BigEndian(buffer[4..]) == length / 2);
#endif
        return length;
    }

    protected static (ShapeType ShapeType, BoundingBox BoundingBox) ReadHeader(Stream stream)
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
        if (shapeType.HasZ)
        {
            boundingBox = new BoundingBox(new Point(xMin, yMin, zMin, mMin), new Point(xMax, yMax, zMax, mMax));
        }
        else if (shapeType.HasM)
        {
            boundingBox = new BoundingBox(new Point(xMin, yMin, mMin), new Point(xMax, yMax, mMax));
        }
        else
        {
            boundingBox = new BoundingBox(new Point(xMin, yMin), new Point(xMax, yMax));
        }

        return (shapeType, boundingBox);
    }

    protected static void WriteHeader(Stream stream, ShapeType shapeType, BoundingBox boundingBox)
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
        BinaryPrimitives.WriteDoubleLittleEndian(buffer[68..], shapeType.HasZ ? boundingBox.MinZ : 0.0);
        BinaryPrimitives.WriteDoubleLittleEndian(buffer[76..], shapeType.HasZ ? boundingBox.MaxZ : 0.0);
        BinaryPrimitives.WriteDoubleLittleEndian(buffer[84..], shapeType.HasM ? boundingBox.MinM : 0.0);
        BinaryPrimitives.WriteDoubleLittleEndian(buffer[92..], shapeType.HasM ? boundingBox.MaxM : 0.0);
    }

    public ShapeRecord<TGeometry, TAttributes> GetRecord(int index)
    {
        var length = PrepareStreamToReadRecord(index);

        using var buffer = length < 256
            ? new SpanOwner<byte>(stackalloc byte[length])
            : new SpanOwner<byte>(length);

        _shp.ReadExactly(buffer.Span);

        var geometry = TSerializer.Deserialize(buffer.Span);
        var attributes = _dbf.GetRecord<TAttributes>(index);
        return ShapeRecord<TGeometry, TAttributes>.Create(geometry, attributes);
    }

    // TODO: We can probably unify this with the method above by moving the read into another class.
    ShapeRecord<Geometry, DbfRecord> IShapefile.GetRecord(int index)
    {
        var length = PrepareStreamToReadRecord(index);

        using var buffer = length < 256
            ? new SpanOwner<byte>(stackalloc byte[length])
            : new SpanOwner<byte>(length);

        _shp.ReadExactly(buffer.Span);

        var geometry = GeometrySerializer.Deserialize(buffer.Span);
        var attributes = _dbf.GetRecord(index);
        return new ShapeRecord<Geometry, DbfRecord>(geometry, attributes);
    }

    public IEnumerator<ShapeRecord<TGeometry, TAttributes>> GetEnumerator()
    {
        var recordCount = RecordCount;
        for (var i = 0; i < recordCount; ++i)
            yield return GetRecord(i);
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void Add(ShapeRecord<TGeometry, TAttributes> record) => Add(record.Geometry, record.Attributes);

    public void Add(TGeometry geometry, TAttributes attributes)
    {
        _dirty = true;
        var index = new ShapeIndexRecord((int)_shp.Length, TSerializer.GetByteSize(geometry));

        using var buffer = index.Length < 256
            ? new SpanOwner<byte>(stackalloc byte[index.Length])
            : new SpanOwner<byte>(index.Length);

        TSerializer.Serialize(buffer.Span, geometry);

        _shx.Add(index);
        _dbf.Add(attributes);
        _shp.Write(buffer.Span);
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
}
