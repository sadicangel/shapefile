using Shape.Geometries;

namespace Shape;

internal static class ShapeTypeExtensions
{
    public static bool HasZ(this ShapeType shapeType) => shapeType switch
    {
        ShapeType.PointZ => true,
        ShapeType.PolyLineZ => true,
        ShapeType.PolygonZ => true,
        ShapeType.MultiPointZ => true,
        _ => false
    };

    public static bool HasM(this ShapeType shapeType) => shapeType switch
    {
        ShapeType.PointM => true,
        ShapeType.PolyLineM => true,
        ShapeType.PolygonM => true,
        ShapeType.MultiPointM => true,
        _ => false
    };

    public static bool IsCompatibleWithGeometry<T>(this ShapeType shapeType) =>
        shapeType.IsCompatibleWithGeometry(typeof(T));

    public static bool IsCompatibleWithGeometry(this ShapeType shapeType, Type geometryType) => shapeType switch
    {
        ShapeType.Point or ShapeType.PointZ or ShapeType.PointM => geometryType == typeof(Point),
        ShapeType.PolyLine or ShapeType.PolyLineZ or ShapeType.PolyLineM => geometryType == typeof(PolyLine),
        ShapeType.Polygon or ShapeType.PolygonZ or ShapeType.PolygonM => geometryType == typeof(Polygon),
        ShapeType.MultiPoint or ShapeType.MultiPointZ or ShapeType.MultiPointM => geometryType == typeof(MultiPoint),
        ShapeType.MultiPatch => geometryType == typeof(MultiPatch),
        _ => false
    };
}
