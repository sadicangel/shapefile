using System.Collections.Immutable;
using Shape.Serialization;

namespace Shape.Geometries;

public sealed record class MultiPoint(ImmutableArray<Point> Points)
    : Geometry(GetShapeType(Points.FirstOrDefault())), IGeometry<MultiPoint>, IGeometrySerializer<MultiPoint>
{
    public static MultiPoint Empty { get; } = new([]);

    public override BoundingBox GetBoundingBox() => BoundingBox.FromPoints(Points);

    public bool Equals(MultiPoint? other) => other is not null && Points.SequenceEqual(other.Points);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var point in Points)
            hash.Add(point);
        return hash.ToHashCode();
    }

    public static ShapeType GetShapeType(Point? referencePoint)
    {
        if (referencePoint is null) return ShapeType.Null;
        return referencePoint switch
        {
            { HasZ: true } => ShapeType.MultiPointZ,
            { HasM: true } => ShapeType.MultiPointM,
            _ => ShapeType.MultiPoint,
        };
    }

    static int IGeometrySerializer<MultiPoint>.GetByteSize(MultiPoint geometry) => MultiPointSerializer.GetByteSize(geometry);
    static void IGeometrySerializer<MultiPoint>.Serialize(Span<byte> target, MultiPoint geometry) => MultiPointSerializer.Serialize(target, geometry);
    static MultiPoint IGeometrySerializer<MultiPoint>.Deserialize(ReadOnlySpan<byte> source) => MultiPointSerializer.Deserialize(source);
}
