using System.Text.Json;
using System.Text.Json.Serialization;
using Shape.Geometries;

namespace Shape.GeoJson;

internal sealed class ShapefileJsonConverter : JsonConverter<Shapefile>
{
    /// <inheritdoc />
    public override Shapefile Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object) throw new JsonException("Expected a FeatureCollection object.");
        string? type = null;
        JsonElement features = default;
        foreach (var member in root.EnumerateObject())
        {
            if (member.NameEquals(GeoJsonPropertyNames.Type))
            {
                if (type is not null || member.Value.ValueKind != JsonValueKind.String)
                    throw new JsonException("A FeatureCollection requires one string type member.");
                type = member.Value.GetString();
            }
            else if (member.NameEquals(GeoJsonPropertyNames.Features))
            {
                if (features.ValueKind != JsonValueKind.Undefined) throw new JsonException("Duplicate features member.");
                features = member.Value;
            }
        }
        if (type != GeoJsonPropertyNames.FeatureCollectionTypeName || features.ValueKind != JsonValueKind.Array)
            throw new JsonException("A FeatureCollection requires type and a features array.");

        var rows = features.EnumerateArray().Select(feature => GeoJsonRecordData.Read(feature, options)).ToArray();
        var shapeType = ShapeType.Null;
        foreach (var row in rows)
        {
            if (row.Geometry.ShapeType == ShapeType.Null) continue;
            if (shapeType != ShapeType.Null && shapeType != row.Geometry.ShapeType)
                throw new JsonException("A shapefile requires one geometry type and dimensional variant.");
            shapeType = row.Geometry.ShapeType;
        }
        var schema = new GeoJsonAttributeSchema(rows.Select(row => row.Properties));
        var file = Shapefile.CreateTemporary(shapeType, schema.Descriptors);
        try
        {
            foreach (var row in rows) file.Add(row.Geometry, schema.CreateRecord(row.Properties));
            file.Flush();
            return file;
        }
        catch
        {
            file.Dispose();
            throw;
        }
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, Shapefile value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString(GeoJsonPropertyNames.Type, GeoJsonPropertyNames.FeatureCollectionTypeName);
        writer.WriteStartArray(GeoJsonPropertyNames.Features);
        foreach (var record in value.EnumerateRecords()) JsonSerializer.Serialize(writer, record, options);
        writer.WriteEndArray();
        writer.WriteEndObject();
    }
}
