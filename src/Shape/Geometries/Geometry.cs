namespace Shape.Geometries;

public closed record Geometry(ShapeType ShapeType)
{
    public abstract BoundingBox GetBoundingBox();
}
