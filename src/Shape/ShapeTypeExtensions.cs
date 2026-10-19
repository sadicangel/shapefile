using Shape.Geometries;

namespace Shape;

internal static class ShapeTypeExtensions
{
    extension(ShapeType shapeType)
    {
        public bool HasZ => (int)shapeType / 10 is 1 or 3;

        public bool HasM => (int)shapeType / 10 >= 1;

        public Dimension Dimension => ((int)shapeType / 10) switch
        {
            3 => Dimension.Xyzm,
            2 => Dimension.Xym,
            1 => Dimension.Xyzm,
            0 when shapeType is not ShapeType.Null => Dimension.Xy,
            _ => Dimension.None,
        };

        public void EnsureCompatibleWith<T>() where T : Geometry
        {
            var geometryType = typeof(T);
            if (geometryType == typeof(Geometry))
            {
                return;
            }

            var isCompatible = shapeType switch
            {
                ShapeType.Point or ShapeType.PointZ or ShapeType.PointM => geometryType == typeof(Point),
                ShapeType.PolyLine or ShapeType.PolyLineZ or ShapeType.PolyLineM => geometryType == typeof(PolyLine),
                ShapeType.Polygon or ShapeType.PolygonZ or ShapeType.PolygonM => geometryType == typeof(Polygon),
                ShapeType.MultiPoint or ShapeType.MultiPointZ or ShapeType.MultiPointM => geometryType == typeof(MultiPoint),
                ShapeType.MultiPatch => geometryType == typeof(MultiPatch),
                _ => false
            };

            if (!isCompatible)
            {
                throw new InvalidOperationException($"{nameof(ShapeType)} '{shapeType}' is not compatible with {typeof(T)}");
            }
        }
    }
}
