using System.Text.Json;
using System.Text.Json.Serialization;
using DBase;
using Shape.Geometries;

namespace Shape.GeoJson;

internal sealed class ShapeRecordJsonConverter : JsonConverter<ShapeRecord<Geometry, DbfRecord>>
{
    /// <inheritdoc />
    public override ShapeRecord<Geometry, DbfRecord> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var (geometry, properties) = GeoJsonRecordData.Read(document.RootElement, options);
        var schema = new GeoJsonAttributeSchema([properties]);
        return new(geometry, schema.CreateRecord(properties));
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, ShapeRecord<Geometry, DbfRecord> value, JsonSerializerOptions options)
    {
        if (value.Attributes.Descriptors.IsDefault)
            throw new JsonException("A shape record requires initialized DBF descriptors.");

        writer.WriteStartObject();
        writer.WriteString(GeoJsonPropertyNames.Type, GeoJsonPropertyNames.FeatureTypeName);
        writer.WriteStartObject(GeoJsonPropertyNames.Properties);
        for (var index = 0; index < value.Attributes.Count; index++)
        {
            writer.WritePropertyName(value.Attributes.Descriptors[index].Name.ToString());
            switch (value.Attributes[index].Value)
            {
                case string text: writer.WriteStringValue(text); break;
                case bool logical: writer.WriteBooleanValue(logical); break;
                case int number: writer.WriteNumberValue(number); break;
                case long number: writer.WriteNumberValue(number); break;
                case double number when double.IsFinite(number): writer.WriteNumberValue(number); break;
                case double: throw new JsonException("Feature properties must contain finite numbers.");
                case decimal number: writer.WriteNumberValue(number); break;
                case DateTime date: writer.WriteStringValue(date); break;
                default: writer.WriteNullValue(); break;
            }
        }
        writer.WriteEndObject();
        writer.WritePropertyName(GeoJsonPropertyNames.Geometry);
        if (value.Geometry is null || value.Geometry.ShapeType == ShapeType.Null) writer.WriteNullValue();
        else JsonSerializer.Serialize(writer, value.Geometry, options);
        writer.WriteEndObject();
    }
}
