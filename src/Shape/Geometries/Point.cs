
using System.Buffers.Binary;

namespace Shape.Geometries;

public sealed record class Point(double X, double Y, double Z, double M) : Geometry, IBinaryGeometry<Point>
{
    public static Point Empty { get; } = new Point(NoValue, NoValue, NoValue, NoValue);

    public bool HasZ => Z > MinValue;
    public bool HasM => M > MinValue;

    public Point(double x, double y, double m) : this(x, y, NoValue, m) { }

    public Point(double x, double y) : this(x, y, NoValue, NoValue) { }

    public override BoundingBox GetBoundingBox() => new(this, this);

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
