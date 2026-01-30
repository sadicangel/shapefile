namespace Shape.Geometries;

public interface IGeometry<out T> where T : Geometry
{
    static abstract T Empty { get; }
}
