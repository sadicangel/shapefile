using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Shape.Geometries;



public sealed record class Polygon(ImmutableArray<LinearRing> Rings) : Geometry, IBinaryGeometry<Polygon>
{
    public static Polygon Empty { get; } = new([[]]);

    public LinearRing ExteriorRing => Rings[0];

    public override BoundingBox GetBoundingBox() => BoundingBox.FromPoints(Rings.SelectMany(x => x));

    public bool Equals(Polygon? other) => other is not null && Rings.SequenceEqual(other.Rings);
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var ring in Rings)
            hash.Add(ring);
        return hash.ToHashCode();
    }

    public static Polygon Read(ReadOnlySpan<byte> source)
    {
        var shapeType = (ShapeType)BinaryPrimitives.ReadInt32LittleEndian(source);
        if (shapeType is ShapeType.Null) return Empty;
        var ringCount = BinaryPrimitives.ReadInt32LittleEndian(source[36..]);
        var pointCount = BinaryPrimitives.ReadInt32LittleEndian(source[40..]);
        var linearRings = ImmutableArray.CreateBuilder<LinearRing>(ringCount);

        var xOffset = 44 + (ringCount * sizeof(int));
        var yOffset = xOffset + sizeof(double);
        var zOffset = xOffset + 16 + (2 * pointCount * sizeof(double));
        var mOffset = shapeType is ShapeType.PolygonM ? zOffset : zOffset + 16 + (pointCount * sizeof(double));

        var ringIndices = source[44..xOffset];

        for (var i = 0; i < ringCount; ++i)
        {
            var start = BinaryPrimitives.ReadInt32LittleEndian(ringIndices[(i * sizeof(int))..]);
            var end = i + 1 < ringCount
                ? BinaryPrimitives.ReadInt32LittleEndian(ringIndices[((i + 1) * sizeof(int))..])
                : pointCount;

            var points = ImmutableArray.CreateBuilder<Point>(end - start);

            while (start < end)
            {
                var x = BinaryPrimitives.ReadDoubleLittleEndian(source[(xOffset + start * 2 * sizeof(double))..]);
                var y = BinaryPrimitives.ReadDoubleLittleEndian(source[(yOffset + start * 2 * sizeof(double))..]);
                var z = NoValue;
                var m = NoValue;
                if (shapeType is ShapeType.PolygonZ or ShapeType.PolygonM)
                {
                    if (shapeType is ShapeType.PolygonZ)
                        z = BinaryPrimitives.ReadDoubleLittleEndian(source[(zOffset + (start * sizeof(double)))..]);
                    m = BinaryPrimitives.ReadDoubleLittleEndian(source[(mOffset + (start * sizeof(double)))..]);
                }
                points.Add(new Point(x, y, z, m));
                ++start;
            }

            linearRings.Add(new LinearRing(points.MoveToImmutable()));
        }
        return new Polygon(linearRings.MoveToImmutable());
    }
}
