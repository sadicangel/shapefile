
using System.Buffers.Binary;

namespace Shape.Geometries;

public abstract record Geometry
{
    internal const double MinValue = -10e38;
    internal const double NoValue = -101e37;

    public abstract BoundingBox GetBoundingBox();

    public static T Read<T>(ReadOnlySpan<byte> source) where T : Geometry, IGeometry<T> => T.Read(source);

    public static Geometry Read(ReadOnlySpan<byte> source, ShapeType expectedShapeType)
    {
        var actualShapeType = (ShapeType)BinaryPrimitives.ReadInt32LittleEndian(source);

        if (actualShapeType is not ShapeType.Null && actualShapeType != expectedShapeType)
        {
            throw new InvalidOperationException($"Shape type mismatch. Expected: {expectedShapeType}, Actual: {actualShapeType}");
        }

        return expectedShapeType switch
        {
            ShapeType.Point or ShapeType.PointZ or ShapeType.PointM => Read<Point>(source),
            ShapeType.PolyLine or ShapeType.PolyLineZ or ShapeType.PolyLineM => Read<PolyLine>(source),
            ShapeType.Polygon or ShapeType.PolygonZ or ShapeType.PolygonM => Read<Polygon>(source),
            ShapeType.MultiPoint or ShapeType.MultiPointZ or ShapeType.MultiPointM => Read<MultiPoint>(source),
            ShapeType.MultiPatch => Read<MultiPatch>(source),
            _ => throw new InvalidOperationException($"Unknown shape type: {expectedShapeType}"),
        };
    }
}
