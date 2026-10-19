using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using Shape.Geometries;

namespace Shape.GeoJson;

internal sealed class PolyLineJsonConverter : JsonConverter<PolyLine>
{
    /// <inheritdoc />
    public override PolyLine Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        reader.ReadGeometryObject(out var type, out var coordinates);
        ImmutableArray<LineString> lines;
        if (type == GeoJsonPropertyNames.LineStringTypeName)
        {
            var points = coordinates.ReadCoordinates();
            GeoJsonExtensions.ValidateLine(points);
            lines = [new LineString(points)];
        }
        else if (type == GeoJsonPropertyNames.MultiLineStringTypeName)
        {
            var parts = coordinates.ReadCoordinatesArray();
            foreach (var part in parts) GeoJsonExtensions.ValidateLine(part, allowEmpty: false);
            lines = [.. parts.Select(points => new LineString(points))];
        }
        else
        {
            throw new JsonException("Expected a LineString or MultiLineString geometry.");
        }
        return new PolyLine(lines);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, PolyLine value, JsonSerializerOptions options)
    {
        var singleLine = value.Lines.Length == 1;
        foreach (var line in value.Lines) GeoJsonExtensions.ValidateLine(line.Points, allowEmpty: singleLine);

        writer.WriteStartObject();
        writer.WriteString(GeoJsonPropertyNames.Type, singleLine ? GeoJsonPropertyNames.LineStringTypeName : GeoJsonPropertyNames.MultiLineStringTypeName);
        writer.WritePropertyName(GeoJsonPropertyNames.Coordinates);
        if (singleLine) writer.WriteCoordinates(value.Lines[0]);
        else writer.WriteCoordinatesArray(value.Lines);
        writer.WriteEndObject();
    }
}
