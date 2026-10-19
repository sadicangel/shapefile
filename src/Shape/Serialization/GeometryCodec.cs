using System.Buffers.Binary;
using Shape.Geometries;

namespace Shape.Serialization;

// Resolves the closed geometry family once per CLR type, without exposing a serializer
// type parameter on the file API. Enumeration holds this instance outside its loop.
internal abstract class GeometryCodec<TGeometry> where TGeometry : Geometry
{
    public static GeometryCodec<TGeometry> Instance { get; } = Create();

    public abstract int GetByteSize(TGeometry geometry);
    public abstract void Serialize(Span<byte> target, TGeometry geometry);
    public abstract TGeometry Deserialize(ReadOnlySpan<byte> source, ShapeType fileShapeType);

    private static GeometryCodec<TGeometry> Create()
    {
        object codec = typeof(TGeometry) switch
        {
            var type when type == typeof(Geometry) => new UntypedGeometryCodec(),
            var type when type == typeof(Point) => new TypedGeometryCodec<Point, PointSerializer>(),
            var type when type == typeof(PolyLine) => new TypedGeometryCodec<PolyLine, PolyLineSerializer>(),
            var type when type == typeof(Polygon) => new TypedGeometryCodec<Polygon, PolygonSerializer>(),
            var type when type == typeof(MultiPoint) => new TypedGeometryCodec<MultiPoint, MultiPointSerializer>(),
            var type when type == typeof(MultiPatch) => new TypedGeometryCodec<MultiPatch, MultiPatchSerializer>(),
            _ => throw new NotSupportedException($"Unsupported geometry type: {typeof(TGeometry)}.")
        };
        return (GeometryCodec<TGeometry>)codec;
    }
}

internal sealed class TypedGeometryCodec<TGeometry, TSerializer> : GeometryCodec<TGeometry>
    where TGeometry : Geometry
    where TSerializer : IGeometrySerializer<TGeometry>
{
    public override int GetByteSize(TGeometry geometry) => TSerializer.GetByteSize(geometry);
    public override void Serialize(Span<byte> target, TGeometry geometry) => TSerializer.Serialize(target, geometry);
    public override TGeometry Deserialize(ReadOnlySpan<byte> source, ShapeType fileShapeType) => TSerializer.Deserialize(source);
}

internal sealed class UntypedGeometryCodec : GeometryCodec<Geometry>
{
    public override int GetByteSize(Geometry geometry) => GeometrySerializer.GetByteSize(geometry);
    public override void Serialize(Span<byte> target, Geometry geometry) => GeometrySerializer.Serialize(target, geometry);

    public override Geometry Deserialize(ReadOnlySpan<byte> source, ShapeType fileShapeType)
    {
        if ((ShapeType)BinaryPrimitives.ReadInt32LittleEndian(source) != ShapeType.Null)
            return GeometrySerializer.Deserialize(source);

        // Null records carry no geometry family. Use the file header to preserve the
        // same Empty value returned by typed reads. Null-only files use Point.Empty.
        return fileShapeType switch
        {
            ShapeType.PolyLine or ShapeType.PolyLineZ or ShapeType.PolyLineM => PolyLine.Empty,
            ShapeType.Polygon or ShapeType.PolygonZ or ShapeType.PolygonM => Polygon.Empty,
            ShapeType.MultiPoint or ShapeType.MultiPointZ or ShapeType.MultiPointM => MultiPoint.Empty,
            ShapeType.MultiPatch => MultiPatch.Empty,
            _ => Point.Empty
        };
    }
}
