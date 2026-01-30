using System.Collections.Immutable;
using Shape.Serialization;

namespace Shape.Geometries;

public sealed record class MultiPatch(ImmutableArray<Surface> Surfaces)
    : Geometry(ShapeType.MultiPatch), IGeometry<MultiPatch>, IGeometrySerializer<MultiPatch>
{
    public static MultiPatch Empty { get; } = new([]);

    public override BoundingBox GetBoundingBox() => BoundingBox.FromPoints(Surfaces.SelectMany(x => x));

    public bool Equals(MultiPatch? other) => other is not null && Surfaces.SequenceEqual(other.Surfaces);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var surface in Surfaces)
            hash.Add(surface);
        return hash.ToHashCode();
    }

    static int IGeometrySerializer<MultiPatch>.GetByteSize(MultiPatch geometry) => MultiPatchSerializer.GetByteSize(geometry);
    static void IGeometrySerializer<MultiPatch>.Serialize(Span<byte> target, MultiPatch geometry) => MultiPatchSerializer.Serialize(target, geometry);
    static MultiPatch IGeometrySerializer<MultiPatch>.Deserialize(ReadOnlySpan<byte> source) => MultiPatchSerializer.Deserialize(source);
}
