using System.Buffers.Binary;
using System.Collections.Immutable;
using DotNext.Buffers;

namespace Shape.Geometries;

public sealed record class MultiPoint(ImmutableArray<Point> Points)
    : Geometry(BoundingBox.FromPoints(Points)), IBinaryGeometry<MultiPoint>, IEquatable<MultiPoint>
{
    public static MultiPoint Empty { get; } = new MultiPoint([]);

    public static MultiPoint Read(ReadOnlySpan<byte> source)
    {
        var shapeType = (ShapeType)BinaryPrimitives.ReadInt32LittleEndian(source);
        if (shapeType is ShapeType.Null) return Empty;
        // Skip bounding box.
        var pointCount = BinaryPrimitives.ReadInt32LittleEndian(source[36..]);
        using var memory = new SpanOwner<(double X, double Y, double Z, double M)>(pointCount);
        var offset = 40;
        for (var i = 0; i < pointCount; i += 2)
        {
            memory.Span[i].X = BinaryPrimitives.ReadDoubleLittleEndian(source[offset..]);
            offset += 8;
            memory.Span[i].Y = BinaryPrimitives.ReadDoubleLittleEndian(source[offset..]);
            offset += 8;

            // Initialize Z and M values with NoValue.
            memory.Span[i].Z = Point.NoValue;
            memory.Span[i].M = Point.NoValue;
        }
        if (shapeType is ShapeType.MultiPointZ or ShapeType.MultiPointM)
        {
            if (shapeType is ShapeType.MultiPointZ)
            {
                offset += 16; // Skip Z range.
                for (var i = 0; i < pointCount; i += 2)
                {
                    memory.Span[i].Z = BinaryPrimitives.ReadDoubleLittleEndian(source[offset..]);
                    offset += 8;
                }
            }

            offset += 16; // Skip M range.
            for (var i = 0; i < pointCount; i += 2)
            {
                memory.Span[i].M = BinaryPrimitives.ReadDoubleLittleEndian(source[offset..]);
                offset += 8;
            }
        }

        var builder = ImmutableArray.CreateBuilder<Point>(pointCount);
        for (var i = 0; i < pointCount; i += 2)
        {
            builder.Add(new Point(memory.Span[i].X, memory.Span[i].Y, memory.Span[i].Z, memory.Span[i].M));
        }
        return new MultiPoint(builder.MoveToImmutable());
    }

    public bool Equals(MultiPoint? other) => other is not null && Points.SequenceEqual(other.Points);

    public override int GetHashCode() => HashCode.Combine(Points);
}
