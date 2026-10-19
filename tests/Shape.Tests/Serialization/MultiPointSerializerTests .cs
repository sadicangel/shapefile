using System.Collections.Immutable;
using Shape.Geometries;
using Shape.Serialization;

namespace Shape.Tests.Serialization;

public sealed class MultiPointSerializerTests : SerializerTests
{
    [Fact]
    public void Deserialize_Null_ReturnsEmpty()
    {
        var buf = new byte[4];
        WriteI32(buf, 0, (int)ShapeType.Null);

        var mp = MultiPointSerializer.Deserialize(buf);

        Assert.Equal(MultiPoint.Empty, mp);
    }

    [Fact]
    public void Deserialize_MultiPoint_ReadsBoxCountAndXYPoints_PerSpec()
    {
        // 3 points: (0,0), (10,10), (-5,2)
        // Box: Xmin=-5, Ymin=0, Xmax=10, Ymax=10
        //
        // Size = 4 (type) + 32 (box) + 4 (numPoints) + 3*16 (XY) = 88
        var buf = new byte[88];

        WriteI32(buf, 0, (int)ShapeType.MultiPoint);

        // Box
        WriteF64(buf, 4, -5); // Xmin
        WriteF64(buf, 12, 0); // Ymin
        WriteF64(buf, 20, 10); // Xmax
        WriteF64(buf, 28, 10); // Ymax

        WriteI32(buf, 36, 3); // NumPoints

        var pointsOffset = 40;

        WriteXY(0, 0, 0);
        WriteXY(1, 10, 10);
        WriteXY(2, -5, 2);

        var mp = MultiPointSerializer.Deserialize(buf);

        Assert.Equal(3, mp.Points.Length);
        AssertPoint(mp.Points[0], 0, 0, NumericLimits.NoValue, NumericLimits.NoValue);
        AssertPoint(mp.Points[1], 10, 10, NumericLimits.NoValue, NumericLimits.NoValue);
        AssertPoint(mp.Points[2], -5, 2, NumericLimits.NoValue, NumericLimits.NoValue);

        void WriteXY(int index, double x, double y)
        {
            WriteF64(buf, pointsOffset + index * 16 + 0, x);
            WriteF64(buf, pointsOffset + index * 16 + 8, y);
        }
    }

    [Fact]
    public void Deserialize_MultiPointM_ReadsMSectionAfterXY_PerSpec()
    {
        // Same 3 points, plus Mmin/Mmax and Marray[3]
        // Size = MultiPoint(88) + 16 + 3*8(=24) = 128
        var buf = new byte[128];

        WriteI32(buf, 0, (int)ShapeType.MultiPointM);

        // Box
        WriteF64(buf, 4, -5);
        WriteF64(buf, 12, 0);
        WriteF64(buf, 20, 10);
        WriteF64(buf, 28, 10);

        WriteI32(buf, 36, 3); // NumPoints

        var pointsOffset = 40;
        WriteXY(0, 0, 0);
        WriteXY(1, 10, 10);
        WriteXY(2, -5, 2);

        // M section begins after XY points
        var mSection = pointsOffset + 3 * 16;
        WriteF64(buf, mSection + 0, 100); // Mmin
        WriteF64(buf, mSection + 8, 102); // Mmax

        var mArray = mSection + 16;
        WriteF64(buf, mArray + 0 * 8, 100);
        WriteF64(buf, mArray + 1 * 8, 101);
        WriteF64(buf, mArray + 2 * 8, 102);

        var mp = MultiPointSerializer.Deserialize(buf);

        Assert.Equal(3, mp.Points.Length);
        AssertPoint(mp.Points[0], 0, 0, NumericLimits.NoValue, 100);
        AssertPoint(mp.Points[1], 10, 10, NumericLimits.NoValue, 101);
        AssertPoint(mp.Points[2], -5, 2, NumericLimits.NoValue, 102);

        void WriteXY(int index, double x, double y)
        {
            WriteF64(buf, pointsOffset + index * 16 + 0, x);
            WriteF64(buf, pointsOffset + index * 16 + 8, y);
        }
    }

    [Fact]
    public void Deserialize_MultiPointZ_ReadsZThenMSections_PerSpec()
    {
        // Same 3 points, plus Z and M sections
        // Size = MultiPoint(88) + (16 + 3*8=24) + (16 + 24) = 88 + 40 + 40 = 168
        var buf = new byte[168];

        WriteI32(buf, 0, (int)ShapeType.MultiPointZ);

        // Box
        WriteF64(buf, 4, -5);
        WriteF64(buf, 12, 0);
        WriteF64(buf, 20, 10);
        WriteF64(buf, 28, 10);

        WriteI32(buf, 36, 3); // NumPoints

        var pointsOffset = 40;
        WriteXY(0, 0, 0);
        WriteXY(1, 10, 10);
        WriteXY(2, -5, 2);

        // Z section begins after XY points
        var zSection = pointsOffset + 3 * 16;
        WriteF64(buf, zSection + 0, 1000); // Zmin
        WriteF64(buf, zSection + 8, 1002); // Zmax

        var zArray = zSection + 16;
        WriteF64(buf, zArray + 0 * 8, 1000);
        WriteF64(buf, zArray + 1 * 8, 1001);
        WriteF64(buf, zArray + 2 * 8, 1002);

        // M section begins after Z array
        var mSection = zArray + 3 * 8;
        WriteF64(buf, mSection + 0, 2000); // Mmin
        WriteF64(buf, mSection + 8, 2002); // Mmax

        var mArray = mSection + 16;
        WriteF64(buf, mArray + 0 * 8, 2000);
        WriteF64(buf, mArray + 1 * 8, 2001);
        WriteF64(buf, mArray + 2 * 8, 2002);

        var mp = MultiPointSerializer.Deserialize(buf);

        Assert.Equal(3, mp.Points.Length);
        AssertPoint(mp.Points[0], 0, 0, 1000, 2000);
        AssertPoint(mp.Points[1], 10, 10, 1001, 2001);
        AssertPoint(mp.Points[2], -5, 2, 1002, 2002);

        void WriteXY(int index, double x, double y)
        {
            WriteF64(buf, pointsOffset + index * 16 + 0, x);
            WriteF64(buf, pointsOffset + index * 16 + 8, y);
        }
    }

    // --- Serialize layout tests (will fail until MultiPointSerializer.Serialize exists) ---

    [Fact]
    public void Serialize_MultiPoint_WritesSpecLayout_PerSpec()
    {
        var mp = MakeMultiPointXY();
        var buf = new byte[88];

        MultiPointSerializer.Serialize(buf, mp);

        Assert.Equal((int)ShapeType.MultiPoint, ReadI32(buf, 0));

        // Box should match points: (-5,0) to (10,10)
        Assert.Equal(-5, ReadF64(buf, 4));
        Assert.Equal(0, ReadF64(buf, 12));
        Assert.Equal(10, ReadF64(buf, 20));
        Assert.Equal(10, ReadF64(buf, 28));

        Assert.Equal(3, ReadI32(buf, 36)); // NumPoints

        var pointsOffset = 40;
        AssertXY(0, 0, 0);
        AssertXY(1, 10, 10);
        AssertXY(2, -5, 2);

        void AssertXY(int index, double x, double y)
        {
            Assert.Equal(x, ReadF64(buf, pointsOffset + index * 16 + 0));
            Assert.Equal(y, ReadF64(buf, pointsOffset + index * 16 + 8));
        }
    }

    [Fact]
    public void Serialize_MultiPointM_WritesMSectionAfterXY_PerSpec()
    {
        var mp = MakeMultiPointM();
        var buf = new byte[128];

        MultiPointSerializer.Serialize(buf, mp);

        Assert.Equal((int)ShapeType.MultiPointM, ReadI32(buf, 0));
        Assert.Equal(3, ReadI32(buf, 36));

        var pointsOffset = 40;
        var mSection = pointsOffset + 3 * 16;

        Assert.Equal(100, ReadF64(buf, mSection + 0)); // Mmin
        Assert.Equal(102, ReadF64(buf, mSection + 8)); // Mmax

        var mArray = mSection + 16;
        Assert.Equal(100, ReadF64(buf, mArray + 0 * 8));
        Assert.Equal(101, ReadF64(buf, mArray + 1 * 8));
        Assert.Equal(102, ReadF64(buf, mArray + 2 * 8));
    }

    [Fact]
    public void Serialize_MultiPointZ_WritesZThenMSections_PerSpec()
    {
        var mp = MakeMultiPointZ();
        var buf = new byte[168];

        MultiPointSerializer.Serialize(buf, mp);

        Assert.Equal((int)ShapeType.MultiPointZ, ReadI32(buf, 0));
        Assert.Equal(3, ReadI32(buf, 36));

        var pointsOffset = 40;

        var zSection = pointsOffset + 3 * 16;
        Assert.Equal(1000, ReadF64(buf, zSection + 0)); // Zmin
        Assert.Equal(1002, ReadF64(buf, zSection + 8)); // Zmax

        var zArray = zSection + 16;
        Assert.Equal(1000, ReadF64(buf, zArray + 0 * 8));
        Assert.Equal(1002, ReadF64(buf, zArray + 2 * 8));

        var mSection = zArray + 3 * 8;
        Assert.Equal(2000, ReadF64(buf, mSection + 0)); // Mmin
        Assert.Equal(2002, ReadF64(buf, mSection + 8)); // Mmax

        var mArray = mSection + 16;
        Assert.Equal(2000, ReadF64(buf, mArray + 0 * 8));
        Assert.Equal(2002, ReadF64(buf, mArray + 2 * 8));
    }

    // ----- model builders (adapt to your actual constructors/properties) -----

    private static MultiPoint MakeMultiPointXY()
        => new(
            ImmutableArray.Create(
                new Point(0, 0),
                new Point(10, 10),
                new Point(-5, 2)));

    private static MultiPoint MakeMultiPointM()
        => new(
            ImmutableArray.Create(
                new Point(0, 0, NumericLimits.NoValue, 100),
                new Point(10, 10, NumericLimits.NoValue, 101),
                new Point(-5, 2, NumericLimits.NoValue, 102)));

    private static MultiPoint MakeMultiPointZ()
        => new(
            ImmutableArray.Create(
                new Point(0, 0, 1000, 2000),
                new Point(10, 10, 1001, 2001),
                new Point(-5, 2, 1002, 2002)));

    private static void AssertPoint(Point p, double x, double y, double z, double m)
    {
        Assert.Equal(x, p.X);
        Assert.Equal(y, p.Y);
        Assert.Equal(z, p.Z);
        Assert.Equal(m, p.M);
    }
}
