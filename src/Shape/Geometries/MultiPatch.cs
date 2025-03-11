using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Shape.Geometries;

public sealed record class MultiPatch(ImmutableArray<Surface> Surfaces) : Geometry, IGeometry<MultiPatch>
{
    public static MultiPatch Empty { get; } = new MultiPatch([]);

    public override BoundingBox GetBoundingBox() => BoundingBox.FromPoints(Surfaces.SelectMany(x => x));

    public bool Equals(MultiPatch? other) => other is not null && Surfaces.SequenceEqual(other.Surfaces);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var surface in Surfaces)
            hash.Add(surface);
        return hash.ToHashCode();
    }

    public static MultiPatch Read(ReadOnlySpan<byte> source)
    {
        var shapeType = (ShapeType)BinaryPrimitives.ReadInt32LittleEndian(source);
        if (shapeType is ShapeType.Null) return Empty;
        var patchCount = BinaryPrimitives.ReadInt32LittleEndian(source[36..]);
        var pointCount = BinaryPrimitives.ReadInt32LittleEndian(source[40..]);
        var surfaces = ImmutableArray.CreateBuilder<Surface>(patchCount);

        var tOffset = 44 + (patchCount * sizeof(int));
        var xOffset = tOffset + (patchCount * sizeof(int));
        var yOffset = xOffset + sizeof(double);
        var zOffset = xOffset + 16 + (2 * pointCount * sizeof(double));
        var mOffset = zOffset + 16 + (pointCount * sizeof(double));

        var ringIndices = source[44..xOffset];

        for (var i = 0; i < patchCount; ++i)
        {
            var start = BinaryPrimitives.ReadInt32LittleEndian(ringIndices[(i * sizeof(int))..]);
            var end = i + 1 < patchCount
                ? BinaryPrimitives.ReadInt32LittleEndian(ringIndices[((i + 1) * sizeof(int))..])
                : pointCount;

            var points = ImmutableArray.CreateBuilder<Point>(end - start);

            while (start < end)
            {
                var x = BinaryPrimitives.ReadDoubleLittleEndian(source[(xOffset + start * 2 * sizeof(double))..]);
                var y = BinaryPrimitives.ReadDoubleLittleEndian(source[(yOffset + start * 2 * sizeof(double))..]);
                var z = BinaryPrimitives.ReadDoubleLittleEndian(source[(zOffset + (start * sizeof(double)))..]);
                var m = BinaryPrimitives.ReadDoubleLittleEndian(source[(mOffset + (start * sizeof(double)))..]);
                points.Add(new Point(x, y, z, m));
                ++start;
            }

            var type = (SurfaceType)BinaryPrimitives.ReadInt32LittleEndian(source[(tOffset + start * sizeof(int))..]);
            surfaces.Add(new Surface(type, points.MoveToImmutable()));
        }
        return new MultiPatch(surfaces.MoveToImmutable());
    }
}
