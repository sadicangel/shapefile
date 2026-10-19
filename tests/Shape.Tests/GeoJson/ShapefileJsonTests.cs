using System.Text;
using System.Text.Json;
using DBase;
using Shape.GeoJson;
using Shape.Geometries;

namespace Shape.Tests.GeoJson;

public sealed class ShapefileJsonTests
{
    private static readonly JsonSerializerOptions Options = new() { Converters = { new GeoJsonConverterFactory() } };

    private static string Collection(params string[] features) =>
        "{\"type\":\"FeatureCollection\",\"features\":[" + string.Join(',', features) + "]}";

    [Fact]
    public void Read_Collection_InfersUnionSchemaAndAlignsSparseReorderedProperties()
    {
        var json = Collection(
            ShapeRecordJsonTests.Feature("""{"Name":"A","Value":1,"Flag":true}"""),
            ShapeRecordJsonTests.Feature("""{"Value":2.5,"Name":"a longer name","Later":"new"}"""),
            ShapeRecordJsonTests.Feature("""{"Value":null,"Flag":false}""", "null"));
        using var file = JsonSerializer.Deserialize<Shapefile>(json, Options)!;
        Assert.Equal(3, file.RecordCount);
        Assert.Equal(ShapeType.Point, file.ShapeType);
        var records = file.ToArray();
        Assert.Equal(new[] { "Name", "Value", "Flag", "Later" },
            records[0].Attributes.Descriptors.Select(descriptor => descriptor.Name.ToString()).ToArray());
        Assert.All(records, record => Assert.Equal(records[0].Attributes.Descriptors, record.Attributes.Descriptors));
        Assert.Equal("a longer name", records[1].Attributes[0].GetValue<string>());
        Assert.Equal(1.0, records[0].Attributes[1].GetValue<double>());
        Assert.Equal(2.5, records[1].Attributes[1].GetValue<double>());
        Assert.True(records[1].Attributes[2].IsNull);
        Assert.True(records[2].Attributes[1].IsNull);
        Assert.Equal("new", records[1].Attributes[3].GetValue<string>());
        Assert.Equal(Point.Empty, records[2].Geometry);

        using var result = JsonDocument.Parse(JsonSerializer.Serialize(file, Options));
        Assert.Equal(3, result.RootElement.GetProperty("features").GetArrayLength());
        Assert.Equal(JsonValueKind.Null, result.RootElement.GetProperty("features")[2].GetProperty("geometry").ValueKind);
    }

    [Fact]
    public void Read_ConflictingScalarTypes_PromotesToTextAndUsesAllRowsForWidths()
    {
        using var file = JsonSerializer.Deserialize<Shapefile>(Collection(
            ShapeRecordJsonTests.Feature("""{"Value":1}"""),
            ShapeRecordJsonTests.Feature("""{"Value":false}"""),
            ShapeRecordJsonTests.Feature("""{"Value":"longer text"}""")), Options)!;
        var records = file.ToArray();
        Assert.Equal(DbfFieldType.Character, records[0].Attributes.Descriptors[0].Type);
        Assert.Equal(new[] { "1", "false", "longer text" }, records.Select(record => record.Attributes[0].GetValue<string>()).ToArray());
    }

    [Fact]
    public void Read_LongUnicodeAndPaddedText_PreservesValuesAndCleansTemporaryStorage()
    {
        var before = TemporaryDirectories();
        var longText = new string('界', 300);
        var properties = JsonSerializer.Serialize(new { Name = " ação ", Long = longText });
        var file = JsonSerializer.Deserialize<Shapefile>(Collection(ShapeRecordJsonTests.Feature(properties)), Options)!;
        var created = TemporaryDirectories().Except(before).ToArray();
        try
        {
            Assert.Single(created);
            Assert.Contains(Directory.EnumerateFiles(created[0]), path => System.IO.Path.GetExtension(path) == ".dbt");
            Assert.Equal(" ação ", file.GetRecord(0).Attributes[0].GetValue<string>());
            Assert.Equal(longText, file.GetRecord(0).Attributes[1].GetValue<string>());
            var json = JsonSerializer.Serialize(file, Options);
            using var restored = JsonSerializer.Deserialize<Shapefile>(json, Options)!;
            Assert.Equal(longText, restored.GetRecord(0).Attributes[1].GetValue<string>());
        }
        finally
        {
            file.Dispose();
        }
        Assert.All(created, directory => Assert.False(Directory.Exists(directory)));
        file.Dispose();
    }

    private static string[] TemporaryDirectories() => Directory.GetDirectories(Path.GetTempPath(), "Shape.GeoJson-*");

    [Fact]
    public void Read_OriginalLongNamesWithDifferentOrders_KeepColumnsDistinct()
    {
        using var file = JsonSerializer.Deserialize<Shapefile>(Collection(
            ShapeRecordJsonTests.Feature("""{"abcdefghijk":1,"abcdefghijl":2}"""),
            ShapeRecordJsonTests.Feature("""{"abcdefghijl":3,"abcdefghijk":4}""")), Options)!;
        Assert.Equal(4L, file.GetRecord(1).Attributes[0].GetValue<long>());
        Assert.Equal(3L, file.GetRecord(1).Attributes[1].GetValue<long>());
        Assert.Equal("abcdefgh_2", file.GetRecord(1).Attributes.Descriptors[1].Name.ToString());
    }

    [Theory]
    [InlineData("{\"type\":\"Point\",\"coordinates\":[1,2]}", ShapeType.Point)]
    [InlineData("{\"type\":\"Point\",\"coordinates\":[1,2,3]}", ShapeType.PointZ)]
    [InlineData("{\"type\":\"MultiPoint\",\"coordinates\":[[1,2],[3,4]]}", ShapeType.MultiPoint)]
    [InlineData("{\"type\":\"LineString\",\"coordinates\":[[1,2],[3,4]]}", ShapeType.PolyLine)]
    [InlineData("{\"type\":\"Polygon\",\"coordinates\":[[[0,0],[2,0],[0,2],[0,0]]]}", ShapeType.Polygon)]
    public void RoundTrip_GeometryFamilies_CreateIndependentUsableFiles(string geometry, ShapeType shapeType)
    {
        var json = Collection(ShapeRecordJsonTests.Feature("{\"Id\":1}", "null"), ShapeRecordJsonTests.Feature("{\"Id\":2}", geometry));
        string output;
        using (var file = JsonSerializer.Deserialize<Shapefile>(json, Options)!)
        {
            Assert.Equal(shapeType, file.ShapeType);
            output = JsonSerializer.Serialize(file, Options);
        }
        using var restored = JsonSerializer.Deserialize<Shapefile>(output, Options)!;
        Assert.Equal(2, restored.RecordCount);
        Assert.Equal(shapeType, restored.ShapeType);
        Assert.Equal(2L, restored.GetRecord(1).Attributes[0].GetValue<long>());
        Assert.Equal(output, JsonSerializer.Serialize(restored, Options));
    }

    [Fact]
    public void Read_LargeIntegerAndScientificDecimal_PreservesRepresentableValues()
    {
        using var file = JsonSerializer.Deserialize<Shapefile>(Collection(
            ShapeRecordJsonTests.Feature("{\"Big\":9223372036854775807,\"Small\":1e-3}")), Options)!;
        Assert.Equal(long.MaxValue, file.GetRecord(0).Attributes[0].GetValue<long>());
        Assert.Equal(0.001, file.GetRecord(0).Attributes[1].GetValue<double>());
    }

    [Fact]
    public void Read_EmptyCollectionOrOnlyNullGeometries_CreatesNullShapeFile()
    {
        using var empty = JsonSerializer.Deserialize<Shapefile>(Collection(), Options)!;
        Assert.Equal(0, empty.RecordCount);
        Assert.Equal(ShapeType.Null, empty.ShapeType);
        Assert.Equal(Collection(), JsonSerializer.Serialize(empty, Options));
        using var nulls = JsonSerializer.Deserialize<Shapefile>(Collection(
            ShapeRecordJsonTests.Feature("null", "null"), ShapeRecordJsonTests.Feature("{}", "null")), Options)!;
        Assert.Equal(2, nulls.RecordCount);
        Assert.Equal(ShapeType.Null, nulls.ShapeType);
        Assert.All(nulls, record => Assert.Empty(record.Attributes));
    }

    [Fact]
    public void Write_ExistingFile_UsesItsNamedSchemaAndDoesNotDependOnSourceAfterRead()
    {
        using var fixture = new ShapefileFixture(ShapeType.Point, new Point(1, 2), new Point(3, 4));
        string json;
        using (var source = Shapefile.Open(fixture.Path)) json = JsonSerializer.Serialize(source, Options);
        using var result = JsonSerializer.Deserialize<Shapefile>(json, Options)!;
        var typed = result.EnumerateRecords<Point, FeatureAttributes>().ToArray();
        Assert.Equal(ShapefileFixture.AttributesAt(0), typed[0].Attributes);
        Assert.Equal(ShapefileFixture.AttributesAt(1), typed[1].Attributes);
        result.Add(new Point(5, 6), ShapefileFixture.AttributesAt(2));
        result.Flush();
        Assert.Equal(3, result.RecordCount);
        Assert.Equal(new Point(5, 6), result.GetRecord<Point>(2).Geometry);
    }

    [Fact]
    public async Task ReadWrite_AsyncStreamsAndNestedCollections_ConsumeExactlyOneValue()
    {
        var collection = Collection(ShapeRecordJsonTests.Feature("{\"Id\":1}"));
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(collection));
        var options = new JsonSerializerOptions(Options) { DefaultBufferSize = 1 };
        using var file = await JsonSerializer.DeserializeAsync<Shapefile>(stream, options, TestContext.Current.CancellationToken);
        Assert.Equal(1, file!.RecordCount);
        using var output = new MemoryStream();
        await JsonSerializer.SerializeAsync(output, file, options, TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(output.ToArray());
        Assert.Equal("FeatureCollection", document.RootElement.GetProperty("type").GetString());
        var files = JsonSerializer.Deserialize<Shapefile[]>("[" + collection + "," + collection + "]", Options)!;
        try { Assert.All(files, value => Assert.Equal(1, value.RecordCount)); }
        finally { foreach (var value in files) value.Dispose(); }
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("{\"type\":\"FeatureCollection\"}")]
    [InlineData("{\"type\":\"Feature\",\"features\":[]}")]
    [InlineData("{\"type\":\"FeatureCollection\",\"features\":null}")]
    [InlineData("{\"type\":\"FeatureCollection\",\"features\":{}}")]
    [InlineData("{\"type\":\"FeatureCollection\",\"features\":[null]}")]
    [InlineData("{\"type\":\"FeatureCollection\",\"features\":[{}]}")]
    [InlineData("{\"type\":\"FeatureCollection\",\"type\":\"FeatureCollection\",\"features\":[]}")]
    [InlineData("{\"type\":\"FeatureCollection\",\"features\":[],\"features\":[]}")]
    [InlineData("{\"type\":\"FeatureCollection\",\"features\":[")]
    public void Read_InvalidCollections_RejectsWithoutCreatingStorage(string json)
    {
        var before = TemporaryDirectories();
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Shapefile>(json, Options));
        Assert.Equal(before.Order(), TemporaryDirectories().Order());
    }

    [Fact]
    public void Read_MixedGeometryTypesOrDimensions_RejectsBeforeCreatingStorage()
    {
        var first = ShapeRecordJsonTests.Feature("{}");
        var polygon = ShapeRecordJsonTests.Feature("{}", "{\"type\":\"Polygon\",\"coordinates\":[[[0,0],[2,0],[0,2],[0,0]]]}");
        var pointZ = ShapeRecordJsonTests.Feature("{}", "{\"type\":\"Point\",\"coordinates\":[1,2,3]}");
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Shapefile>(Collection(first, polygon), Options));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Shapefile>(Collection(first, pointZ), Options));
    }

    [Fact]
    public void Read_IncompatibleNumericWidths_RejectsWithoutLeakingStorage()
    {
        var before = TemporaryDirectories();
        var json = Collection(ShapeRecordJsonTests.Feature("{\"Value\":9223372036854775807}"),
            ShapeRecordJsonTests.Feature("{\"Value\":0.1}"));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Shapefile>(json, Options));
        Assert.Equal(before.Order(), TemporaryDirectories().Order());
    }

    [Fact]
    public void Read_NullFile_UsesStandardReferenceNullHandling()
    {
        Assert.Null(JsonSerializer.Deserialize<Shapefile>("null", Options));
        Assert.Equal("null", JsonSerializer.Serialize<Shapefile?>(null, Options));
    }

    [Fact]
    public void Read_MemberOrderAndForeignMembers_StillReturnsAFile()
    {
        var json = "{\"metadata\":{\"features\":[1,2]},\"features\":[" +
            ShapeRecordJsonTests.Feature("{\"Name\":\"A\"}") + "],\"type\":\"FeatureCollection\",\"bbox\":[0,0,1,2]}";
        using var file = JsonSerializer.Deserialize<Shapefile>(json, Options)!;
        Assert.Equal("A", file.GetRecord(0).Attributes[0].GetValue<string>());
    }

    [Fact]
    public void Read_NullTextProperties_UseDbfBlankTextSemantics()
    {
        using var file = JsonSerializer.Deserialize<Shapefile>(Collection(
            ShapeRecordJsonTests.Feature("{\"Name\":\"A\",\"Other\":null}"),
            ShapeRecordJsonTests.Feature("{\"Name\":null}")), Options)!;
        Assert.Equal(string.Empty, file.GetRecord(1).Attributes[0].GetValue<string>());
        Assert.Equal(string.Empty, file.GetRecord(1).Attributes[1].GetValue<string>());
    }
}
