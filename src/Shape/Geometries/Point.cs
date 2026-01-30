using Shape.Serialization;

namespace Shape.Geometries;

public sealed record class Point(ShapeType ShapeType, double X, double Y, double Z, double M)
    : Geometry(ShapeType), IGeometry<Point>, IGeometrySerializer<Point>
{
    public static Point Empty { get; } = new(ShapeType.Null, NumericLimits.NoValue, NumericLimits.NoValue, NumericLimits.NoValue, NumericLimits.NoValue);

    public bool HasZ => Z > NumericLimits.MinValue;
    public bool HasM => M > NumericLimits.MinValue;

    public Point(double x, double y, double z, double m) : this(ShapeType.PointZ, x, y, z, m) { }
    public Point(double x, double y, double m) : this(ShapeType.PointM, x, y, NumericLimits.NoValue, m) { }

    public Point(double x, double y) : this(ShapeType.Point, x, y, NumericLimits.NoValue, NumericLimits.NoValue) { }

    public override BoundingBox GetBoundingBox() => new(this, this);

    static int IGeometrySerializer<Point>.GetByteSize(Point geometry) => PointSerializer.GetByteSize(geometry);
    static void IGeometrySerializer<Point>.Serialize(Span<byte> target, Point geometry) => PointSerializer.Serialize(target, geometry);
    static Point IGeometrySerializer<Point>.Deserialize(ReadOnlySpan<byte> source) => PointSerializer.Deserialize(source);
}
