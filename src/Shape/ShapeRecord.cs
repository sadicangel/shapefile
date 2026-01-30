using Shape.Geometries;

namespace Shape;

public readonly record struct ShapeRecord<TGeometry, TAttributes>(TGeometry Geometry, TAttributes Attributes)
    where TGeometry : Geometry
{
    public static ShapeRecord<TGeometry, TAttributes> Create(TGeometry geometry, TAttributes attributes) => new(geometry, attributes);
}
