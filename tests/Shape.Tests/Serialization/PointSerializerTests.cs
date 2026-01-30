using Shape.Geometries;
using Shape.Serialization;

namespace Shape.Tests.Serialization;

public sealed class PointSerializerTests : SerializerTests
{
    [Fact]
    public void Deserialize_NullShape_RecordContents_IsJustShapeType()
    {
        // Spec: Null shape record contents: only shape type at byte 0. Size = 4 bytes.
        var buf = new byte[4];
        WriteI32(buf, 0, (int)ShapeType.Null);

        var p = PointSerializer.Deserialize(buf);

        Assert.Equal(Point.Empty, p);
    }

    [Fact]
    public void Deserialize_Point_RecordContents_XY_AtOffsets4And12()
    {
        // Spec Table 4: Point: type=1, X at 4, Y at 12. Size = 20 bytes.
        var buf = new byte[20];
        WriteI32(buf, 0, (int)ShapeType.Point);
        WriteF64(buf, 4, 1.25);
        WriteF64(buf, 12, -2.5);

        var p = PointSerializer.Deserialize(buf);

        Assert.Equal(ShapeType.Point, p.ShapeType);
        Assert.Equal(1.25, p.X);
        Assert.Equal(-2.5, p.Y);
        Assert.False(p.HasZ);
        Assert.False(p.HasM);
    }

    [Fact]
    public void Deserialize_PointM_RecordContents_M_AtOffset20()
    {
        // Spec Table 8: PointM: type=21, X at 4, Y at 12, M at 20. Size = 28 bytes.
        var buf = new byte[28];
        WriteI32(buf, 0, (int)ShapeType.PointM);
        WriteF64(buf, 4, 7);
        WriteF64(buf, 12, 9);
        WriteF64(buf, 20, 123.456);

        var p = PointSerializer.Deserialize(buf);

        Assert.Equal(ShapeType.PointM, p.ShapeType);
        Assert.Equal(7, p.X);
        Assert.Equal(9, p.Y);
        Assert.False(p.HasZ);
        Assert.True(p.HasM);
        Assert.Equal(123.456, p.M);
    }

    [Fact]
    public void Deserialize_PointZ_RecordContents_Z_At20_AndM_At28()
    {
        // Spec Table 12: PointZ: type=11, X at 4, Y at 12, Z at 20, M at 28. Size = 36 bytes.
        var buf = new byte[36];
        WriteI32(buf, 0, (int)ShapeType.PointZ);
        WriteF64(buf, 4, 10);
        WriteF64(buf, 12, 20);
        WriteF64(buf, 20, 30);
        WriteF64(buf, 28, 40);

        var p = PointSerializer.Deserialize(buf);

        Assert.Equal(ShapeType.PointZ, p.ShapeType);
        Assert.Equal(10, p.X);
        Assert.Equal(20, p.Y);
        Assert.True(p.HasZ);
        Assert.True(p.HasM);
        Assert.Equal(30, p.Z);
        Assert.Equal(40, p.M);
    }

    [Fact]
    public void Serialize_Point_RecordContents_WritesShapeTypeXy()
    {
        // Spec: Point record contents length is 20 bytes. X at 4, Y at 12.
        var buf = new byte[20];
        var p = new Point(1.25, -2.5);

        PointSerializer.Serialize(buf, p);

        Assert.Equal((int)ShapeType.Point, ReadI32(buf, 0));
        Assert.Equal(1.25, ReadF64(buf, 4));
        Assert.Equal(-2.5, ReadF64(buf, 12));
    }

    [Fact]
    public void Serialize_PointM_RecordContents_M_MustBeAtOffset20_PerSpec()
    {
        // Spec: PointM stores M at byte 20 (not 28). Record contents length is 28 bytes.
        var buf = new byte[28];
        var p = new Point(7, 9, 123.456);

        PointSerializer.Serialize(buf, p);

        Assert.Equal(ShapeType.PointM, (ShapeType)ReadI32(buf, 0));
        Assert.Equal(7, ReadF64(buf, 4));
        Assert.Equal(9, ReadF64(buf, 12));

        // This is the spec requirement:
        Assert.Equal(123.456, ReadF64(buf, 20));
    }

    [Fact]
    public void Serialize_PointZ_RecordContents_Z_At20_AndM_At28_PerSpec()
    {
        // Spec: PointZ stores Z at 20 and M at 28. Record contents length is 36 bytes.
        var buf = new byte[36];
        var p = new Point(10, 20, 30, 40);

        PointSerializer.Serialize(buf, p);

        Assert.Equal((int)ShapeType.PointZ, ReadI32(buf, 0));
        Assert.Equal(10, ReadF64(buf, 4));
        Assert.Equal(20, ReadF64(buf, 12));
        Assert.Equal(30, ReadF64(buf, 20));
        Assert.Equal(40, ReadF64(buf, 28));
    }
}
