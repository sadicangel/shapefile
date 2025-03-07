using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Shape.Geometries;

public sealed record class PolyLine(ImmutableArray<LineString> Lines)
    : Geometry(BoundingBox.FromPoints(Lines.SelectMany(x => x))), IBinaryGeometry<PolyLine>
{
    public static PolyLine Empty { get; } = new([[]]);

    public static PolyLine Read(ReadOnlySpan<byte> source)
    {
        var shapeType = (ShapeType)BinaryPrimitives.ReadInt32LittleEndian(source);
        if (shapeType is ShapeType.Null) return Empty;
        var ringCount = BinaryPrimitives.ReadInt32LittleEndian(source[36..]);
        var pointCount = BinaryPrimitives.ReadInt32LittleEndian(source[40..]);
        var lineStrings = ImmutableArray.CreateBuilder<LineString>(ringCount);

        var pOffset = 44 + (ringCount * sizeof(int));
        var zOffset = pOffset + 16 + (2 * pointCount * sizeof(double));
        var mOffset = shapeType is ShapeType.PolyLineM ? zOffset : zOffset + 16 + (pointCount * sizeof(double));

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
                if (shapeType is ShapeType.PolyLineZ or ShapeType.PolyLineM)
                {
                    if (shapeType is ShapeType.PolyLineZ)
                        z = BinaryPrimitives.ReadDoubleLittleEndian(source[(zOffset + (start * sizeof(double)))..]);
                    m = BinaryPrimitives.ReadDoubleLittleEndian(source[(mOffset + (start * sizeof(double)))..]);
                }
                points.Add(new Point(x, y, z, m));
                ++start;
            }

            lineStrings.Add(new LineString(points.MoveToImmutable()));
        }
        return new PolyLine(lineStrings.MoveToImmutable());
    }
}
