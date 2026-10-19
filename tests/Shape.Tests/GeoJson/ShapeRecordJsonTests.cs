using System.Collections.Immutable;
using System.Text.Json;
using DBase;
using Shape.GeoJson;
using Shape.Geometries;
using RawRecord = Shape.ShapeRecord<Shape.Geometries.Geometry, DBase.DbfRecord>;

namespace Shape.Tests.GeoJson;

public sealed class ShapeRecordJsonTests
{
    private static readonly JsonSerializerOptions Options = new() { Converters = { new GeoJsonConverterFactory() } };

    internal static string Feature(string properties, string geometry = "{\"type\":\"Point\",\"coordinates\":[1,2]}") =>
        $$"""{"type":"Feature","properties":{{properties}},"geometry":{{geometry}}}""";

    [Fact]
    public void Read_JsonPrimitives_InfersDbfDescriptorsAndValues()
    {
        var json = Feature("""{"Name":"Porto","Count":42,"Ratio":1.25,"Enabled":true,"Missing":null,"Date":"2026-10-19"}""");
        var record = JsonSerializer.Deserialize<RawRecord>(json, Options);

        Assert.Equal(new Point(1, 2), record.Geometry);
        Assert.Equal(new[] { "Name", "Count", "Ratio", "Enabled", "Missing", "Date" },
            record.Attributes.Descriptors.Select(descriptor => descriptor.Name.ToString()).ToArray());
        Assert.Equal(new[] { DbfFieldType.Character, DbfFieldType.Numeric, DbfFieldType.Numeric,
                DbfFieldType.Logical, DbfFieldType.Character, DbfFieldType.Character },
            record.Attributes.Descriptors.Select(descriptor => descriptor.Type).ToArray());
        Assert.Equal("Porto", record.Attributes[0].GetValue<string>());
        Assert.Equal(42L, record.Attributes[1].GetValue<long>());
        Assert.Equal(1.25, record.Attributes[2].GetValue<double>());
        Assert.Equal(2, record.Attributes.Descriptors[2].Decimal);
        Assert.True(record.Attributes[3].GetValue<bool>());
        Assert.True(record.Attributes[4].IsNull);
        Assert.Equal("2026-10-19", record.Attributes[5].GetValue<string>());
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    public void Read_EmptyOrNullProperties_ReturnsInitializedEmptyRecord(string properties)
    {
        var record = JsonSerializer.Deserialize<RawRecord>(Feature(properties, "null"), Options);
        Assert.Equal(Point.Empty, record.Geometry);
        Assert.Empty(record.Attributes);
        Assert.False(record.Attributes.Descriptors.IsDefault);
        using var result = JsonDocument.Parse(JsonSerializer.Serialize(record, Options));
        Assert.Equal(JsonValueKind.Null, result.RootElement.GetProperty("geometry").ValueKind);
        Assert.Empty(result.RootElement.GetProperty("properties").EnumerateObject());
    }

    [Theory]
    [InlineData("{\"type\":\"Point\",\"coordinates\":[1,2]}", typeof(Point))]
    [InlineData("{\"type\":\"Point\",\"coordinates\":[1,2,3]}", typeof(Point))]
    [InlineData("{\"type\":\"MultiPoint\",\"coordinates\":[[1,2],[3,4]]}", typeof(MultiPoint))]
    [InlineData("{\"type\":\"LineString\",\"coordinates\":[[1,2],[3,4]]}", typeof(PolyLine))]
    [InlineData("{\"type\":\"MultiLineString\",\"coordinates\":[[[1,2],[3,4]],[[5,6],[7,8]]]}", typeof(PolyLine))]
    [InlineData("{\"type\":\"Polygon\",\"coordinates\":[[[0,0],[2,0],[0,2],[0,0]]]}", typeof(Polygon))]
    public void RoundTrip_AllSupportedGeometryTypes_WriteActualNestedGeometry(string geometry, Type expectedType)
    {
        var json = Feature("""{"Name":"A","Value":-2.5}""", geometry);
        var record = JsonSerializer.Deserialize<RawRecord>(json, Options);
        Assert.IsType(expectedType, record.Geometry);
        var output = JsonSerializer.Serialize(record, Options);
        Assert.Equal(json, output);
        var restored = JsonSerializer.Deserialize<RawRecord>(output, Options);
        Assert.Equal(record.Geometry, restored.Geometry);
        Assert.Equal(record.Attributes, restored.Attributes);
    }

    [Fact]
    public void Write_AllDbfValueTypes_ProduceJsonPrimitivesAndIsoDate()
    {
        ImmutableArray<DbfFieldDescriptor> descriptors =
        [
            DbfFieldDescriptor.Character("Text", 10), DbfFieldDescriptor.Logical("Flag"),
            DbfFieldDescriptor.Int32("Int"), DbfFieldDescriptor.Numeric("Long", 20),
            DbfFieldDescriptor.Double("Double"), DbfFieldDescriptor.Numeric("Decimal", 20, 2),
            DbfFieldDescriptor.Date("Date"), DbfFieldDescriptor.Character("Null", 1)
        ];
        var date = new DateTime(2026, 10, 19, 12, 30, 0, DateTimeKind.Utc);
        var record = new RawRecord(new Point(1, 2), new DbfRecord(descriptors,
            [new("text"), new(true), new(12), new(long.MaxValue), new(1.25), new(2.5m), new(date), default]));

        using var result = JsonDocument.Parse(JsonSerializer.Serialize(record, Options));
        var properties = result.RootElement.GetProperty("properties");
        Assert.Equal("text", properties.GetProperty("Text").GetString());
        Assert.True(properties.GetProperty("Flag").GetBoolean());
        Assert.Equal(12, properties.GetProperty("Int").GetInt32());
        Assert.Equal(long.MaxValue, properties.GetProperty("Long").GetInt64());
        Assert.Equal(1.25, properties.GetProperty("Double").GetDouble());
        Assert.Equal(2.5m, properties.GetProperty("Decimal").GetDecimal());
        Assert.Equal(date, properties.GetProperty("Date").GetDateTime());
        Assert.Equal(JsonValueKind.Null, properties.GetProperty("Null").ValueKind);
        var inferred = JsonSerializer.Deserialize<RawRecord>(result.RootElement, Options);
        Assert.Equal(DbfFieldType.Character, inferred.Attributes.Descriptors[6].Type);
    }

    [Fact]
    public void Read_PropertiesWithNestedValues_StoresJsonText()
    {
        var record = JsonSerializer.Deserialize<RawRecord>(Feature("""{"Nested":{"x":1},"Array":[true,"a"]}"""), Options);
        Assert.Equal("{\"x\":1}", record.Attributes[0].GetValue<string>());
        Assert.Equal("[true,\"a\"]", record.Attributes[1].GetValue<string>());
    }

    [Fact]
    public void Read_LongUnicodeAndPaddedText_UsesAppropriateFieldWidthsOrMemo()
    {
        var properties = JsonSerializer.Serialize(new { Short = "ação", Long = new string('界', 100), Padded = " a " });
        var record = JsonSerializer.Deserialize<RawRecord>(Feature(properties), Options);
        Assert.Equal(6, record.Attributes.Descriptors[0].Length);
        Assert.Equal(DbfFieldType.Memo, record.Attributes.Descriptors[1].Type);
        Assert.Equal(DbfFieldType.Memo, record.Attributes.Descriptors[2].Type);
        Assert.Equal(" a ", record.Attributes[2].GetValue<string>());
    }

    [Fact]
    public void Read_InvalidAndCollidingFieldNames_GeneratesUniqueDbfNames()
    {
        var record = JsonSerializer.Deserialize<RawRecord>(Feature(
            """{"abcdefghijk":1,"abcdefghijl":2,"Name":3,"name":4,"":5,"City Name":6,"área":7}"""), Options);
        var names = record.Attributes.Descriptors.Select(descriptor => descriptor.Name.ToString()).ToArray();
        Assert.Equal(new[] { "abcdefghij", "abcdefgh_2", "Name", "name_2", "Field", "City_Name", "_rea" }, names);
        Assert.All(record.Attributes.Descriptors, descriptor => Assert.True(descriptor.Name.IsValid));
        Assert.Equal(names.Length, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void Read_MemberOrderAndForeignMembers_AreSupportedInsideArrays()
    {
        const string feature = """{"geometry":{"type":"Point","coordinates":[1,2]},"foreign":{"nested":[1,2]},"properties":{"A":1},"id":"ignored","type":"Feature"}""";
        var records = JsonSerializer.Deserialize<RawRecord[]>("[" + feature + "," + feature + "]", Options)!;
        Assert.Equal(2, records.Length);
        Assert.All(records, record => Assert.Equal(1L, record.Attributes[0].GetValue<long>()));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("{\"type\":\"Feature\",\"geometry\":null}")]
    [InlineData("{\"type\":\"Feature\",\"properties\":{}}")]
    [InlineData("{\"type\":\"feature\",\"geometry\":null,\"properties\":{}}")]
    [InlineData("{\"type\":null,\"geometry\":null,\"properties\":{}}")]
    [InlineData("{\"type\":\"Feature\",\"type\":\"Feature\",\"geometry\":null,\"properties\":{}}")]
    [InlineData("{\"type\":\"Feature\",\"geometry\":null,\"geometry\":null,\"properties\":{}}")]
    [InlineData("{\"type\":\"Feature\",\"geometry\":null,\"properties\":{},\"properties\":{}}")]
    [InlineData("{\"type\":\"Feature\",\"geometry\":[],\"properties\":{}}")]
    [InlineData("{\"type\":\"Feature\",\"geometry\":null,\"properties\":[]}")]
    [InlineData("{\"type\":\"Feature\",\"geometry\":null,\"properties\":{\"A\":1,\"A\":2}}")]
    [InlineData("{\"type\":\"Feature\",\"geometry\":null,\"properties\":{\"A\":1e400}}")]
    [InlineData("{\"type\":\"Feature\",\"geometry\":null,\"properties\":{\"A\":1e-100}}")]
    [InlineData("{\"type\":\"Feature\",\"geometry\":null,\"properties\":{\"A\":1e-400}}")]
    [InlineData("{\"type\":\"Feature\",\"geometry\":null,\"properties\":{\"A\":9223372036854775808}}")]
    [InlineData("{\"type\":\"Feature\",\"geometry\":null,\"properties\":{\"A\":0.1234567890123456789}}")]
    [InlineData("{\"type\":\"Feature\",\"geometry\":null,\"properties\":{\"A\":9.999999999999999999}}")]
    public void Read_InvalidFeaturesOrUnrepresentableNumbers_ThrowJsonException(string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<RawRecord>(json, Options));
    }

    [Fact]
    public void Write_UninitializedRecordOrNonFiniteProperty_ThrowsJsonException()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Serialize(default(RawRecord), Options));
        var record = new RawRecord(Point.Empty,
            new DbfRecord([DbfFieldDescriptor.Double("Value")], [new(double.NaN)]));
        Assert.Throws<JsonException>(() => JsonSerializer.Serialize(record, Options));
    }

    [Fact]
    public void Factory_RegistersRecordAndFileConverters()
    {
        var factory = new GeoJsonConverterFactory();
        Assert.True(factory.CanConvert(typeof(RawRecord)));
        Assert.True(factory.CanConvert(typeof(Shapefile)));
        Assert.IsType<ShapeRecordJsonConverter>(factory.CreateConverter(typeof(RawRecord), Options));
        Assert.IsType<ShapefileJsonConverter>(factory.CreateConverter(typeof(Shapefile), Options));
    }

    [Fact]
    public void Read_ZeroWithExponent_IsNotConfusedWithUnderflow()
    {
        var record = JsonSerializer.Deserialize<RawRecord>(Feature("{\"Zero\":0e-400}"), Options);
        Assert.Equal(0L, record.Attributes[0].GetValue<long>());
    }
}
