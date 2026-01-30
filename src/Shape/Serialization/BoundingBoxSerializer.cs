using System.Buffers.Binary;
using Shape.Geometries;

namespace Shape.Serialization;

internal interface IBoundingBoxOffsets<T> where T : allows ref struct
{
    int ShapeType { get; }
    int MinX { get; }
    int MinY { get; }
    int MaxX { get; }
    int MaxY { get; }
    int MinZ { get; }
    int MaxZ { get; }
    int MinM { get; }
    int MaxM { get; }
}

internal static class BoundingBoxSerializer
{
    public static void Serialize<T>(Span<byte> target, T boundingBoxOffsets, Geometry geometry)
        where T : IBoundingBoxOffsets<T>, allows ref struct
    {
        var o = boundingBoxOffsets;
        var boundingBox = geometry.GetBoundingBox();
        var shapeType = geometry.ShapeType;

        BinaryPrimitives.WriteDoubleLittleEndian(target[o.MinX..], boundingBox.MinX);
        BinaryPrimitives.WriteDoubleLittleEndian(target[o.MinY..], boundingBox.MinY);
        BinaryPrimitives.WriteDoubleLittleEndian(target[o.MaxX..], boundingBox.MaxX);
        BinaryPrimitives.WriteDoubleLittleEndian(target[o.MaxY..], boundingBox.MaxY);
        if (shapeType.HasZ)
        {
            BinaryPrimitives.WriteDoubleLittleEndian(target[o.MinZ..], boundingBox.MinZ);
            BinaryPrimitives.WriteDoubleLittleEndian(target[o.MaxZ..], boundingBox.MaxZ);
        }

        if (shapeType.HasM)
        {
            BinaryPrimitives.WriteDoubleLittleEndian(target[o.MinM..], boundingBox.MinM);
            BinaryPrimitives.WriteDoubleLittleEndian(target[o.MaxM..], boundingBox.MaxM);
        }
    }

    public static BoundingBox Deserialize<T>(ReadOnlySpan<byte> source, T boundingBoxOffsets)
        where T : IBoundingBoxOffsets<T>, allows ref struct
    {
        var o = boundingBoxOffsets;
        var shapeType = (ShapeType)BinaryPrimitives.ReadInt32LittleEndian(source[o.ShapeType..]);

        var minX = BinaryPrimitives.ReadDoubleLittleEndian(source[o.MinX..]);
        var minY = BinaryPrimitives.ReadDoubleLittleEndian(source[o.MinY..]);
        var maxX = BinaryPrimitives.ReadDoubleLittleEndian(source[o.MaxX..]);
        var maxY = BinaryPrimitives.ReadDoubleLittleEndian(source[o.MaxY..]);

        if (shapeType.HasZ)
        {
            var minZ = BinaryPrimitives.ReadDoubleLittleEndian(source[o.MinZ..]);
            var maxZ = BinaryPrimitives.ReadDoubleLittleEndian(source[o.MaxZ..]);
            var minM = BinaryPrimitives.ReadDoubleLittleEndian(source[o.MinM..]);
            var maxM = BinaryPrimitives.ReadDoubleLittleEndian(source[o.MaxM..]);

            return new BoundingBox(
                Min: new Point(minX, minY, minZ, minM),
                Max: new Point(maxX, maxY, maxZ, maxM));
        }

        if (shapeType.HasM)
        {
            var minM = BinaryPrimitives.ReadDoubleLittleEndian(source[o.MinM..]);
            var maxM = BinaryPrimitives.ReadDoubleLittleEndian(source[o.MaxM..]);

            return new BoundingBox(
                Min: new Point(minX, minY, minM),
                Max: new Point(maxX, maxY, maxM));
        }

        return new BoundingBox(
            Min: new Point(minX, minY),
            Max: new Point(maxX, maxY));
    }
}
