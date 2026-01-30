using System.Buffers.Binary;
using Shape.Geometries;

namespace Shape.Serialization;

internal sealed class PolyLineSerializer : IGeometrySerializer<PolyLine>
{
    /*
     * ┌───────────┬────────────┬───────────┬─────────┬────────────┬────────────┐
     * │ Position  │ Field      │ Value     │ Type    │ Count      │ Byte Order │
     * ├───────────┼────────────┼───────────┼─────────┼────────────┼────────────┤
     * │ Byte 0    │ ShapeType  │ 3|13|23   │ Int32   │ 1          │ Little     │
     * │ Byte 4    │ Box        │ Box       │ Double  │ 4          │ Little     │
     * │ Byte 36   │ NumParts   │ NumParts  │ Int32   │ 1          │ Little     │
     * │ Byte 40   │ NumPoints  │ NumPoints │ Int32   │ 1          │ Little     │
     * │ Byte 44   │ Parts      │ Parts     │ Int32   │ NumParts   │ Little     │
     * │ Byte X    │ Points     │ Points    │ Point   │ NumPoints  │ Little     │
     * │ Byte Y*   │ Z|M min    │ Z|M min   │ Double  │ 1          │ Little     │
     * │ Byte Y+8* │ Z|M max    │ Z|M max   │ Double  │ 1          │ Little     │
     * │ Byte Y+16*│ Z|M Array  │ Z|M Array │ Double  │ NumPoints  │ Little     │
     * │ Byte Z*   │ M min      │ M min     │ Double  │ 1          │ Little     │
     * │ Byte Z+8* │ M max      │ M max     │ Double  │ 1          │ Little     │
     * │ Byte Z+16*│ M Array    │ M Array   │ Double  │ NumPoints  │ Little     │
     * └───────────┴────────────┴───────────┴─────────┴────────────┴────────────┘
     *
     * Notes:
     *   X = 44 + (4  * NumParts)
     *   Y = X  + (16 * NumPoints)
     *   Z = Y  + 16 + (8 * NumPoints)
     *
     *   (*) optional.
     */

    private readonly ref struct PolyLineOffsets : IBoundingBoxOffsets<PolyLineOffsets>, IPartArrayOffsets<PolyLineOffsets>
    {
        public int ShapeType { get; }
        public int MinX { get; }
        public int MinY { get; }
        public int MaxX { get; }
        public int MaxY { get; }
        public int NumParts { get; }
        public int NumPoints { get; }
        public int Parts { get; }
        public int Points { get; }
        public int MinZ { get; }
        public int MaxZ { get; }
        public int ZArray { get; }
        public int MinM { get; }
        public int MaxM { get; }
        public int MArray { get; }

        private PolyLineOffsets(ShapeType shapeType, int numParts, int numPoints)
        {
            ShapeType = 0;
            if (shapeType is Shape.ShapeType.Null) return;

            var x = 44 + 4 * numParts;
            MinX = 4;
            MinY = 12;
            MaxX = 20;
            MaxY = 28;
            NumParts = 36;
            NumPoints = 40;
            Parts = 44;
            Points = x;
            if (shapeType is Shape.ShapeType.PolyLine) return;

            var y = x + 16 * numPoints;
            var z = y + 16 + 8 * numPoints;
            MinZ = y;
            MaxZ = y + 8;
            ZArray = y + 16;
            if (shapeType is Shape.ShapeType.PolyLineM) z = y;

            MinM = z;
            MaxM = z + 8;
            MArray = z + 16;
        }

        public PolyLineOffsets(PolyLine polyLine) : this(
            shapeType: polyLine.ShapeType,
            numParts: polyLine.Lines.Length,
            numPoints: polyLine.Lines.Sum(ls => ls.Length))
        { }

        public static PolyLineOffsets Detect(ReadOnlySpan<byte> source)
        {
            var shapeType = (ShapeType)BinaryPrimitives.ReadInt32LittleEndian(source);
            if (shapeType is Shape.ShapeType.Null)
            {
                return new PolyLineOffsets(shapeType, 0, 0);
            }

            shapeType.EnsureCompatibleWith<PolyLine>();

            var numParts = BinaryPrimitives.ReadInt32LittleEndian(source[36..]);
            var numPoints = BinaryPrimitives.ReadInt32LittleEndian(source[40..]);
            return new PolyLineOffsets(shapeType, numParts, numPoints);
        }
    }

    public static int GetByteSize(PolyLine geometry)
    {
        if (geometry.ShapeType is ShapeType.Null) return 4 /* ShapeType */;

        var dimension = geometry.ShapeType.Dimension;
        return
            4 /* ShapeType */ +
            dimension.BoundingBoxSize +
            4 /* NumParts */ +
            4 /* NumPoints */ +
            geometry.Lines.Length * 4 /* Parts */ +
            geometry.Lines.Sum(x => x.Length) * dimension.PointByteSize /* Points */;
    }

    public static void Serialize(Span<byte> target, PolyLine polyLine)
    {
        var offsets = new PolyLineOffsets(polyLine);
        ShapeTypeSerializer.Serialize(target, offsets, polyLine);
        if (polyLine.ShapeType is ShapeType.Null) return;
        BoundingBoxSerializer.Serialize(target, offsets, polyLine);
        PartArraySerializer.Serialize(target, offsets, polyLine.ShapeType, polyLine.Lines);
    }

    public static PolyLine Deserialize(ReadOnlySpan<byte> source)
    {
        var offsets = PolyLineOffsets.Detect(source);
        var shapeType = ShapeTypeSerializer.Deserialize(source, offsets);
        if (shapeType is ShapeType.Null) return PolyLine.Empty;
        var lines = PartArraySerializer.Deserialize(source, offsets, (_, points) => new LineString(points));
        return new PolyLine(lines);
    }
}
