using System.Buffers.Binary;
using System.Collections.Immutable;
using Shape.Geometries;
using Shape.Serialization;

namespace Shape.Tests.Serialization;

public sealed class PolyLineSerializerTests : SerializerTests
{
    [Fact]
    public void Deserialize_Null_ReturnsEmpty()
    {
        var buf = new byte[4];
        WriteI32(buf, 0, (int)ShapeType.Null);

        var pl = PolyLineSerializer.Deserialize(buf);

        Assert.Equal(PolyLine.Empty, pl);
    }

    [Fact]
    public void Deserialize_PolyLine_TwoParts_ReadsPartsAndPoints_PerSpec()
    {
        // Parts:
        //  - Part 0: (0,0), (1,1), (2,0)
        //  - Part 1: (10,10), (11,11)
        // NumParts=2, NumPoints=5, Parts=[0,3]
        //
        // Record contents size (bytes):
        // 4 (type) + 32 (box) + 4 + 4 + (2*4) + (5*16) = 132
        var buf = new byte[132];

        // Header
        WriteI32(buf, 0, (int)ShapeType.PolyLine);

        // Box (Xmin, Ymin, Xmax, Ymax)
        WriteF64(buf, 4, 0); // Xmin
        WriteF64(buf, 12, 0); // Ymin
        WriteF64(buf, 20, 11); // Xmax
        WriteF64(buf, 28, 11); // Ymax

        // NumParts, NumPoints
        WriteI32(buf, 36, 2);
        WriteI32(buf, 40, 5);

        // Parts array @ 44
        WriteI32(buf, 44, 0);
        WriteI32(buf, 48, 3);

        // Points array starts at 44 + NumParts*4 = 52
        // Each point is 16 bytes: X(double), Y(double)
        var pointsOffset = 52;

        // p0
        WriteF64(buf, pointsOffset + 0 * 16 + 0, 0);
        WriteF64(buf, pointsOffset + 0 * 16 + 8, 0);

        // p1
        WriteF64(buf, pointsOffset + 1 * 16 + 0, 1);
        WriteF64(buf, pointsOffset + 1 * 16 + 8, 1);

        // p2
        WriteF64(buf, pointsOffset + 2 * 16 + 0, 2);
        WriteF64(buf, pointsOffset + 2 * 16 + 8, 0);

        // p3
        WriteF64(buf, pointsOffset + 3 * 16 + 0, 10);
        WriteF64(buf, pointsOffset + 3 * 16 + 8, 10);

        // p4
        WriteF64(buf, pointsOffset + 4 * 16 + 0, 11);
        WriteF64(buf, pointsOffset + 4 * 16 + 8, 11);

        var pl = PolyLineSerializer.Deserialize(buf);

        Assert.Equal(2, pl.Lines.Length);

        var ls0 = pl.Lines[0];
        Assert.Equal(3, ls0.Points.Length);
        AssertPoint(ls0.Points[0], 0, 0, NumericLimits.NoValue, NumericLimits.NoValue);
        AssertPoint(ls0.Points[1], 1, 1, NumericLimits.NoValue, NumericLimits.NoValue);
        AssertPoint(ls0.Points[2], 2, 0, NumericLimits.NoValue, NumericLimits.NoValue);

        var ls1 = pl.Lines[1];
        Assert.Equal(2, ls1.Points.Length);
        AssertPoint(ls1.Points[0], 10, 10, NumericLimits.NoValue, NumericLimits.NoValue);
        AssertPoint(ls1.Points[1], 11, 11, NumericLimits.NoValue, NumericLimits.NoValue);
    }

    [Fact]
    public void Deserialize_PolyLineM_ReadsMArray_PerSpec()
    {
        // Same geometry as above, plus Mmin/Mmax + Marray[5].
        // Size = PolyLine(132) + 16 + (5*8)=40 => 188
        var buf = new byte[188];

        WriteI32(buf, 0, (int)ShapeType.PolyLineM);

        // Box
        WriteF64(buf, 4, 0);
        WriteF64(buf, 12, 0);
        WriteF64(buf, 20, 11);
        WriteF64(buf, 28, 11);

        WriteI32(buf, 36, 2); // NumParts
        WriteI32(buf, 40, 5); // NumPoints

        WriteI32(buf, 44, 0);
        WriteI32(buf, 48, 3);

        var pointsOffset = 52;
        WriteXY(pointsOffset, 0, 0, 0);
        WriteXY(pointsOffset, 1, 1, 1);
        WriteXY(pointsOffset, 2, 2, 0);
        WriteXY(pointsOffset, 3, 10, 10);
        WriteXY(pointsOffset, 4, 11, 11);

        // M section starts immediately after XY points:
        // mMin/mMax at (pointsOffset + NumPoints*16)
        var mSection = pointsOffset + 5 * 16;
        WriteF64(buf, mSection + 0, 100); // Mmin
        WriteF64(buf, mSection + 8, 104); // Mmax

        // M array follows
        var mArray = mSection + 16;
        WriteF64(buf, mArray + 0 * 8, 100);
        WriteF64(buf, mArray + 1 * 8, 101);
        WriteF64(buf, mArray + 2 * 8, 102);
        WriteF64(buf, mArray + 3 * 8, 103);
        WriteF64(buf, mArray + 4 * 8, 104);

        var pl = PolyLineSerializer.Deserialize(buf);

        var ls0 = pl.Lines[0];
        AssertPoint(ls0.Points[0], 0, 0, NumericLimits.NoValue, 100);
        AssertPoint(ls0.Points[1], 1, 1, NumericLimits.NoValue, 101);
        AssertPoint(ls0.Points[2], 2, 0, NumericLimits.NoValue, 102);

        var ls1 = pl.Lines[1];
        AssertPoint(ls1.Points[0], 10, 10, NumericLimits.NoValue, 103);
        AssertPoint(ls1.Points[1], 11, 11, NumericLimits.NoValue, 104);

        void WriteXY(int baseOffset, int index, double x, double y)
        {
            WriteF64(buf, baseOffset + index * 16 + 0, x);
            WriteF64(buf, baseOffset + index * 16 + 8, y);
        }
    }

    [Fact]
    public void Deserialize_PolyLineZ_ReadsZAndMArrays_PerSpec()
    {
        // Same geometry as above, plus:
        // Zmin/Zmax + Zarray[5] + Mmin/Mmax + Marray[5]
        // Size = PolyLine(132) + (16 + 40) + (16 + 40) = 244
        var buf = new byte[244];

        WriteI32(buf, 0, (int)ShapeType.PolyLineZ);

        // Box
        WriteF64(buf, 4, 0);
        WriteF64(buf, 12, 0);
        WriteF64(buf, 20, 11);
        WriteF64(buf, 28, 11);

        WriteI32(buf, 36, 2); // NumParts
        WriteI32(buf, 40, 5); // NumPoints

        WriteI32(buf, 44, 0);
        WriteI32(buf, 48, 3);

        var partsOffset = 44;
        var pointsOffset = partsOffset + 2 * 4; // 52

        WriteXY(0, 0, 0);
        WriteXY(1, 1, 1);
        WriteXY(2, 2, 0);
        WriteXY(3, 10, 10);
        WriteXY(4, 11, 11);

        // Z section begins after XY points
        var zSection = pointsOffset + 5 * 16;
        WriteF64(buf, zSection + 0, 1000); // Zmin
        WriteF64(buf, zSection + 8, 1004); // Zmax

        var zArray = zSection + 16;
        WriteF64(buf, zArray + 0 * 8, 1000);
        WriteF64(buf, zArray + 1 * 8, 1001);
        WriteF64(buf, zArray + 2 * 8, 1002);
        WriteF64(buf, zArray + 3 * 8, 1003);
        WriteF64(buf, zArray + 4 * 8, 1004);

        // M section begins after Z array
        var mSection = zArray + 5 * 8;
        WriteF64(buf, mSection + 0, 2000); // Mmin
        WriteF64(buf, mSection + 8, 2004); // Mmax

        var mArray = mSection + 16;
        WriteF64(buf, mArray + 0 * 8, 2000);
        WriteF64(buf, mArray + 1 * 8, 2001);
        WriteF64(buf, mArray + 2 * 8, 2002);
        WriteF64(buf, mArray + 3 * 8, 2003);
        WriteF64(buf, mArray + 4 * 8, 2004);

        var pl = PolyLineSerializer.Deserialize(buf);

        var ls0 = pl.Lines[0];
        AssertPoint(ls0.Points[0], 0, 0, 1000, 2000);
        AssertPoint(ls0.Points[1], 1, 1, 1001, 2001);
        AssertPoint(ls0.Points[2], 2, 0, 1002, 2002);

        var ls1 = pl.Lines[1];
        AssertPoint(ls1.Points[0], 10, 10, 1003, 2003);
        AssertPoint(ls1.Points[1], 11, 11, 1004, 2004);

        void WriteXY(int index, double x, double y)
        {
            WriteF64(buf, pointsOffset + index * 16 + 0, x);
            WriteF64(buf, pointsOffset + index * 16 + 8, y);
        }
    }

    // --- Serialize layout tests (will fail until you implement Serialize) ---

    [Fact]
    public void Serialize_PolyLine_WritesSpecLayout_TwoParts()
    {
        var pl = MakeTwoPartPolyLineXY();
        var buf = new byte[132];


        PolyLineSerializer.Serialize(buf, pl);

        Assert.Equal((int)ShapeType.PolyLine, ReadI32(buf, 0));

        // Box
        Assert.Equal(0, ReadF64(buf, 4)); // Xmin
        Assert.Equal(0, ReadF64(buf, 12)); // Ymin
        Assert.Equal(11, ReadF64(buf, 20)); // Xmax
        Assert.Equal(11, ReadF64(buf, 28)); // Ymax

        Assert.Equal(2, ReadI32(buf, 36)); // NumParts
        Assert.Equal(5, ReadI32(buf, 40)); // NumPoints

        // Parts array
        Assert.Equal(0, ReadI32(buf, 44));
        Assert.Equal(3, ReadI32(buf, 48));

        // XY array
        var pointsOffset = 52;
        AssertXY(buf, pointsOffset, 0, 0, 0);
        AssertXY(buf, pointsOffset, 1, 1, 1);
        AssertXY(buf, pointsOffset, 2, 2, 0);
        AssertXY(buf, pointsOffset, 3, 10, 10);
        AssertXY(buf, pointsOffset, 4, 11, 11);

        static void AssertXY(byte[] b, int baseOffset, int index, double x, double y)
        {
            Assert.Equal(x, BinaryPrimitives.ReadDoubleLittleEndian(b.AsSpan(baseOffset + index * 16 + 0, 8)));
            Assert.Equal(y, BinaryPrimitives.ReadDoubleLittleEndian(b.AsSpan(baseOffset + index * 16 + 8, 8)));
        }
    }

    [Fact]
    public void Serialize_PolyLineM_WritesMSectionAfterXY_PerSpec()
    {
        var pl = MakeTwoPartPolyLineM();
        var buf = new byte[188];

        PolyLineSerializer.Serialize(buf, pl);

        Assert.Equal((int)ShapeType.PolyLineM, ReadI32(buf, 0));
        Assert.Equal(2, ReadI32(buf, 36));
        Assert.Equal(5, ReadI32(buf, 40));

        var pointsOffset = 52;
        var mSection = pointsOffset + 5 * 16;

        // Mmin/Mmax
        Assert.Equal(100, ReadF64(buf, mSection + 0));
        Assert.Equal(104, ReadF64(buf, mSection + 8));

        // M array
        var mArray = mSection + 16;
        Assert.Equal(100, ReadF64(buf, mArray + 0 * 8));
        Assert.Equal(101, ReadF64(buf, mArray + 1 * 8));
        Assert.Equal(102, ReadF64(buf, mArray + 2 * 8));
        Assert.Equal(103, ReadF64(buf, mArray + 3 * 8));
        Assert.Equal(104, ReadF64(buf, mArray + 4 * 8));
    }

    [Fact]
    public void Serialize_PolyLineZ_WritesZThenMSections_PerSpec()
    {
        var pl = MakeTwoPartPolyLineZ();
        var buf = new byte[244];

        PolyLineSerializer.Serialize(buf, pl);

        Assert.Equal((int)ShapeType.PolyLineZ, ReadI32(buf, 0));
        Assert.Equal(2, ReadI32(buf, 36));
        Assert.Equal(5, ReadI32(buf, 40));

        var pointsOffset = 52;

        var zSection = pointsOffset + 5 * 16;
        Assert.Equal(1000, ReadF64(buf, zSection + 0)); // Zmin
        Assert.Equal(1004, ReadF64(buf, zSection + 8)); // Zmax

        var zArray = zSection + 16;
        Assert.Equal(1000, ReadF64(buf, zArray + 0 * 8));
        Assert.Equal(1004, ReadF64(buf, zArray + 4 * 8));

        var mSection = zArray + 5 * 8;
        Assert.Equal(2000, ReadF64(buf, mSection + 0)); // Mmin
        Assert.Equal(2004, ReadF64(buf, mSection + 8)); // Mmax

        var mArray = mSection + 16;
        Assert.Equal(2000, ReadF64(buf, mArray + 0 * 8));
        Assert.Equal(2004, ReadF64(buf, mArray + 4 * 8));
    }

    // ----- helpers for expected model objects -----

    private static PolyLine MakeTwoPartPolyLineXY()
    {
        var ls0 = new LineString(
            ImmutableArray.Create(
                new Point(0, 0),
                new Point(1, 1),
                new Point(2, 0)));

        var ls1 = new LineString(
            ImmutableArray.Create(
                new Point(10, 10),
                new Point(11, 11)));

        return new PolyLine(ImmutableArray.Create(ls0, ls1));
    }

    private static PolyLine MakeTwoPartPolyLineM()
    {
        var ls0 = new LineString(
            ImmutableArray.Create(
                new Point(0, 0, NumericLimits.NoValue, 100),
                new Point(1, 1, NumericLimits.NoValue, 101),
                new Point(2, 0, NumericLimits.NoValue, 102)));

        var ls1 = new LineString(
            ImmutableArray.Create(
                new Point(10, 10, NumericLimits.NoValue, 103),
                new Point(11, 11, NumericLimits.NoValue, 104)));

        return new PolyLine(ImmutableArray.Create(ls0, ls1));
    }

    private static PolyLine MakeTwoPartPolyLineZ()
    {
        var ls0 = new LineString(
            ImmutableArray.Create(
                new Point(0, 0, 1000, 2000),
                new Point(1, 1, 1001, 2001),
                new Point(2, 0, 1002, 2002)));

        var ls1 = new LineString(
            ImmutableArray.Create(
                new Point(10, 10, 1003, 2003),
                new Point(11, 11, 1004, 2004)));

        return new PolyLine(ImmutableArray.Create(ls0, ls1));
    }

    private static void AssertPoint(Point p, double x, double y, double z, double m)
    {
        Assert.Equal(x, p.X);
        Assert.Equal(y, p.Y);
        Assert.Equal(z, p.Z);
        Assert.Equal(m, p.M);
    }
}
