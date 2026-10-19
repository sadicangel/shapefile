# Shape

A .NET reader and writer for ESRI shapefiles. `Shapefile` owns the `.shp`, `.shx`,
and `.dbf` files; attribute serialization uses [DBase](https://github.com/sadicangel/dbase).

Open a file once and choose geometry and attribute types for each operation:

```csharp
using Shape;
using Shape.Geometries;

using var file = Shapefile.Open("parcels.shp");

var raw = file.GetRecord(0);                         // Geometry + DbfRecord
var geometryTyped = file.GetRecord<Polygon>(0);     // Polygon + DbfRecord
var typed = file.GetRecord<Polygon, Parcel>(0);     // Polygon + Parcel
var attributesTyped = file.GetRecord<Geometry, Parcel>(0);

foreach (var (geometry, attributes) in file.EnumerateRecords<Polygon, Parcel>())
{
    Console.WriteLine(attributes.Name);
}

public record Parcel(long Id, string Name);
```

`EnumerateRecords()` and `EnumerateRecords<TGeometry>()` provide the corresponding
untyped-attribute sequences. Direct `foreach` over the file yields
`ShapeRecord<Geometry, DbfRecord>` values. Consumer attribute types follow DBase's
schema mapping rules; `DbfRecord` is also supported as the second type argument.

Append to an existing file using either a geometry/attributes pair or a `ShapeRecord`:

```csharp
file.Add(polygon, new Parcel(42, "North field"));
file.Add(record);
file.Flush();
```

Type arguments for `Add` are inferred. Non-null geometries must match the file's exact
shape type, including its dimensional variant. Disposal flushes and closes all owned files.
Appending across the component files is not transactional if an I/O operation fails.

Enumeration is lazy: geometry compatibility and reader selection occur once when an
enumeration starts. Its record count is captured at that point, so later appends are
excluded from that enumeration. DBF reads continue to use DBase's cached serializers.
Keep the owner open while enumerating; concurrent access is not supported.

Geometry and attributes are paired by physical record position, including DBF rows
marked as deleted. Typed attribute objects do not expose the deletion marker.
Null shapes use the geometry family's existing `Empty` value, selected from the file
header for untyped reads. Files whose header declares `ShapeType.Null` use `Point.Empty`
for untyped null records.

Migration: replace `Shapefile.Open<Polygon, Parcel>(path)` with
`Shapefile.Open(path)` followed by `EnumerateRecords<Polygon, Parcel>()` or
`GetRecord<Polygon, Parcel>(index)`. Generic file classes and `IShapefile` have been removed.

Use the opt-in `System.Text.Json` converter factory to read and write geometry objects:

```csharp
using System.Text.Json;
using Shape.GeoJson;

var options = new JsonSerializerOptions
{
    Converters = { new GeoJsonConverterFactory() }
};

string json = JsonSerializer.Serialize<Geometry>(new Point(1, 2), options);
Geometry? geometry = JsonSerializer.Deserialize<Geometry>(json, options);
```

Concrete `Point`, `MultiPoint`, `PolyLine`, and `Polygon` values are supported as well
as base `Geometry` values. A polyline with one part becomes a `LineString`; multiple
parts become a `MultiLineString`. The third coordinate is Z, and measures (M) are
omitted. Empty geometries use empty coordinate arrays. Polygon output uses
counterclockwise exterior rings and clockwise holes; both windings are accepted
when reading, and the source geometry is not modified.

No coordinate reference system conversion is performed. These coordinate conventions
follow [RFC 7946](https://www.rfc-editor.org/rfc/rfc7946). MultiPatch has no direct
GeoJSON equivalent and throws `NotSupportedException`. MultiPolygon and
GeometryCollection are not represented by the current geometry model and are unsupported.

The same factory maps `ShapeRecord<Geometry, DbfRecord>` to a GeoJSON `Feature`,
and `Shapefile` to a `FeatureCollection`. No separate Feature model is needed:

```csharp
using DBase;

var record = JsonSerializer.Deserialize<ShapeRecord<Geometry, DbfRecord>>(featureJson, options);
using var reconstructed = JsonSerializer.Deserialize<Shapefile>(collectionJson, options);
string output = JsonSerializer.Serialize(reconstructed, options);
```

Reading infers a new DBF schema; it does not restore the original DBF format or field
metadata. Strings become Character fields (Memo for long or padded text), integers
become Numeric fields containing `long`, fractional numbers become Numeric fields
containing `double`, and booleans become Logical fields. Date-looking strings remain
text. Nested objects and arrays are stored as JSON text. Numeric values outside the
supported 64-bit integer and 20-character Numeric field limits are rejected.

Collections infer one schema from all features, align properties by their original
names, and promote conflicting scalar types to text. Missing values are blank/null
according to the DBF field type: null or missing text becomes an empty string once
stored. Property names are normalized to unique ASCII DBF names of at most ten
characters. Null geometries become null shapes. Mixed non-null geometry types or
dimensional variants cannot share a shapefile and are rejected.

Deserialized shapefiles own in-memory SHP/SHX streams and temporary UTF-8 DBF storage,
including memo storage when needed. Dispose the returned file to remove that storage.
The reconstructed file supports the usual indexed reads, enumeration, append, and
flush operations. Standalone deserialized records retain their inferred descriptors.

The project currently targets .NET 11 with preview language features. Build and test with:

```text
dotnet test
dotnet test --configuration Release
```
