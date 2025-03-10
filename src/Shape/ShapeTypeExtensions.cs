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
}
