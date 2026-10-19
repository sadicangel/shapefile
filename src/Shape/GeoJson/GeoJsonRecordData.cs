using System.Text.Json;
using Shape.Geometries;

namespace Shape.GeoJson;

internal static class GeoJsonRecordData
{
    // The returned properties borrow the caller's JsonDocument until schema inference and
    // materialization finish. No JsonElement is retained by the resulting DBF records.
    public static (Geometry Geometry, JsonElement Properties) Read(JsonElement element, JsonSerializerOptions options)
    {
        if (element.ValueKind != JsonValueKind.Object) throw new JsonException("Expected a Feature object.");
        string? type = null;
        JsonElement geometry = default, properties = default;
        foreach (var member in element.EnumerateObject())
        {
            if (member.NameEquals(GeoJsonPropertyNames.Type))
            {
                if (type is not null || member.Value.ValueKind != JsonValueKind.String)
                    throw new JsonException("A Feature requires one string type member.");
                type = member.Value.GetString();
            }
            else if (member.NameEquals(GeoJsonPropertyNames.Geometry))
            {
                if (geometry.ValueKind != JsonValueKind.Undefined) throw new JsonException("Duplicate geometry member.");
                geometry = member.Value;
            }
            else if (member.NameEquals(GeoJsonPropertyNames.Properties))
            {
                if (properties.ValueKind != JsonValueKind.Undefined) throw new JsonException("Duplicate properties member.");
                properties = member.Value;
            }
        }
        if (type != GeoJsonPropertyNames.FeatureTypeName ||
            geometry.ValueKind is not (JsonValueKind.Object or JsonValueKind.Null) ||
            properties.ValueKind is not (JsonValueKind.Object or JsonValueKind.Null))
            throw new JsonException("A Feature requires type, geometry, and object or null properties.");

        return (geometry.Deserialize<Geometry>(options) ?? Point.Empty, properties);
    }
}
