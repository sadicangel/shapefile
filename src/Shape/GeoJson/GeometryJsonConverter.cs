using System.Text.Json;
using System.Text.Json.Serialization;
using Shape.Geometries;

namespace Shape.GeoJson;

internal sealed class GeometryJsonConverter : JsonConverter<Geometry>
{
    /// <inheritdoc />
    public override Geometry Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var probe = reader;
        probe.ReadGeometryObject(out var type, out _, requireCoordinates: false);
        return type switch
        {
            GeoJsonPropertyNames.PointTypeName => JsonSerializer.Deserialize<Point>(ref reader, options)!,
            GeoJsonPropertyNames.MultiPointTypeName => JsonSerializer.Deserialize<MultiPoint>(ref reader, options)!,
            GeoJsonPropertyNames.LineStringTypeName or GeoJsonPropertyNames.MultiLineStringTypeName =>
                JsonSerializer.Deserialize<PolyLine>(ref reader, options)!,
            GeoJsonPropertyNames.PolygonTypeName => JsonSerializer.Deserialize<Polygon>(ref reader, options)!,
            GeoJsonPropertyNames.MultiPatchTypeName or GeoJsonPropertyNames.MultiPolygonTypeName or GeoJsonPropertyNames.GeometryCollectionTypeName or GeoJsonPropertyNames.FeatureTypeName or GeoJsonPropertyNames.FeatureCollectionTypeName =>
                throw new NotSupportedException($"The geometry model does not support the GeoJSON type '{type}'."),
            _ => throw new JsonException($"Unknown GeoJSON geometry type: '{type}'.")
        };
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, Geometry value, JsonSerializerOptions options)
    {
        switch (value)
        {
            case Point point: JsonSerializer.Serialize(writer, point, options); break;
            case MultiPoint multiPoint: JsonSerializer.Serialize(writer, multiPoint, options); break;
            case PolyLine polyLine: JsonSerializer.Serialize(writer, polyLine, options); break;
            case Polygon polygon: JsonSerializer.Serialize(writer, polygon, options); break;
            case MultiPatch: throw new NotSupportedException("MultiPatch has no direct GeoJSON representation.");
        }
    }
}
