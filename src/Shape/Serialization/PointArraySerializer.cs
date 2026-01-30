using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Diagnostics;
using Shape.Geometries;

namespace Shape.Serialization;

internal interface IPointArrayOffsets<T> : IShapeTypeOffsets<T> where T : allows ref struct
{
    int NumPoints { get; }
    int Points { get; }
    int ZArray { get; }
    int MArray { get; }
}

internal static class PointArraySerializer
{
    public static void Serialize<TOffsets>(Span<byte> target, TOffsets pointsOffset, ShapeType shapeType, IReadOnlyList<Point> points)
        where TOffsets : IPointArrayOffsets<TOffsets>, allows ref struct
    {
        var o = pointsOffset;
        BinaryPrimitives.WriteInt32LittleEndian(target[o.NumPoints..], points.Count);

        var j = 0;
        foreach (var point in points)
        {
            BinaryPrimitives.WriteDoubleLittleEndian(target[(o.Points + j * 16)..], point.X);
            BinaryPrimitives.WriteDoubleLittleEndian(target[(o.Points + j * 16 + 8)..], point.Y);
            if (shapeType.HasZ) BinaryPrimitives.WriteDoubleLittleEndian(target[(o.ZArray + j * 8)..], point.Z);
            if (shapeType.HasM) BinaryPrimitives.WriteDoubleLittleEndian(target[(o.MArray + j * 8)..], point.M);
            j++;
        }
    }

    public static ImmutableArray<Point> Deserialize<TOffsets>(ReadOnlySpan<byte> source, TOffsets pointArrayOffsets)
        where TOffsets : IPointArrayOffsets<TOffsets>, allows ref struct
    {
        var o = pointArrayOffsets;
        var shapeType = ShapeTypeSerializer.Deserialize(source, o);
        var numPoints = BinaryPrimitives.ReadInt32LittleEndian(source[o.NumPoints..]);

        var points = ImmutableArray.CreateBuilder<Point>(numPoints);
        for (var i = 0; i < numPoints; i++)
        {
            var x = BinaryPrimitives.ReadDoubleLittleEndian(source[(o.Points + i * 16)..]);
            var y = BinaryPrimitives.ReadDoubleLittleEndian(source[(o.Points + i * 16 + 8)..]);
            switch (shapeType.Dimension)
            {
                case Dimension.Xyzm:
                    {
                        var z = BinaryPrimitives.ReadDoubleLittleEndian(source[(o.ZArray + i * 8)..]);
                        var m = BinaryPrimitives.ReadDoubleLittleEndian(source[(o.MArray + i * 8)..]);

                        points.Add(new Point(x, y, z, m));
                    }
                    break;

                case Dimension.Xym:
                    {
                        var m = BinaryPrimitives.ReadDoubleLittleEndian(source[(o.MArray + i * 8)..]);
                        points.Add(new Point(x, y, m));
                    }
                    break;

                case Dimension.Xy:
                    {
                        points.Add(new Point(x, y));
                    }
                    break;

                default:
                    throw new UnreachableException($"{nameof(Dimension)} '{shapeType.Dimension}' is invalid");
            }
        }

        return points.MoveToImmutable();
    }
}
