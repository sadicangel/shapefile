using System.Buffers.Binary;
using Shape.Geometries;

namespace Shape.Serialization;

public sealed class PointSerializer : IGeometrySerializer<Point>
{
    /*
     * ┌───────────┬────────────┬───────────┬─────────┬────────────┬────────────┐
     * │ Position  │ Field      │ Value     │ Type    │ Count      │ Byte Order │
     * ├───────────┼────────────┼───────────┼─────────┼────────────┼────────────┤
     * │ Byte 0    │ ShapeType  │ 1|11|21   │ Int32   │ 1          │ Little     │
     * │ Byte 4    │ X          │ X         │ Double  │ 1          │ Little     │
     * │ Byte 12   │ Y          │ Y         │ Double  │ 1          │ Little     │
     * │ Byte 20*  │ Z|M        │ Z|M       │ Double  │ 1          │ Little     │
     * │ Byte 28*  │ M          │ M         │ Double  │ 1          │ Little     │
     * └───────────┴────────────┴───────────┴─────────┴────────────┴────────────┘
     *
     * Notes:
     *   (*) optional.
     */

    private readonly ref struct PointOffsets : IShapeTypeOffsets<PointOffsets>
    {
        public int ShapeType { get; }
        public int X { get; }
        public int Y { get; }
        public int Z { get; }
        public int M { get; }

        private PointOffsets(ShapeType shapeType)
        {
            ShapeType = 0;
            if (shapeType is Shape.ShapeType.Null) return;
            X = 4;
            Y = 12;
            if (shapeType is Shape.ShapeType.Point) return;
            Z = 20;
            M = 28;
            if (shapeType is Shape.ShapeType.PointM) M = Z;
        }

        public PointOffsets(Point point) : this(point.ShapeType) { }

        public static PointOffsets Detect(ReadOnlySpan<byte> source) => new((ShapeType)BinaryPrimitives.ReadInt32LittleEndian(source));
    }

    public static int GetByteSize(Point geometry) => 4 /* ShapeType */ + geometry.ShapeType.Dimension.PointByteSize;

    public static void Serialize(Span<byte> target, Point point)
    {
        var o = new PointOffsets(point);
        BinaryPrimitives.WriteInt32LittleEndian(target[o.ShapeType..], (int)point.ShapeType);
        if (point.ShapeType is ShapeType.Null) return;
        BinaryPrimitives.WriteDoubleLittleEndian(target[o.X..], point.X);
        BinaryPrimitives.WriteDoubleLittleEndian(target[o.Y..], point.Y);
        if (point.HasZ) BinaryPrimitives.WriteDoubleLittleEndian(target[o.Z..], point.Z);
        if (point.HasM) BinaryPrimitives.WriteDoubleLittleEndian(target[o.M..], point.M);
    }

    public static Point Deserialize(ReadOnlySpan<byte> source)
    {
        var o = PointOffsets.Detect(source);
        var shapeType = (ShapeType)BinaryPrimitives.ReadInt32LittleEndian(source);
        return shapeType switch
        {
            ShapeType.Null => Point.Empty,
            ShapeType.Point => new Point(
                BinaryPrimitives.ReadDoubleLittleEndian(source[o.X..]),
                BinaryPrimitives.ReadDoubleLittleEndian(source[o.Y..])),
            ShapeType.PointZ => new Point(
                BinaryPrimitives.ReadDoubleLittleEndian(source[o.X..]),
                BinaryPrimitives.ReadDoubleLittleEndian(source[o.Y..]),
                BinaryPrimitives.ReadDoubleLittleEndian(source[o.Z..]),
                BinaryPrimitives.ReadDoubleLittleEndian(source[o.M..])),
            ShapeType.PointM => new Point(
                BinaryPrimitives.ReadDoubleLittleEndian(source[o.X..]),
                BinaryPrimitives.ReadDoubleLittleEndian(source[o.Y..]),
                BinaryPrimitives.ReadDoubleLittleEndian(source[o.M..])),
            _ => throw new InvalidOperationException($"Invalid shape type: {shapeType}. Expected: Point, PointZ, or PointM."),
        };
    }
}
