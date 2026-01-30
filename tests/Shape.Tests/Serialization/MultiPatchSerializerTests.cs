using System.Collections.Immutable;
using Shape.Geometries;
using Shape.Serialization;

namespace Shape.Tests.Serialization;

public sealed class MultiPatchSerializerTests : SerializerTests
{
    // Test geometry:
    // - Surface 0: TriangleStrip (0), 4 points
    // - Surface 1: FirstRing (4), 5 points (closed)
    //
    // NumParts = 2
    // NumPoints = 9
    // Parts = [0, 4]
    // PartTypes = [0, 4]

    [Fact]
    public void Deserialize_Null_ReturnsEmpty()
    {
        var buf = new byte[4];
        WriteI32(buf, 0, (int)ShapeType.Null);

        var mp = MultiPatchSerializer.Deserialize(buf);

        Assert.Equal(MultiPatch.Empty, mp);
    }

    [Fact]
    public void Deserialize_MultiPatch_AllMNoValue_ReadsSurfacesPointsZArrayAndMArray_PerSpec()
    {
        // MultiPatch record contents always include M range + M array (values may be NoData).
        // Size for this geometry:
        // 4 + 32 + 4 + 4 + (2*4) + (2*4) + (9*16) + 16 + (9*8) + 16 + (9*8) = 380
        var buf = new byte[380];

        WriteMultiPatchHeader(
            buf,
            shapeType: ShapeType.MultiPatch,
            xmin: 0,
            ymin: 0,
            xmax: 10,
            ymax: 10,
            numParts: 2,
            numPoints: 9);

        // Parts @ 44
        WriteI32(buf, 44, 0);
        WriteI32(buf, 48, 4);

        // PartTypes @ 52
        WriteI32(buf, 52, 0); // TriangleStrip
        WriteI32(buf, 56, 4); // FirstRing

        // Points @ 60 (XY)
        var pointsOffset = 60;

        // Surface0 strip (4 points)
        WriteXY(buf, pointsOffset, 0, 0, 0);
        WriteXY(buf, pointsOffset, 1, 1, 0);
        WriteXY(buf, pointsOffset, 2, 1, 1);
        WriteXY(buf, pointsOffset, 3, 0, 1);

        // Surface1 ring (5 points, closed)
        WriteXY(buf, pointsOffset, 4, 0, 0);
        WriteXY(buf, pointsOffset, 5, 10, 0);
        WriteXY(buf, pointsOffset, 6, 10, 10);
        WriteXY(buf, pointsOffset, 7, 0, 10);
        WriteXY(buf, pointsOffset, 8, 0, 0);

        // Z section begins at:
        // Y = X + 16*NumPoints = 60 + 16*9 = 204
        var zSection = 204;

        // Zmin/Zmax
        WriteF64(buf, zSection + 0, 1000);
        WriteF64(buf, zSection + 8, 1008);

        // Z array at zSection+16
        var zArray = zSection + 16;
        for (var i = 0; i < 9; i++)
            WriteF64(buf, zArray + i * 8, 1000 + i);

        // M section begins at:
        // Z = Y + 16 + 8*NumPoints = 204 + 16 + 72 = 292
        var mSection = 292;

        // Spec: M section exists; NoData is encoded by any value < -1e38.
        // We'll write your library's NoValue constant.
        WriteF64(buf, mSection + 0, NumericLimits.NoValue); // Mmin
        WriteF64(buf, mSection + 8, NumericLimits.NoValue); // Mmax

        var mArray = mSection + 16;
        for (var i = 0; i < 9; i++)
            WriteF64(buf, mArray + i * 8, NumericLimits.NoValue);

        var mp = MultiPatchSerializer.Deserialize(buf);

        Assert.Equal(2, mp.Surfaces.Length);

        Assert.Equal((SurfaceType)0, mp.Surfaces[0].Type);
        Assert.Equal(4, mp.Surfaces[0].Points.Length);
        AssertPoint(mp.Surfaces[0].Points[0], 0, 0, 1000, NumericLimits.NoValue);
        AssertPoint(mp.Surfaces[0].Points[3], 0, 1, 1003, NumericLimits.NoValue);

        Assert.Equal((SurfaceType)4, mp.Surfaces[1].Type);
        Assert.Equal(5, mp.Surfaces[1].Points.Length);
        AssertPoint(mp.Surfaces[1].Points[0], 0, 0, 1004, NumericLimits.NoValue);
        AssertPoint(mp.Surfaces[1].Points[4], 0, 0, 1008, NumericLimits.NoValue);
    }

    [Fact]
    public void Deserialize_MultiPatch_WithM_ReadsMRangeAndMArray_PerSpec()
    {
        // MultiPatch record contents include M range + M array.
        // Size = 380 for this geometry.
        var buf = new byte[380];

        WriteMultiPatchHeader(
            buf,
            shapeType: ShapeType.MultiPatch,
            xmin: 0,
            ymin: 0,
            xmax: 10,
            ymax: 10,
            numParts: 2,
            numPoints: 9);

        // Parts
        WriteI32(buf, 44, 0);
        WriteI32(buf, 48, 4);

        // PartTypes
        WriteI32(buf, 52, 0);
        WriteI32(buf, 56, 4);

        // XY points
        var pointsOffset = 60;

        WriteXY(buf, pointsOffset, 0, 0, 0);
        WriteXY(buf, pointsOffset, 1, 1, 0);
        WriteXY(buf, pointsOffset, 2, 1, 1);
        WriteXY(buf, pointsOffset, 3, 0, 1);

        WriteXY(buf, pointsOffset, 4, 0, 0);
        WriteXY(buf, pointsOffset, 5, 10, 0);
        WriteXY(buf, pointsOffset, 6, 10, 10);
        WriteXY(buf, pointsOffset, 7, 0, 10);
        WriteXY(buf, pointsOffset, 8, 0, 0);

        // Z section at 204
        var zSection = 204;
        WriteF64(buf, zSection + 0, 1000);
        WriteF64(buf, zSection + 8, 1008);

        var zArray = zSection + 16;
        for (var i = 0; i < 9; i++)
            WriteF64(buf, zArray + i * 8, 1000 + i);

        // M section begins at 292
        var mSection = 292;

        WriteF64(buf, mSection + 0, 2000); // Mmin
        WriteF64(buf, mSection + 8, 2008); // Mmax

        var mArray = mSection + 16;
        for (var i = 0; i < 9; i++)
            WriteF64(buf, mArray + i * 8, 2000 + i);

        var mp = MultiPatchSerializer.Deserialize(buf);

        Assert.Equal(2, mp.Surfaces.Length);

        AssertPoint(mp.Surfaces[0].Points[0], 0, 0, 1000, 2000);
        AssertPoint(mp.Surfaces[0].Points[3], 0, 1, 1003, 2003);

        AssertPoint(mp.Surfaces[1].Points[0], 0, 0, 1004, 2004);
        AssertPoint(mp.Surfaces[1].Points[4], 0, 0, 1008, 2008);
    }

    // --- Serialize layout tests (will fail until MultiPatchSerializer.Serialize is implemented) ---

    [Fact]
    public void Serialize_MultiPatch_AllMNoValue_WritesSpecLayout_ThroughMArray()
    {
        // MultiPatch must write an M section; "no M" means values are NoValue.
        var mp = MakeMultiPatch_AllMNoValue();
        var buf = new byte[380];

        MultiPatchSerializer.Serialize(buf, mp);

        Assert.Equal((int)ShapeType.MultiPatch, ReadI32(buf, 0));
        Assert.Equal(2, ReadI32(buf, 36)); // NumParts
        Assert.Equal(9, ReadI32(buf, 40)); // NumPoints

        // Parts
        Assert.Equal(0, ReadI32(buf, 44));
        Assert.Equal(4, ReadI32(buf, 48));

        // PartTypes @ 52
        Assert.Equal(0, ReadI32(buf, 52));
        Assert.Equal(4, ReadI32(buf, 56));

        // Points start @ 60
        var pointsOffset = 60;
        AssertXY(buf, pointsOffset, 0, 0, 0);
        AssertXY(buf, pointsOffset, 8, 0, 0);

        // Z section @ 204
        var zSection = 204;
        Assert.Equal(1000, ReadF64(buf, zSection + 0)); // Zmin
        Assert.Equal(1008, ReadF64(buf, zSection + 8)); // Zmax

        var zArray = zSection + 16;
        Assert.Equal(1000, ReadF64(buf, zArray + 0 * 8));
        Assert.Equal(1008, ReadF64(buf, zArray + 8 * 8));

        // M section @ 292
        var mSection = 292;

        Assert.Equal(NumericLimits.NoValue, ReadF64(buf, mSection + 0)); // Mmin
        Assert.Equal(NumericLimits.NoValue, ReadF64(buf, mSection + 8)); // Mmax

        var mArray = mSection + 16;
        Assert.Equal(NumericLimits.NoValue, ReadF64(buf, mArray + 0 * 8));
        Assert.Equal(NumericLimits.NoValue, ReadF64(buf, mArray + 8 * 8));
    }

    [Fact]
    public void Serialize_MultiPatch_WithM_WritesMRangeAndMArray()
    {
        var mp = MakeMultiPatch_WithM();
        var buf = new byte[380];

        MultiPatchSerializer.Serialize(buf, mp);

        Assert.Equal((int)ShapeType.MultiPatch, ReadI32(buf, 0));
        Assert.Equal(2, ReadI32(buf, 36));
        Assert.Equal(9, ReadI32(buf, 40));

        // M section begins @ 292 for this geometry
        var mSection = 292;
        Assert.Equal(2000, ReadF64(buf, mSection + 0)); // Mmin
        Assert.Equal(2008, ReadF64(buf, mSection + 8)); // Mmax

        var mArray = mSection + 16;
        Assert.Equal(2000, ReadF64(buf, mArray + 0 * 8));
        Assert.Equal(2008, ReadF64(buf, mArray + 8 * 8));
    }

    // ---- local helpers ----

    private static void WriteMultiPatchHeader(
        byte[] buf,
        ShapeType shapeType,
        double xmin,
        double ymin,
        double xmax,
        double ymax,
        int numParts,
        int numPoints)
    {
        WriteI32(buf, 0, (int)shapeType);
        WriteF64(buf, 4, xmin);
        WriteF64(buf, 12, ymin);
        WriteF64(buf, 20, xmax);
        WriteF64(buf, 28, ymax);
        WriteI32(buf, 36, numParts);
        WriteI32(buf, 40, numPoints);
    }

    private static void WriteXY(byte[] buf, int pointsOffset, int index, double x, double y)
    {
        WriteF64(buf, pointsOffset + index * 16 + 0, x);
        WriteF64(buf, pointsOffset + index * 16 + 8, y);
    }

    private static void AssertXY(byte[] buf, int pointsOffset, int index, double x, double y)
    {
        Assert.Equal(x, ReadF64(buf, pointsOffset + index * 16 + 0));
        Assert.Equal(y, ReadF64(buf, pointsOffset + index * 16 + 8));
    }

    private static void AssertPoint(Point p, double x, double y, double z, double m)
    {
        Assert.Equal(x, p.X);
        Assert.Equal(y, p.Y);
        Assert.Equal(z, p.Z);
        Assert.Equal(m, p.M);
    }

    private static MultiPatch MakeMultiPatch_AllMNoValue()
    {
        var s0 = new Surface(
            (SurfaceType)0,
            ImmutableArray.Create(
                new Point(0, 0, 1000, NumericLimits.NoValue),
                new Point(1, 0, 1001, NumericLimits.NoValue),
                new Point(1, 1, 1002, NumericLimits.NoValue),
                new Point(0, 1, 1003, NumericLimits.NoValue)));

        var s1 = new Surface(
            (SurfaceType)4,
            ImmutableArray.Create(
                new Point(0, 0, 1004, NumericLimits.NoValue),
                new Point(10, 0, 1005, NumericLimits.NoValue),
                new Point(10, 10, 1006, NumericLimits.NoValue),
                new Point(0, 10, 1007, NumericLimits.NoValue),
                new Point(0, 0, 1008, NumericLimits.NoValue)));

        return new MultiPatch(ImmutableArray.Create(s0, s1));
    }

    private static MultiPatch MakeMultiPatch_WithM()
    {
        var s0 = new Surface(
            (SurfaceType)0,
            ImmutableArray.Create(
                new Point(0, 0, 1000, 2000),
                new Point(1, 0, 1001, 2001),
                new Point(1, 1, 1002, 2002),
                new Point(0, 1, 1003, 2003)));

        var s1 = new Surface(
            (SurfaceType)4,
            ImmutableArray.Create(
                new Point(0, 0, 1004, 2004),
                new Point(10, 0, 1005, 2005),
                new Point(10, 10, 1006, 2006),
                new Point(0, 10, 1007, 2007),
                new Point(0, 0, 1008, 2008)));

        return new MultiPatch(ImmutableArray.Create(s0, s1));
    }
}
