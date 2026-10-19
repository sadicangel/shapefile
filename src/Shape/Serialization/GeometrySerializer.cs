using System.Buffers.Binary;
using Shape.Geometries;

namespace Shape.Serialization;

public interface IGeometrySerializer<TGeometry> where TGeometry : Geometry
{
    static abstract int GetByteSize(TGeometry geometry);
    static abstract void Serialize(Span<byte> target, TGeometry geometry);
    static abstract TGeometry Deserialize(ReadOnlySpan<byte> source);
}

public sealed class GeometrySerializer : IGeometrySerializer<Geometry>
{
    public static int GetByteSize(Geometry geometry)
    {
        return geometry switch
        {
            Point point => ByteSize<Point, PointSerializer>(point),
            PolyLine polyLine => ByteSize<PolyLine, PolyLineSerializer>(polyLine),
            Polygon polygon => ByteSize<Polygon, PolygonSerializer>(polygon),
            MultiPoint multiPoint => ByteSize<MultiPoint, MultiPointSerializer>(multiPoint),
            MultiPatch multiPatch => ByteSize<MultiPatch, MultiPatchSerializer>(multiPatch),
            _ => throw new InvalidOperationException($"Unsupported geometry type: {geometry.GetType().Name}")
        };
    }

    public static int ByteSize<TGeometry, TSerializer>(TGeometry geometry)
        where TGeometry : Geometry
        where TSerializer : IGeometrySerializer<TGeometry> =>
        TSerializer.GetByteSize(geometry);

    public static void Serialize(Span<byte> target, Geometry geometry)
    {
        switch (geometry)
        {
            case Point point: Serialize<Point, PointSerializer>(target, point); break;
            case PolyLine polyLine: Serialize<PolyLine, PolyLineSerializer>(target, polyLine); break;
            case Polygon polygon: Serialize<Polygon, PolygonSerializer>(target, polygon); break;
            case MultiPoint multiPoint: Serialize<MultiPoint, MultiPointSerializer>(target, multiPoint); break;
            case MultiPatch multiPatch: Serialize<MultiPatch, MultiPatchSerializer>(target, multiPatch); break;
            default: throw new InvalidOperationException($"Unsupported geometry type: {geometry.GetType().Name}");
        }
    }

    public static void Serialize<TGeometry, TSerializer>(Span<byte> target, TGeometry geometry) where TGeometry : Geometry where TSerializer : IGeometrySerializer<TGeometry> =>
        TSerializer.Serialize(target, geometry);

    public static Geometry Deserialize(ReadOnlySpan<byte> source)
    {
        var shapeType = (ShapeType)BinaryPrimitives.ReadInt32LittleEndian(source);

        return shapeType switch
        {
            ShapeType.Point or ShapeType.PointZ or ShapeType.PointM => Deserialize<Point, PointSerializer>(source),
            ShapeType.PolyLine or ShapeType.PolyLineZ or ShapeType.PolyLineM => Deserialize<PolyLine, PolyLineSerializer>(source),
            ShapeType.Polygon or ShapeType.PolygonZ or ShapeType.PolygonM => Deserialize<Polygon, PolygonSerializer>(source),
            ShapeType.MultiPoint or ShapeType.MultiPointZ or ShapeType.MultiPointM => Deserialize<MultiPoint, MultiPointSerializer>(source),
            ShapeType.MultiPatch => Deserialize<MultiPatch, MultiPatchSerializer>(source),
            _ => throw new InvalidOperationException($"Unknown shape type: {shapeType}"),
        };
    }

    public static TGeometry Deserialize<TGeometry, TSerializer>(ReadOnlySpan<byte> source) where TGeometry : Geometry where TSerializer : IGeometrySerializer<TGeometry> =>
        TSerializer.Deserialize(source);
}
