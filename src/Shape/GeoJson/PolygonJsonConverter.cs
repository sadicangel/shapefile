using System.Text.Json;
using System.Text.Json.Serialization;
using Shape.Geometries;

namespace Shape.GeoJson;

internal sealed class PolygonJsonConverter : JsonConverter<Polygon>
{
    /// <inheritdoc />
    public override Polygon Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        reader.ReadGeometryObject(out var type, out var coordinates);
        if (type != GeoJsonPropertyNames.PolygonTypeName) throw new JsonException("Expected a Polygon geometry.");
        var rings = coordinates.ReadCoordinatesArray();
        if (rings.IsEmpty) return Polygon.Empty;
        foreach (var ring in rings) GeoJsonExtensions.ValidateRing(ring);
        return new Polygon([.. rings.Select(ring => new LinearRing(ring))]);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, Polygon value, JsonSerializerOptions options)
    {
        if (value.ShapeType != ShapeType.Null)
            foreach (var ring in value.Rings) GeoJsonExtensions.ValidateRing(ring.Points);

        writer.WriteStartObject();
        writer.WriteString(GeoJsonPropertyNames.Type, GeoJsonPropertyNames.PolygonTypeName);
        writer.WritePropertyName(GeoJsonPropertyNames.Coordinates);
        if (value.ShapeType == ShapeType.Null)
        {
            writer.WriteStartArray();
            writer.WriteEndArray();
        }
        else
        {
            writer.WriteCoordinatesArray(value.Rings);
        }
        writer.WriteEndObject();
    }
}
