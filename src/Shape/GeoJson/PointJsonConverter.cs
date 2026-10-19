using System.Text.Json;
using System.Text.Json.Serialization;
using Shape.Geometries;

namespace Shape.GeoJson;

internal sealed class PointJsonConverter : JsonConverter<Point>
{
    /// <inheritdoc />
    public override Point Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        reader.ReadGeometryObject(out var type, out var coordinates);
        if (type != GeoJsonPropertyNames.PointTypeName) throw new JsonException("Expected a Point geometry.");
        return coordinates.ReadCoordinate();
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, Point value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString(GeoJsonPropertyNames.Type, GeoJsonPropertyNames.PointTypeName);
        writer.WritePropertyName(GeoJsonPropertyNames.Coordinates);
        writer.WriteCoordinate(value);
        writer.WriteEndObject();
    }
}
