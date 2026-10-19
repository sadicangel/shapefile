using System.Text.Json;
using System.Text.Json.Serialization;
using Shape.Geometries;

namespace Shape.GeoJson;

internal sealed class MultiPointJsonConverter : JsonConverter<MultiPoint>
{
    /// <inheritdoc />
    public override MultiPoint Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        reader.ReadGeometryObject(out var type, out var coordinates);
        if (type != GeoJsonPropertyNames.MultiPointTypeName) throw new JsonException("Expected a MultiPoint geometry.");
        var points = coordinates.ReadCoordinates();
        return points.IsEmpty ? MultiPoint.Empty : new MultiPoint(points);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, MultiPoint value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString(GeoJsonPropertyNames.Type, GeoJsonPropertyNames.MultiPointTypeName);
        writer.WritePropertyName(GeoJsonPropertyNames.Coordinates);
        writer.WriteCoordinates(value.Points);
        writer.WriteEndObject();
    }
}
