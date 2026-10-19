using System.Collections.Immutable;
using Shape.Serialization;

namespace Shape.Geometries;

public sealed record class Polygon : Geometry, IGeometry<Polygon>, IGeometrySerializer<Polygon>
{
    public static Polygon Empty { get; } = new([[]]);

    public LinearRing ExteriorRing => Rings[0];

    public ImmutableArray<LinearRing> Rings { get; init; }

    public Polygon(ImmutableArray<LinearRing> rings) : base(GetShapeType(rings.SelectMany(x => x.Points).FirstOrDefault()))
    {
        Rings = rings.Length > 0
            ? rings
            : throw new ArgumentException("At least one ring is required.", nameof(rings));
    }

    public override BoundingBox GetBoundingBox() => BoundingBox.FromPoints(Rings.SelectMany(x => x));

    public bool Equals(Polygon? other) => other is not null && Rings.SequenceEqual(other.Rings);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var ring in Rings)
            hash.Add(ring);
        return hash.ToHashCode();
    }

    public static ShapeType GetShapeType(Point? referencePoint)
    {
        if (referencePoint is null) return ShapeType.Null;
        return referencePoint switch
        {
            { HasZ: true } => ShapeType.PolygonZ,
            { HasM: true } => ShapeType.PolygonM,
            _ => ShapeType.Polygon,
        };
    }

    static int IGeometrySerializer<Polygon>.GetByteSize(Polygon geometry) => PolygonSerializer.GetByteSize(geometry);
    static void IGeometrySerializer<Polygon>.Serialize(Span<byte> target, Polygon geometry) => PolygonSerializer.Serialize(target, geometry);
    static Polygon IGeometrySerializer<Polygon>.Deserialize(ReadOnlySpan<byte> source) => PolygonSerializer.Deserialize(source);
}
