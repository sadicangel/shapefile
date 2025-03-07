using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Shape.Geometries;



public sealed record class Polygon(ImmutableArray<LinearRing> Rings)
    : Geometry(BoundingBox.FromPoints(Rings.SelectMany(x => x))), IBinaryGeometry<Polygon>
{
    public static Polygon Empty { get; } = new([[]]);

    public LinearRing ExteriorRing => Rings[0];

    public static Polygon Read(ReadOnlySpan<byte> source)
    {
        var shapeType = (ShapeType)BinaryPrimitives.ReadInt32LittleEndian(source);
        if (shapeType is ShapeType.Null) return Empty;
        var ringCount = BinaryPrimitives.ReadInt32LittleEndian(source[36..]);
        var pointCount = BinaryPrimitives.ReadInt32LittleEndian(source[40..]);
        var linearRings = ImmutableArray.CreateBuilder<LinearRing>(ringCount);

        var pOffset = 44 + (ringCount * sizeof(int));
        var zOffset = pOffset + 16 + (2 * pointCount * sizeof(double));
        var mOffset = shapeType is ShapeType.PolygonM ? zOffset : zOffset + 16 + (pointCount * sizeof(double));

        var ringIndices = source[44..pOffset];

        for (var i = 0; i < ringCount; ++i)
        {
            var start = BinaryPrimitives.ReadInt32LittleEndian(ringIndices[(i * sizeof(int))..]);
            var end = i < ringCount
                ? BinaryPrimitives.ReadInt32LittleEndian(ringIndices[((i + 1) * sizeof(int))..])
                : pointCount;

            var points = ImmutableArray.CreateBuilder<Point>(end - start);

            while (start < end)
            {
                var x = BinaryPrimitives.ReadDoubleLittleEndian(source[(pOffset + start * 2 * sizeof(double))..]);
                var y = BinaryPrimitives.ReadDoubleLittleEndian(source[(pOffset + start * 2 * sizeof(double) + sizeof(double))..]);
                var z = Point.NoValue;
                var m = Point.NoValue;
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
