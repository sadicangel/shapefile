using System.Buffers.Binary;
using Shape.Geometries;

namespace Shape.Serialization;

internal sealed class MultiPointSerializer : IGeometrySerializer<MultiPoint>
{
    /*
     * ┌───────────┬────────────┬───────────┬─────────┬────────────┬────────────┐
     * │ Position  │ Field      │ Value     │ Type    │ Count      │ Byte Order │
     * ├───────────┼────────────┼───────────┼─────────┼────────────┼────────────┤
     * │ Byte 0    │ ShapeType  │ 8|18|28   │ Int32   │ 1          │ Little     │
     * │ Byte 4    │ Box        │ Box       │ Double  │ 4          │ Little     │
     * │ Byte 36   │ NumPoints  │ NumPoints │ Int32   │ 1          │ Little     │
     * │ Byte 40   │ Points     │ Points    │ Point   │ NumPoints  │ Little     │
     * │ Byte X*   │ Z|M min    │ Z|M min   │ Double  │ 1          │ Little     │
     * │ Byte Y+8* │ Z|M max    │ Z|M max   │ Double  │ 1          │ Little     │
     * │ Byte Y+16*│ Z|M Array  │ Z|M Array │ Double  │ NumPoints  │ Little     │
     * │ Byte Y*   │ M min      │ M min     │ Double  │ 1          │ Little     │
     * │ Byte Y+8* │ M max      │ M max     │ Double  │ 1          │ Little     │
     * │ Byte Y+16*│ M Array    │ M Array   │ Double  │ NumPoints  │ Little     │
     * └───────────┴────────────┴───────────┴─────────┴────────────┴────────────┘
     *
     * Notes:
     *   X = 40  + (16 * NumPoints)
     *   Y = X  + 16 + (8 * NumPoints)
     *
     *   (*) optional.
     */

    private readonly ref struct MultiPointOffsets : IBoundingBoxOffsets<MultiPointOffsets>, IPointArrayOffsets<MultiPointOffsets>
    {
        public int ShapeType { get; }
        public int MinX { get; }
        public int MinY { get; }
        public int MaxX { get; }
        public int MaxY { get; }
        public int NumPoints { get; }
        public int Points { get; }
        public int MinZ { get; }
        public int MaxZ { get; }
        public int ZArray { get; }
        public int MinM { get; }
        public int MaxM { get; }
        public int MArray { get; }

        private MultiPointOffsets(ShapeType shapeType, int numPoints)
        {
            ShapeType = 0;
            if (shapeType is Shape.ShapeType.Null) return;

            MinX = 4;
            MinY = 12;
            MaxX = 20;
            MaxY = 28;
            NumPoints = 36;
            Points = 40;
            if (shapeType is Shape.ShapeType.MultiPoint) return;

            var x = 40 + 16 * numPoints;
            var y = x + 16 + 8 * numPoints;
            MinZ = x;
            MaxZ = x + 8;
            ZArray = x + 16;
            if (shapeType is Shape.ShapeType.MultiPointM) y = x;

            MinM = y;
            MaxM = y + 8;
            MArray = y + 16;
        }

        public MultiPointOffsets(MultiPoint multiPoint) : this(multiPoint.ShapeType, multiPoint.Points.Length) { }

        public static MultiPointOffsets Detect(ReadOnlySpan<byte> source)
        {
            var shapeType = (ShapeType)BinaryPrimitives.ReadInt32LittleEndian(source);
            if (shapeType is Shape.ShapeType.Null)
            {
                return new MultiPointOffsets(shapeType, 0);
            }

            shapeType.EnsureCompatibleWith<MultiPoint>();

            var numPoints = BinaryPrimitives.ReadInt32LittleEndian(source[36..]);
            return new MultiPointOffsets(shapeType, numPoints);
        }
    }

    public static int GetByteSize(MultiPoint geometry)
    {
        if (geometry.ShapeType is ShapeType.Null) return 4 /* ShapeType */;

        var dimension = geometry.ShapeType.Dimension;
        return
            4 /* ShapeType */ +
            dimension.BoundingBoxSize +
            4 /* NumPoints */ +
            geometry.Points.Length * dimension.PointByteSize;
    }

    public static void Serialize(Span<byte> target, MultiPoint multiPoint)
    {
        var offsets = new MultiPointOffsets(multiPoint);
        ShapeTypeSerializer.Serialize(target, offsets, multiPoint);
        BoundingBoxSerializer.Serialize(target, offsets, multiPoint);
        PointArraySerializer.Serialize(target, offsets, multiPoint.ShapeType, multiPoint.Points);
    }

    public static MultiPoint Deserialize(ReadOnlySpan<byte> source)
    {
        var offsets = MultiPointOffsets.Detect(source);
        var shapeType = ShapeTypeSerializer.Deserialize(source, offsets);
        if (shapeType is ShapeType.Null) return MultiPoint.Empty;

        var points = PointArraySerializer.Deserialize(source, offsets);
        return new MultiPoint(points);
    }
}
