using System.Buffers.Binary;
using DBase;
using Shape.Geometries;
using Shape.Serialization;

namespace Shape.Tests;

// Small temporary fixtures replace the machine-specific railway dataset. Point
// records, record headers, index entries, and file headers are encoded independently
// of Shapefile's writer so round-trip tests can detect symmetric read/write errors.
internal sealed class ShapefileFixture : IDisposable
{
    private readonly string _directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Shape.Tests-" + Guid.NewGuid().ToString("N"));

    public string Path { get; }
    public string IndexPath => System.IO.Path.ChangeExtension(Path, ".shx");
    public string DbfPath => System.IO.Path.ChangeExtension(Path, ".dbf");

    public ShapefileFixture(ShapeType shapeType, params Geometry[] geometries)
    {
        Directory.CreateDirectory(_directory);
        Path = System.IO.Path.Combine(_directory, "features.shp");
        using var shp = new MemoryStream();
        using var shx = new MemoryStream();
        shp.SetLength(100);
        shx.SetLength(100);
        shp.Position = shx.Position = 100;
        using (var dbf = Dbf.Create(DbfPath,
            [DbfFieldDescriptor.Numeric("Id", 10), DbfFieldDescriptor.Character("Name", 24)]))
        {
            for (var index = 0; index < geometries.Length; index++)
            {
                var contents = Encode(geometries[index]);
                var offset = checked((int)shp.Position);
                WriteBigEndian(shp, index + 1);
                WriteBigEndian(shp, contents.Length / 2);
                shp.Write(contents);
                WriteBigEndian(shx, offset / 2);
                WriteBigEndian(shx, contents.Length / 2);
                dbf.Add(AttributesAt(index));
            }
        }

        var bounds = BoundingBox.FromPoints(geometries
            .Where(geometry => geometry.ShapeType != ShapeType.Null)
            .SelectMany(geometry => new[] { geometry.GetBoundingBox().Min, geometry.GetBoundingBox().Max }));
        WriteHeader(shp, shapeType, bounds);
        WriteHeader(shx, shapeType, bounds);
        File.WriteAllBytes(Path, shp.ToArray());
        File.WriteAllBytes(IndexPath, shx.ToArray());
    }

    public static FeatureAttributes AttributesAt(int index) => new(index + 1, $"Feature {index + 1}");

    public void MarkDeleted(int index)
    {
        using var dbf = File.Open(DbfPath, FileMode.Open, FileAccess.ReadWrite);
        Span<byte> header = stackalloc byte[12];
        dbf.ReadExactly(header);
        var headerLength = BinaryPrimitives.ReadUInt16LittleEndian(header[8..]);
        var recordLength = BinaryPrimitives.ReadUInt16LittleEndian(header[10..]);
        dbf.Position = headerLength + index * recordLength;
        dbf.WriteByte(0x2A);
    }

    public static void WriteInt32(string path, int offset, int value, bool bigEndian = false)
    {
        using var stream = File.Open(path, FileMode.Open, FileAccess.Write);
        stream.Position = offset;
        Span<byte> bytes = stackalloc byte[4];
        if (bigEndian) BinaryPrimitives.WriteInt32BigEndian(bytes, value);
        else BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
        stream.Write(bytes);
    }

    private static byte[] Encode(Geometry geometry)
    {
        if (geometry.ShapeType == ShapeType.Null) return new byte[4];
        if (geometry is Point point)
        {
            var length = point.ShapeType == ShapeType.Point ? 20 : point.ShapeType == ShapeType.PointM ? 28 : 36;
            var result = new byte[length];
            BinaryPrimitives.WriteInt32LittleEndian(result, (int)point.ShapeType);
            BinaryPrimitives.WriteDoubleLittleEndian(result.AsSpan(4), point.X);
            BinaryPrimitives.WriteDoubleLittleEndian(result.AsSpan(12), point.Y);
            if (length > 20)
                BinaryPrimitives.WriteDoubleLittleEndian(result.AsSpan(20), point.ShapeType == ShapeType.PointM ? point.M : point.Z);
            if (length > 28)
                BinaryPrimitives.WriteDoubleLittleEndian(result.AsSpan(28), point.M);
            return result;
        }
        var buffer = new byte[GeometrySerializer.GetByteSize(geometry)];
        GeometrySerializer.Serialize(buffer, geometry);
        return buffer;
    }

    private static void WriteBigEndian(Stream stream, int value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(bytes, value);
        stream.Write(bytes);
    }

    private static void WriteHeader(MemoryStream stream, ShapeType shapeType, BoundingBox bounds)
    {
        var header = new byte[100];
        BinaryPrimitives.WriteInt32BigEndian(header, 9994);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(24), checked((int)stream.Length / 2));
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(28), 1000);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(32), (int)shapeType);
        var coordinates = new[] { bounds.MinX, bounds.MinY, bounds.MaxX, bounds.MaxY,
            bounds.MinZ, bounds.MaxZ, bounds.MinM, bounds.MaxM };
        for (var index = 0; index < coordinates.Length; index++)
            BinaryPrimitives.WriteDoubleLittleEndian(header.AsSpan(36 + index * 8), coordinates[index]);
        stream.Position = 0;
        stream.Write(header);
    }

    public void Dispose()
    {
        foreach (var file in Directory.EnumerateFiles(_directory)) File.Delete(file);
        Directory.Delete(_directory);
    }
}

public sealed record FeatureAttributes(long Id, string Name);
