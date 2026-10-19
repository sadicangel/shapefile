using System.Text.Json;
using System.Text.Json.Serialization;
using DBase;
using Shape.Geometries;

namespace Shape.GeoJson;

/// <summary>Registers GeoJSON converters for geometry classes, untyped shape records, and shapefiles.</summary>
/// <remarks>
/// Add an instance to <see cref="JsonSerializerOptions.Converters"/>. Coordinates contain X, Y,
/// and optional Z; shapefile measures are omitted. MultiPatch is explicitly unsupported.
/// Polygon output normalizes ring winding. No coordinate reference system transformation is performed.
/// Deserialized shapefiles own temporary attribute storage; dispose them when finished.
/// </remarks>
public sealed class GeoJsonConverterFactory : JsonConverterFactory
{
    /// <inheritdoc />
    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert == typeof(Geometry) || typeToConvert == typeof(Point) ||
        typeToConvert == typeof(MultiPoint) || typeToConvert == typeof(PolyLine) ||
        typeToConvert == typeof(Polygon) || typeToConvert == typeof(MultiPatch) ||
        typeToConvert == typeof(ShapeRecord<Geometry, DbfRecord>) || typeToConvert == typeof(Shapefile);

    /// <inheritdoc />
    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        if (typeToConvert == typeof(Geometry)) return new GeometryJsonConverter();
        if (typeToConvert == typeof(Point)) return new PointJsonConverter();
        if (typeToConvert == typeof(MultiPoint)) return new MultiPointJsonConverter();
        if (typeToConvert == typeof(PolyLine)) return new PolyLineJsonConverter();
        if (typeToConvert == typeof(Polygon)) return new PolygonJsonConverter();
        if (typeToConvert == typeof(MultiPatch)) return new UnsupportedMultiPatchJsonConverter();
        if (typeToConvert == typeof(ShapeRecord<Geometry, DbfRecord>)) return new ShapeRecordJsonConverter();
        if (typeToConvert == typeof(Shapefile)) return new ShapefileJsonConverter();
        throw new NotSupportedException($"No GeoJSON converter is available for '{typeToConvert}'.");
    }
}

internal sealed class UnsupportedMultiPatchJsonConverter : JsonConverter<MultiPatch>
{
    public override MultiPatch Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        throw new NotSupportedException("MultiPatch has no direct GeoJSON representation.");

    public override void Write(Utf8JsonWriter writer, MultiPatch value, JsonSerializerOptions options) =>
        throw new NotSupportedException("MultiPatch has no direct GeoJSON representation.");
}
