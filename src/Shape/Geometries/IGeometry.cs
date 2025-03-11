namespace Shape.Geometries;

public interface IGeometry<T> where T : Geometry
{
    abstract static T Empty { get; }

    abstract static T Read(ReadOnlySpan<byte> source);
}
