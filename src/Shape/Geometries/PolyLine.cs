using System.Collections.Immutable;
using Shape.Serialization;

namespace Shape.Geometries;

public sealed record class PolyLine(ImmutableArray<LineString> Lines)
    : Geometry(GetShapeType(Lines.SelectMany(x => x.Points).FirstOrDefault())), IGeometry<PolyLine>, IGeometrySerializer<PolyLine>
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

    public static ShapeType GetShapeType(Point? referencePoint)
    {
        if (referencePoint is null) return ShapeType.Null;
        return referencePoint switch
        {
            { HasZ: true } => ShapeType.PolyLineZ,
            { HasM: true } => ShapeType.PolyLineM,
            _ => ShapeType.PolyLine,
        };
    }

    static int IGeometrySerializer<PolyLine>.GetByteSize(PolyLine geometry) => PolyLineSerializer.GetByteSize(geometry);
    static void IGeometrySerializer<PolyLine>.Serialize(Span<byte> target, PolyLine geometry) => PolyLineSerializer.Serialize(target, geometry);
    static PolyLine IGeometrySerializer<PolyLine>.Deserialize(ReadOnlySpan<byte> source) => PolyLineSerializer.Deserialize(source);
}
