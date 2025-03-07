namespace Shape;

public enum ShapeType
{
    /// <summary>
    /// No shape.
    /// </summary>
    Null = 0,
    /// <summary>
    /// 2D point.
    /// </summary>
    Point = 1,
    /// <summary>
    /// 2D line.
    /// </summary>
    PolyLine = 3,
    /// <summary>
    /// 2D polygon.
    /// </summary>
    Polygon = 5,
    /// <summary>
    /// 2D points.
    /// </summary>
    MultiPoint = 8,
    /// <summary>
    /// 3D point with optional measure.
    /// </summary>
    PointZ = 11,
    /// <summary>
    /// 3D lines with optional measure.
    /// </summary>
    PolyLineZ = 13,
    /// <summary>
    /// 3D polygon with optional measure.
    /// </summary>
    PolygonZ = 15,
    /// <summary>
    /// 3D points with optional measure.
    /// </summary>
    MultiPointZ = 18,
    /// <summary>
    /// 2D point with optional measure.
    /// </summary>
    PointM = 21,
    /// <summary>
    /// 2D line with optional measure.
    /// </summary>
    PolyLineM = 23,
    /// <summary>
    /// 2D polygon with optional measure.
    /// </summary>
    PolygonM = 25,
    /// <summary>
    /// 2D points with optional measure.
    /// </summary>
    MultiPointM = 28,
    /// <summary>
    /// ?
    /// </summary>
    MultiPatch = 31
}
