namespace Shape.GeoJson;

internal static class GeoJsonPropertyNames
{
    public const string PointTypeName = "Point";
    public const string MultiPointTypeName = "MultiPoint";
    public const string LineStringTypeName = "LineString";
    public const string MultiLineStringTypeName = "MultiLineString";
    public const string PolygonTypeName = "Polygon";
    public const string MultiPolygonTypeName = "MultiPolygon";
    public const string GeometryCollectionTypeName = "GeometryCollection";
    public const string MultiPatchTypeName = "MultiPatch";
    public const string FeatureTypeName = "Feature";
    public const string FeatureCollectionTypeName = "FeatureCollection";

    public static ReadOnlySpan<byte> Type => "type"u8;
    public static ReadOnlySpan<byte> Coordinates => "coordinates"u8;
    public static ReadOnlySpan<byte> Properties => "properties"u8;
    public static ReadOnlySpan<byte> Geometry => "geometry"u8;
    public static ReadOnlySpan<byte> Features => "features"u8;
}
