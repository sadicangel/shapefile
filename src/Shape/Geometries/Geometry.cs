
using System.Buffers.Binary;

namespace Shape.Geometries;

public abstract record Geometry(BoundingBox BoundingBox)
{
    public static T Read<T>(ReadOnlySpan<byte> source) where T : Geometry, IBinaryGeometry<T> => T.Read(source);

    public static Geometry ReadGeometry(ReadOnlySpan<byte> source)
    {
        var shapeType = (ShapeType)BinaryPrimitives.ReadInt32LittleEndian(source);
        return shapeType switch
        {
            ShapeType.Null => Read<NullGeometry>(source),
            ShapeType.Point or ShapeType.PointZ or ShapeType.PointM => Read<Point>(source),
            ShapeType.PolyLine or ShapeType.PolyLineZ or ShapeType.PolyLineM => Read<PolyLine>(source),
            ShapeType.Polygon or ShapeType.PolygonZ or ShapeType.PolygonM => Read<Polygon>(source),
            ShapeType.MultiPoint or ShapeType.MultiPointZ or ShapeType.MultiPointM => Read<MultiPoint>(source),
            ShapeType.MultiPatch => Read<MultiPatch>(source),
            _ => throw new InvalidOperationException($"Unknown shape type: {shapeType}"),
        };
    }
}

file sealed record class NullGeometry : Geometry, IBinaryGeometry<NullGeometry>
{
    public static NullGeometry Empty { get; } = new NullGeometry();

    private NullGeometry() : base(BoundingBox.Empty) { }

    public static NullGeometry Read(ReadOnlySpan<byte> source) => Empty;

    public static implicit operator Point(NullGeometry _) => Point.Empty;
    public static implicit operator Polygon(NullGeometry _) => Polygon.Empty;
    public static implicit operator PolyLine(NullGeometry _) => PolyLine.Empty;
    public static implicit operator MultiPoint(NullGeometry _) => MultiPoint.Empty;
    public static implicit operator MultiPatch(NullGeometry _) => MultiPatch.Empty;
}
