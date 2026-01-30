namespace Shape.Geometries;

public abstract record Geometry(ShapeType ShapeType)
{
    public abstract BoundingBox GetBoundingBox();
}
