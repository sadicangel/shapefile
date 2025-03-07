namespace Shape.Geometries;

public interface IBinaryGeometry<T> where T : Geometry
{
    abstract static T Empty { get; }

    abstract static T Read(ReadOnlySpan<byte> source);
}
