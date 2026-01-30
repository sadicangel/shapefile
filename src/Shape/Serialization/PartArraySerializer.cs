using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Diagnostics;
using Shape.Geometries;

namespace Shape.Serialization;

internal interface IPartArrayOffsets<T> : IShapeTypeOffsets<T> where T : allows ref struct
{
    int NumParts { get; }
    int NumPoints { get; }
    int Parts { get; }
    int Points { get; }
    int ZArray { get; }
    int MArray { get; }
}

internal static class PartArraySerializer
{
    public static void Serialize<TOffsets, TPart>(Span<byte> target, TOffsets pointsOffset, ShapeType shapeType, IReadOnlyList<TPart> parts)
        where TOffsets : IPartArrayOffsets<TOffsets>, allows ref struct
        where TPart : IReadOnlyList<Point>
    {
        var o = pointsOffset;
        BinaryPrimitives.WriteInt32LittleEndian(target[o.NumParts..], parts.Count);
        BinaryPrimitives.WriteInt32LittleEndian(target[o.NumPoints..], parts.Sum(ls => ls.Count));
        var partIndex = 0;
        for (var i = 0; i < parts.Count; i++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(target[(o.Parts + i * sizeof(int))..], partIndex);
            partIndex += parts[i].Count;
        }

        var j = 0;
        foreach (var point in parts.SelectMany(part => part))
        {
            BinaryPrimitives.WriteDoubleLittleEndian(target[(o.Points + j * 16)..], point.X);
            BinaryPrimitives.WriteDoubleLittleEndian(target[(o.Points + j * 16 + 8)..], point.Y);
            if (shapeType.HasZ) BinaryPrimitives.WriteDoubleLittleEndian(target[(o.ZArray + j * 8)..], point.Z);
            if (shapeType.HasM) BinaryPrimitives.WriteDoubleLittleEndian(target[(o.MArray + j * 8)..], point.M);
            j++;
        }
    }

    public static ImmutableArray<TPart> Deserialize<TOffsets, TPart>(ReadOnlySpan<byte> source, TOffsets pointArrayOffsets, Func<int, ImmutableArray<Point>, TPart> factory)
        where TOffsets : IPartArrayOffsets<TOffsets>, allows ref struct
        where TPart : IReadOnlyList<Point>
    {
        var o = pointArrayOffsets;
        var shapeType = ShapeTypeSerializer.Deserialize(source, o);
        var numParts = BinaryPrimitives.ReadInt32LittleEndian(source[o.NumParts..]);
        var numPoints = BinaryPrimitives.ReadInt32LittleEndian(source[o.NumPoints..]);

        var parts = ImmutableArray.CreateBuilder<TPart>(numParts);
        var lengths = GetLengths(source, o.Parts, numParts, numPoints);
        var partIndex = 0;
        var pointIndex = 0;
        foreach (var length in lengths)
        {
            var points = ImmutableArray.CreateBuilder<Point>(length);
            for (var i = 0; i < length; i++, pointIndex++)
            {
                var x = BinaryPrimitives.ReadDoubleLittleEndian(source[(o.Points + pointIndex * 16)..]);
                var y = BinaryPrimitives.ReadDoubleLittleEndian(source[(o.Points + pointIndex * 16 + 8)..]);
                switch (shapeType.Dimension)
                {
                    case Dimension.Xyzm:
                        {
                            var z = BinaryPrimitives.ReadDoubleLittleEndian(source[(o.ZArray + pointIndex * 8)..]);
                            var m = BinaryPrimitives.ReadDoubleLittleEndian(source[(o.MArray + pointIndex * 8)..]);

                            points.Add(new Point(x, y, z, m));
                        }
                        break;

                    case Dimension.Xym:
                        {
                            var m = BinaryPrimitives.ReadDoubleLittleEndian(source[(o.MArray + pointIndex * 8)..]);
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

            parts.Add(factory.Invoke(partIndex++, points.MoveToImmutable()));
        }

        return parts.MoveToImmutable();

        static int[] GetLengths(
            ReadOnlySpan<byte> source,
            int partsOffset,
            int numParts,
            int numPoints)
        {
            var lengths = new int[numParts];
            var previous = BinaryPrimitives.ReadInt32LittleEndian(source.Slice(partsOffset, sizeof(int)));

            for (var i = 1; i < numParts; i++)
            {
                var current = BinaryPrimitives.ReadInt32LittleEndian(source.Slice(partsOffset + i * sizeof(int), sizeof(int)));

                lengths[i - 1] = current - previous;
                previous = current;
            }

            // Last part ends at numPoints
            lengths[numParts - 1] = numPoints - previous;

            return lengths;
        }
    }
}
