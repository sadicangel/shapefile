using System.Buffers.Binary;
using Shape.Geometries;

namespace Shape.Serialization;

internal sealed class MultiPatchSerializer : IGeometrySerializer<MultiPatch>
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
     * │ Byte W    │ PartTypes  │ PartTypes │ Int32   │ NumParts   │ Little     │
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
     *   W = 44 + (4  * NumParts)
     *   X = W + (4  * NumParts)
     *   Y = X  + (16 * NumPoints)
     *   Z = Y  + 16 + (8 * NumPoints)
     *
     *   (*) optional.
     */

    private readonly ref struct MultiPatchOffsets : IBoundingBoxOffsets<MultiPatchOffsets>, IPartArrayOffsets<MultiPatchOffsets>
    {
        public int ShapeType { get; }
        public int MinX { get; }
        public int MinY { get; }
        public int MaxX { get; }
        public int MaxY { get; }
        public int NumParts { get; }
        public int NumPoints { get; }
        public int Parts { get; }
        public int PartTypes { get; }
        public int Points { get; }
        public int MinZ { get; }
        public int MaxZ { get; }
        public int ZArray { get; }
        public int MinM { get; }
        public int MaxM { get; }
        public int MArray { get; }

        private MultiPatchOffsets(ShapeType shapeType, int numParts, int numPoints)
        {
            ShapeType = 0;
            if (shapeType is Shape.ShapeType.Null) return;

            var w = 44 + 4 * numParts;
            var x = w + 4 * numParts;
            var y = x + 16 * numPoints;
            var z = y + 16 + 8 * numPoints;

            MinX = 4;
            MinY = 12;
            MaxX = 20;
            MaxY = 28;
            NumParts = 36;
            NumPoints = 40;
            Parts = 44;
            PartTypes = w;
            Points = x;
            MinZ = y;
            MaxZ = y + 8;
            ZArray = y + 16;

            MinM = z;
            MaxM = z + 8;
            MArray = z + 16;
        }

        public MultiPatchOffsets(MultiPatch multiPatch) : this(
            shapeType: multiPatch.ShapeType,
            numParts: multiPatch.Surfaces.Length,
            numPoints: multiPatch.Surfaces.Sum(s => s.Points.Length))
        { }

        public static MultiPatchOffsets Detect(ReadOnlySpan<byte> source)
        {
            var shapeType = (ShapeType)BinaryPrimitives.ReadInt32LittleEndian(source);
            if (shapeType is Shape.ShapeType.Null)
            {
                return new MultiPatchOffsets(shapeType, 0, 0);
            }

            shapeType.EnsureCompatibleWith<MultiPatch>();

            var numParts = BinaryPrimitives.ReadInt32LittleEndian(source[36..]);
            var numPoints = BinaryPrimitives.ReadInt32LittleEndian(source[40..]);
            return new MultiPatchOffsets(shapeType, numParts, numPoints);
        }
    }

    public static int GetByteSize(MultiPatch geometry)
    {
        if (geometry.ShapeType is ShapeType.Null) return 4 /* ShapeType */;

        var dimension = geometry.ShapeType.Dimension;
        return
            4 /* ShapeType */ +
            dimension.BoundingBoxSize +
            4 /* NumParts */ +
            4 /* NumPoints */ +
            geometry.Surfaces.Length * 4 /* Parts */ +
            geometry.Surfaces.Length * 4 /* PartTypes */ +
            geometry.Surfaces.Sum(x => x.Length) * dimension.PointByteSize /* Points */;
    }

    public static void Serialize(Span<byte> target, MultiPatch multiPatch)
    {
        var offsets = new MultiPatchOffsets(multiPatch);
        ShapeTypeSerializer.Serialize(target, offsets, multiPatch);
        BoundingBoxSerializer.Serialize(target, offsets, multiPatch);
        if (multiPatch.ShapeType is ShapeType.Null) return;

        for (var i = 0; i < multiPatch.Surfaces.Length; i++)
            BinaryPrimitives.WriteInt32LittleEndian(target[(offsets.PartTypes + i * 4)..], (int)multiPatch.Surfaces[i].Type);

        PartArraySerializer.Serialize(target, offsets, multiPatch.ShapeType, multiPatch.Surfaces);
    }

    public static MultiPatch Deserialize(ReadOnlySpan<byte> source)
    {
        var offsets = MultiPatchOffsets.Detect(source);
        var shapeType = ShapeTypeSerializer.Deserialize(source, offsets);
        if (shapeType is ShapeType.Null) return MultiPatch.Empty;

        var numParts = BinaryPrimitives.ReadInt32LittleEndian(source[offsets.NumParts..]);
        var partTypes = new SurfaceType[numParts];
        for (var i = 0; i < numParts; i++)
        {
            partTypes[i] = (SurfaceType)BinaryPrimitives.ReadInt32LittleEndian(source[(offsets.PartTypes + i * 4)..]);
        }

        var surfaces = PartArraySerializer.Deserialize(source, offsets, (index, points) => new Surface(partTypes[index], points));
        return new MultiPatch(surfaces);
    }
}
