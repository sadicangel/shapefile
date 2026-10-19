using System.Collections.Immutable;
using Shape.Geometries;
using Shape.Serialization;

namespace Shape.Tests.Serialization;

public sealed class PolygonSerializerTests : SerializerTests
{
    [Fact]
    public void Deserialize_Null_ReturnsEmpty()
    {
        var buf = new byte[4];
        WriteI32(buf, 0, (int)ShapeType.Null);

        var poly = PolygonSerializer.Deserialize(buf);

        Assert.Equal(Polygon.Empty, poly);
    }

    [Fact]
    public void Deserialize_Polygon_TwoRings_OuterAndHole_PerSpec()
    {
        // Outer ring: square (0,0)-(10,0)-(10,10)-(0,10)-(0,0)
        // Inner ring (hole): square (2,2)-(8,2)-(8,8)-(2,8)-(2,2)
        //
        // NumParts=2
        // NumPoints=10 (5 + 5)
        // Parts=[0,5]
        //
        // Record contents size (Polygon XY):
        // 4 + 32 + 4 + 4 + (2*4) + (10*16) = 4+32+4+4+8+160 = 212
        var buf = new byte[212];

        WriteI32(buf, 0, (int)ShapeType.Polygon);

        // Box
        WriteF64(buf, 4, 0); // Xmin
        WriteF64(buf, 12, 0); // Ymin
        WriteF64(buf, 20, 10); // Xmax
        WriteF64(buf, 28, 10); // Ymax

        WriteI32(buf, 36, 2); // NumParts
        WriteI32(buf, 40, 10); // NumPoints

        // Parts @ 44
        WriteI32(buf, 44, 0); // outer start
        WriteI32(buf, 48, 5); // hole start

        var pointsOffset = 52;

        // Outer ring points (5)
        WriteXY(0, 0, 0);
        WriteXY(1, 10, 0);
        WriteXY(2, 10, 10);
        WriteXY(3, 0, 10);
        WriteXY(4, 0, 0);

        // Hole ring points (5)
        WriteXY(5, 2, 2);
        WriteXY(6, 8, 2);
        WriteXY(7, 8, 8);
        WriteXY(8, 2, 8);
        WriteXY(9, 2, 2);

        var poly = PolygonSerializer.Deserialize(buf);

        Assert.Equal(2, poly.Rings.Length);

        var outer = poly.Rings[0];
        Assert.Equal(5, outer.Points.Length);
        AssertPoint(outer.Points[0], 0, 0, NumericLimits.NoValue, NumericLimits.NoValue);
        AssertPoint(outer.Points[4], 0, 0, NumericLimits.NoValue, NumericLimits.NoValue);

        var hole = poly.Rings[1];
        Assert.Equal(5, hole.Points.Length);
        AssertPoint(hole.Points[0], 2, 2, NumericLimits.NoValue, NumericLimits.NoValue);
        AssertPoint(hole.Points[4], 2, 2, NumericLimits.NoValue, NumericLimits.NoValue);

        void WriteXY(int index, double x, double y)
        {
            WriteF64(buf, pointsOffset + index * 16 + 0, x);
            WriteF64(buf, pointsOffset + index * 16 + 8, y);
        }
    }

    [Fact]
    public void Deserialize_PolygonM_ReadsMArray_PerSpec()
    {
        // Same rings as above, plus Mmin/Mmax + Marray[10].
        // Size = PolygonXY(212) + 16 + (10*8)=80 => 308
        var buf = new byte[308];

        WriteI32(buf, 0, (int)ShapeType.PolygonM);

        // Box
        WriteF64(buf, 4, 0);
        WriteF64(buf, 12, 0);
        WriteF64(buf, 20, 10);
        WriteF64(buf, 28, 10);

        WriteI32(buf, 36, 2); // NumParts
        WriteI32(buf, 40, 10); // NumPoints

        WriteI32(buf, 44, 0);
        WriteI32(buf, 48, 5);

        var pointsOffset = 52;

        // Outer ring
        WriteXY(0, 0, 0);
        WriteXY(1, 10, 0);
        WriteXY(2, 10, 10);
        WriteXY(3, 0, 10);
        WriteXY(4, 0, 0);

        // Hole ring
        WriteXY(5, 2, 2);
        WriteXY(6, 8, 2);
        WriteXY(7, 8, 8);
        WriteXY(8, 2, 8);
        WriteXY(9, 2, 2);

        // M section after XY points
        var mSection = pointsOffset + 10 * 16;
        WriteF64(buf, mSection + 0, 100); // Mmin
        WriteF64(buf, mSection + 8, 109); // Mmax

        var mArray = mSection + 16;
        for (var i = 0; i < 10; i++)
            WriteF64(buf, mArray + i * 8, 100 + i);

        var poly = PolygonSerializer.Deserialize(buf);

        // Spot-check Ms on a few points
        Assert.Equal(100, poly.Rings[0].Points[0].M);
        Assert.Equal(104, poly.Rings[0].Points[4].M);
        Assert.Equal(105, poly.Rings[1].Points[0].M);
        Assert.Equal(109, poly.Rings[1].Points[4].M);

        void WriteXY(int index, double x, double y)
        {
            WriteF64(buf, pointsOffset + index * 16 + 0, x);
            WriteF64(buf, pointsOffset + index * 16 + 8, y);
        }
    }

    [Fact]
    public void Deserialize_PolygonZ_ReadsZThenMSections_PerSpec()
    {
        // Same rings as above, plus:
        // Zmin/Zmax + Zarray[10] + Mmin/Mmax + Marray[10]
        // Size = PolygonXY(212) + (16 + 10*8=80) + (16 + 10*8=80) = 212 + 96 + 96 = 404
        var buf = new byte[404];

        WriteI32(buf, 0, (int)ShapeType.PolygonZ);

        // Box
        WriteF64(buf, 4, 0);
        WriteF64(buf, 12, 0);
        WriteF64(buf, 20, 10);
        WriteF64(buf, 28, 10);

        WriteI32(buf, 36, 2);
        WriteI32(buf, 40, 10);

        WriteI32(buf, 44, 0);
        WriteI32(buf, 48, 5);

        var pointsOffset = 52;

        // XY points (10)
        WriteXY(0, 0, 0);
        WriteXY(1, 10, 0);
        WriteXY(2, 10, 10);
        WriteXY(3, 0, 10);
        WriteXY(4, 0, 0);

        WriteXY(5, 2, 2);
        WriteXY(6, 8, 2);
        WriteXY(7, 8, 8);
        WriteXY(8, 2, 8);
        WriteXY(9, 2, 2);

        // Z section
        var zSection = pointsOffset + 10 * 16;
        WriteF64(buf, zSection + 0, 1000); // Zmin
        WriteF64(buf, zSection + 8, 1009); // Zmax

        var zArray = zSection + 16;
        for (var i = 0; i < 10; i++)
            WriteF64(buf, zArray + i * 8, 1000 + i);

        // M section
        var mSection = zArray + 10 * 8;
        WriteF64(buf, mSection + 0, 2000); // Mmin
        WriteF64(buf, mSection + 8, 2009); // Mmax

        var mArray = mSection + 16;
        for (var i = 0; i < 10; i++)
            WriteF64(buf, mArray + i * 8, 2000 + i);

        var poly = PolygonSerializer.Deserialize(buf);

        // Spot-check Z/M at a few indices across rings
        AssertPoint(poly.Rings[0].Points[0], 0, 0, 1000, 2000);
        AssertPoint(poly.Rings[0].Points[4], 0, 0, 1004, 2004);
        AssertPoint(poly.Rings[1].Points[0], 2, 2, 1005, 2005);
        AssertPoint(poly.Rings[1].Points[4], 2, 2, 1009, 2009);

        void WriteXY(int index, double x, double y)
        {
            WriteF64(buf, pointsOffset + index * 16 + 0, x);
            WriteF64(buf, pointsOffset + index * 16 + 8, y);
        }
    }

    // --- Serialize layout tests (will fail until you implement PolygonSerializer.Serialize) ---

    [Fact]
    public void Serialize_Polygon_WritesSpecLayout_OuterAndHole()
    {
        var poly = MakeOuterAndHoleXY();
        var buf = new byte[212];

        PolygonSerializer.Serialize(buf, poly);

        Assert.Equal((int)ShapeType.Polygon, ReadI32(buf, 0));

        // Box
        Assert.Equal(0, ReadF64(buf, 4));
        Assert.Equal(0, ReadF64(buf, 12));
        Assert.Equal(10, ReadF64(buf, 20));
        Assert.Equal(10, ReadF64(buf, 28));

        Assert.Equal(2, ReadI32(buf, 36)); // NumParts
        Assert.Equal(10, ReadI32(buf, 40)); // NumPoints

        Assert.Equal(0, ReadI32(buf, 44));
        Assert.Equal(5, ReadI32(buf, 48));

        var pointsOffset = 52;

        AssertXY(0, 0, 0);
        AssertXY(1, 10, 0);
        AssertXY(2, 10, 10);
        AssertXY(3, 0, 10);
        AssertXY(4, 0, 0);

        AssertXY(5, 2, 2);
        AssertXY(6, 8, 2);
        AssertXY(7, 8, 8);
        AssertXY(8, 2, 8);
        AssertXY(9, 2, 2);

        void AssertXY(int index, double x, double y)
        {
            Assert.Equal(x, ReadF64(buf, pointsOffset + index * 16 + 0));
            Assert.Equal(y, ReadF64(buf, pointsOffset + index * 16 + 8));
        }
    }

    [Fact]
    public void Serialize_PolygonM_WritesMSectionAfterXY_PerSpec()
    {
        var poly = MakeOuterAndHoleM();
        var buf = new byte[308];

        PolygonSerializer.Serialize(buf, poly);

        Assert.Equal((int)ShapeType.PolygonM, ReadI32(buf, 0));
        Assert.Equal(2, ReadI32(buf, 36));
        Assert.Equal(10, ReadI32(buf, 40));

        var pointsOffset = 52;
        var mSection = pointsOffset + 10 * 16;

        Assert.Equal(100, ReadF64(buf, mSection + 0)); // Mmin
        Assert.Equal(109, ReadF64(buf, mSection + 8)); // Mmax

        var mArray = mSection + 16;
        Assert.Equal(100, ReadF64(buf, mArray + 0 * 8));
        Assert.Equal(109, ReadF64(buf, mArray + 9 * 8));
    }

    [Fact]
    public void Serialize_PolygonZ_WritesZThenMSections_PerSpec()
    {
        var poly = MakeOuterAndHoleZ();
        var buf = new byte[404];

        PolygonSerializer.Serialize(buf, poly);

        Assert.Equal((int)ShapeType.PolygonZ, ReadI32(buf, 0));
        Assert.Equal(2, ReadI32(buf, 36));
        Assert.Equal(10, ReadI32(buf, 40));

        var pointsOffset = 52;

        var zSection = pointsOffset + 10 * 16;
        Assert.Equal(1000, ReadF64(buf, zSection + 0)); // Zmin
        Assert.Equal(1009, ReadF64(buf, zSection + 8)); // Zmax

        var zArray = zSection + 16;
        Assert.Equal(1000, ReadF64(buf, zArray + 0 * 8));
        Assert.Equal(1009, ReadF64(buf, zArray + 9 * 8));

        var mSection = zArray + 10 * 8;
        Assert.Equal(2000, ReadF64(buf, mSection + 0)); // Mmin
        Assert.Equal(2009, ReadF64(buf, mSection + 8)); // Mmax

        var mArray = mSection + 16;
        Assert.Equal(2000, ReadF64(buf, mArray + 0 * 8));
        Assert.Equal(2009, ReadF64(buf, mArray + 9 * 8));
    }

    // ----- model builders (adapt names/types to your actual geometry model) -----

    private static Polygon MakeOuterAndHoleXY()
    {
        var outer = new LinearRing(
            ImmutableArray.Create(
                new Point(0, 0),
                new Point(10, 0),
                new Point(10, 10),
                new Point(0, 10),
                new Point(0, 0)));

        var hole = new LinearRing(
            ImmutableArray.Create(
                new Point(2, 2),
                new Point(8, 2),
                new Point(8, 8),
                new Point(2, 8),
                new Point(2, 2)));

        return new Polygon(ImmutableArray.Create(outer, hole));
    }

    private static Polygon MakeOuterAndHoleM()
    {
        // M values 100..109 in point order
        var outer = new LinearRing(
            ImmutableArray.Create(
                new Point(0, 0, NumericLimits.NoValue, 100),
                new Point(10, 0, NumericLimits.NoValue, 101),
                new Point(10, 10, NumericLimits.NoValue, 102),
                new Point(0, 10, NumericLimits.NoValue, 103),
                new Point(0, 0, NumericLimits.NoValue, 104)));

        var hole = new LinearRing(
            ImmutableArray.Create(
                new Point(2, 2, NumericLimits.NoValue, 105),
                new Point(8, 2, NumericLimits.NoValue, 106),
                new Point(8, 8, NumericLimits.NoValue, 107),
                new Point(2, 8, NumericLimits.NoValue, 108),
                new Point(2, 2, NumericLimits.NoValue, 109)));

        return new Polygon(ImmutableArray.Create(outer, hole));
    }

    private static Polygon MakeOuterAndHoleZ()
    {
        // Z values 1000..1009, M values 2000..2009 in point order
        var outer = new LinearRing(
            ImmutableArray.Create(
                new Point(0, 0, 1000, 2000),
                new Point(10, 0, 1001, 2001),
                new Point(10, 10, 1002, 2002),
                new Point(0, 10, 1003, 2003),
                new Point(0, 0, 1004, 2004)));

        var hole = new LinearRing(
            ImmutableArray.Create(
                new Point(2, 2, 1005, 2005),
                new Point(8, 2, 1006, 2006),
                new Point(8, 8, 1007, 2007),
                new Point(2, 8, 1008, 2008),
                new Point(2, 2, 1009, 2009)));

        return new Polygon(ImmutableArray.Create(outer, hole));
    }

    private static void AssertPoint(Point p, double x, double y, double z, double m)
    {
        Assert.Equal(x, p.X);
        Assert.Equal(y, p.Y);
        Assert.Equal(z, p.Z);
        Assert.Equal(m, p.M);
    }
}
