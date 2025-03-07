
using System.Buffers.Binary;

namespace Shape.Geometries;

public sealed record class Point : Geometry, IBinaryGeometry<Point>
{
    internal const double MinValue = -10e38;
    internal const double NoValue = -101e37;

    public static Point Empty { get; } = new Point(NoValue, NoValue, NoValue, NoValue);

    public double X { get; init; }
    public double Y { get; init; }
    public double Z { get; init; }
    public bool HasZ => Z > MinValue;
    public double M { get; init; }
    public bool HasM => M > MinValue;

    public Point(double x, double y, double z, double m) : base(default(BoundingBox))
    {
        X = x;
        Y = y;
        Z = z;
        M = m;
        BoundingBox = new BoundingBox(this, this);
    }
    public Point(double x, double y, double m) : this(x, y, NoValue, m) { }
    public Point(double x, double y) : this(x, y, NoValue, NoValue) { }

    public static Point Read(ReadOnlySpan<byte> source)
    {
        var shapeType = (ShapeType)BinaryPrimitives.ReadInt32LittleEndian(source);
        return shapeType switch
        {
            ShapeType.Null => Empty,
            ShapeType.Point => new Point(
                BinaryPrimitives.ReadDoubleLittleEndian(source[4..]),
                BinaryPrimitives.ReadDoubleLittleEndian(source[12..])),
            ShapeType.PointZ => new Point(
                BinaryPrimitives.ReadDoubleLittleEndian(source[4..]),
                BinaryPrimitives.ReadDoubleLittleEndian(source[12..]),
                BinaryPrimitives.ReadDoubleLittleEndian(source[20..]),
                BinaryPrimitives.ReadDoubleLittleEndian(source[28..])),
            ShapeType.PointM => new Point(
                BinaryPrimitives.ReadDoubleLittleEndian(source[4..]),
                BinaryPrimitives.ReadDoubleLittleEndian(source[12..]),
                NoValue,
                BinaryPrimitives.ReadDoubleLittleEndian(source[20..])),
            _ => throw new InvalidOperationException($"Invalid shape type: {shapeType}. Expected: Point, PointZ, or PointM."),
        };
    }
}
