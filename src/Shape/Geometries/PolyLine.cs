using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Shape.Geometries;

public sealed record class PolyLine(ImmutableArray<LineString> Lines) : Geometry, IGeometry<PolyLine>
{
    public static PolyLine Empty { get; } = new([[]]);

    public override BoundingBox GetBoundingBox() => BoundingBox.FromPoints(Lines.SelectMany(x => x));

    public bool Equals(PolyLine? other) => other is not null && Lines.SequenceEqual(other.Lines);
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var line in Lines)
            hash.Add(line);
        return hash.ToHashCode();
    }

    public static PolyLine Read(ReadOnlySpan<byte> source)
    {
        var shapeType = (ShapeType)BinaryPrimitives.ReadInt32LittleEndian(source);
        if (shapeType is ShapeType.Null) return Empty;
        var stringCount = BinaryPrimitives.ReadInt32LittleEndian(source[36..]);
        var pointCount = BinaryPrimitives.ReadInt32LittleEndian(source[40..]);
        var lineStrings = ImmutableArray.CreateBuilder<LineString>(stringCount);

        var xOffset = 44 + (stringCount * sizeof(int));
        var yOffset = xOffset + sizeof(double);
        var zOffset = xOffset + 16 + (2 * pointCount * sizeof(double));
        var mOffset = shapeType is ShapeType.PolyLineM ? zOffset : zOffset + 16 + (pointCount * sizeof(double));

        var ringIndices = source[44..xOffset];

        for (var i = 0; i < stringCount; ++i)
        {
            var start = BinaryPrimitives.ReadInt32LittleEndian(ringIndices[(i * sizeof(int))..]);
            var end = i + 1 < stringCount
                ? BinaryPrimitives.ReadInt32LittleEndian(ringIndices[((i + 1) * sizeof(int))..])
                : pointCount;

            var points = ImmutableArray.CreateBuilder<Point>(end - start);

            while (start < end)
            {
                var x = BinaryPrimitives.ReadDoubleLittleEndian(source[(xOffset + start * 2 * sizeof(double))..]);
                var y = BinaryPrimitives.ReadDoubleLittleEndian(source[(yOffset + start * 2 * sizeof(double))..]);
                var z = NoValue;
                var m = NoValue;
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
