using System.Buffers;
using System.Text;
using System.Text.Json;
using Shape.GeoJson;
using Shape.Geometries;

namespace Shape.Tests.GeoJson;

public sealed class GeoJsonTests
{
    private static readonly JsonSerializerOptions Options = new() { Converters = { new GeoJsonConverterFactory() } };

    public static TheoryData<int, string> Geometries => new()
    {
        { 0, """{"type":"Point","coordinates":[10,20]}""" },
        { 1, """{"type":"Point","coordinates":[10,20,30]}""" },
        { 2, """{"type":"MultiPoint","coordinates":[[1,2],[3,4]]}""" },
        { 3, """{"type":"MultiPoint","coordinates":[[1,2,3],[4,5,6]]}""" },
        { 4, """{"type":"LineString","coordinates":[[1,2],[3,4]]}""" },
        { 5, """{"type":"LineString","coordinates":[[1,2,3],[4,5,6]]}""" },
        { 6, """{"type":"MultiLineString","coordinates":[[[1,2],[3,4]],[[5,6],[7,8]]]}""" },
        { 7, """{"type":"MultiLineString","coordinates":[[[1,2,3],[4,5,6]],[[7,8,9],[10,11,12]]]}""" },
        { 8, """{"type":"Polygon","coordinates":[[[0,0],[2,0],[0,2],[0,0]]]}""" },
        { 9, """{"type":"Polygon","coordinates":[[[0,0,3],[2,0,3],[0,2,3],[0,0,3]]]}""" },
        { 10, """{"type":"Polygon","coordinates":[[[0,0],[2,0],[0,2],[0,0]],[[0.25,0.25],[0.25,0.5],[0.5,0.25],[0.25,0.25]]]}""" },
        { 11, """{"type":"Point","coordinates":[]}""" },
        { 12, """{"type":"MultiPoint","coordinates":[]}""" },
        { 13, """{"type":"LineString","coordinates":[]}""" },
        { 14, """{"type":"MultiLineString","coordinates":[]}""" },
        { 15, """{"type":"Polygon","coordinates":[]}""" }
    };

    private static Point PointZ(double x, double y, double z) => new(x, y, z, NumericLimits.NoValue);

    private static Geometry GeometryAt(int index) => index switch
    {
        0 => new Point(10, 20),
        1 => PointZ(10, 20, 30),
        2 => new MultiPoint([new Point(1, 2), new Point(3, 4)]),
        3 => new MultiPoint([PointZ(1, 2, 3), PointZ(4, 5, 6)]),
        4 => new PolyLine([new LineString([new Point(1, 2), new Point(3, 4)])]),
        5 => new PolyLine([new LineString([PointZ(1, 2, 3), PointZ(4, 5, 6)])]),
        6 => new PolyLine(
        [
            new LineString([new Point(1, 2), new Point(3, 4)]),
            new LineString([new Point(5, 6), new Point(7, 8)])
        ]),
        7 => new PolyLine(
        [
            new LineString([PointZ(1, 2, 3), PointZ(4, 5, 6)]),
            new LineString([PointZ(7, 8, 9), PointZ(10, 11, 12)])
        ]),
        8 => new Polygon([new LinearRing([new Point(0, 0), new Point(2, 0), new Point(0, 2), new Point(0, 0)])]),
        9 => new Polygon([new LinearRing([PointZ(0, 0, 3), PointZ(2, 0, 3), PointZ(0, 2, 3), PointZ(0, 0, 3)])]),
        10 => new Polygon(
        [
            new LinearRing([new Point(0, 0), new Point(2, 0), new Point(0, 2), new Point(0, 0)]),
            new LinearRing([new Point(0.25, 0.25), new Point(0.25, 0.5), new Point(0.5, 0.25), new Point(0.25, 0.25)])
        ]),
        11 => Point.Empty,
        12 => MultiPoint.Empty,
        13 => PolyLine.Empty,
        14 => new PolyLine([]),
        15 => Polygon.Empty,
        _ => throw new ArgumentOutOfRangeException(nameof(index))
    };

    [Theory]
    [MemberData(nameof(Geometries))]
    public void Serialize_AllSupportedGeometries_WriteExpectedCoordinateStructure(int index, string expected)
    {
        var geometry = GeometryAt(index);
        Assert.Equal(expected, JsonSerializer.Serialize(geometry, Options));
        Assert.Equal(expected, JsonSerializer.Serialize(geometry, geometry.GetType(), Options));
    }

    [Theory]
    [MemberData(nameof(Geometries))]
    public void Deserialize_AllSupportedGeometries_SupportConcreteAndBaseTypes(int index, string json)
    {
        var expected = GeometryAt(index);
        Assert.Equal(expected, JsonSerializer.Deserialize<Geometry>(json, Options));
        Assert.Equal(expected, JsonSerializer.Deserialize(json, expected.GetType(), Options));
    }

    [Theory]
    [MemberData(nameof(Geometries))]
    public void Deserialize_CoordinatesBeforeTypeAndForeignMembers_IgnoresObjectMemberOrder(int index, string json)
    {
        using var document = JsonDocument.Parse(json);
        var type = document.RootElement.GetProperty("type").GetString();
        var coordinates = document.RootElement.GetProperty("coordinates").GetRawText();
        var reordered = $$"""
            {"metadata":{"type":"ignored","coordinates":[{"nested":true}]},
             "coordinates":{{coordinates}},"bbox":[0,0,1,1],"type":"{{type}}","extra":[null,{"value":1}]}
            """;
        var expected = GeometryAt(index);
        Assert.Equal(expected, JsonSerializer.Deserialize<Geometry>(reordered, Options));
        Assert.Equal(expected, JsonSerializer.Deserialize(reordered, expected.GetType(), Options));
    }

    [Theory]
    [MemberData(nameof(Geometries))]
    public void RoundTrip_AllSupportedGeometries_PreserveCoordinates(int index, string json)
    {
        var expected = GeometryAt(index);
        var serialized = JsonSerializer.Serialize(expected, Options);
        Assert.Equal(expected, JsonSerializer.Deserialize<Geometry>(serialized, Options));
        Assert.Equal(json, serialized);
    }

    [Fact]
    public void Point_ThirdOrdinate_IsZAndMeasuresAreOmitted()
    {
        var point = JsonSerializer.Deserialize<Point>("""{"type":"Point","coordinates":[1,2,3]}""", Options)!;
        Assert.Equal(ShapeType.PointZ, point.ShapeType);
        Assert.Equal(3, point.Z);
        Assert.False(point.HasM);
        Assert.Equal(
            """{"type":"Point","coordinates":[1,2,3]}""",
            JsonSerializer.Serialize(new Point(1, 2, 3, 99), Options));
        Assert.Equal(
            """{"type":"Point","coordinates":[1,2]}""",
            JsonSerializer.Serialize(new Point(1, 2, 99), Options));
    }

    [Fact]
    public void Serialize_MeasuredCompositeGeometries_OmitMeasuresFromEveryPosition()
    {
        var line = new PolyLine([new LineString([new Point(1, 2, 100), new Point(3, 4, 200)])]);
        var multiPoint = new MultiPoint([new Point(1, 2, 3, 100), new Point(4, 5, 6, 200)]);
        var polygon = new Polygon(
        [
            new LinearRing(
            [
                new Point(0, 0, 10), new Point(2, 0, 20), new Point(0, 2, 30), new Point(0, 0, 40)
            ])
        ]);
        Assert.Equal("""{"type":"LineString","coordinates":[[1,2],[3,4]]}""", JsonSerializer.Serialize(line, Options));
        Assert.Equal("""{"type":"MultiPoint","coordinates":[[1,2,3],[4,5,6]]}""", JsonSerializer.Serialize(multiPoint, Options));
        Assert.Equal("""{"type":"Polygon","coordinates":[[[0,0],[2,0],[0,2],[0,0]]]}""", JsonSerializer.Serialize(polygon, Options));
    }

    [Fact]
    public void Serialize_Polygon_NormalizesExteriorAndHoleWindingWithoutMutatingGeometry()
    {
        LinearRing exterior = [new Point(0, 0), new Point(0, 2), new Point(2, 0), new Point(0, 0)];
        LinearRing hole = [new Point(0.25, 0.25), new Point(0.5, 0.25), new Point(0.25, 0.5), new Point(0.25, 0.25)];
        var polygon = new Polygon([exterior, hole]);
        var json = JsonSerializer.Serialize(polygon, Options);
        Assert.Equal(GeometryAt(10), JsonSerializer.Deserialize<Polygon>(json, Options));
        Assert.Equal(exterior, polygon.Rings[0]);
        Assert.Equal(hole, polygon.Rings[1]);

        // Reading legacy winding is valid, even though writers normalize it.
        const string clockwise = """{"type":"Polygon","coordinates":[[[0,0],[0,2],[2,0],[0,0]]]}""";
        Assert.Equal(exterior, JsonSerializer.Deserialize<Polygon>(clockwise, Options)!.ExteriorRing);
    }

    [Fact]
    public void ArraysAndObjectProperties_ConsumeExactlyOneGeometryAtATime()
    {
        Geometry[] geometries = [GeometryAt(0), GeometryAt(2), GeometryAt(6), GeometryAt(10)];
        var json = JsonSerializer.Serialize(geometries, Options);
        Assert.Equal(geometries, JsonSerializer.Deserialize<Geometry[]>(json, Options));

        var envelope = new Envelope(GeometryAt(7), "after geometry");
        Assert.Equal(envelope, JsonSerializer.Deserialize<Envelope>(JsonSerializer.Serialize(envelope, Options), Options));
    }

    [Fact]
    public void Deserialize_MultiSegmentInput_SupportsCopiedCoordinateReaders()
    {
        const string json = """{"coordinates":[[[1,2,3],[4,5,6]],[[7,8,9],[10,11,12]]],"type":"MultiLineString"}""";
        var bytes = Encoding.UTF8.GetBytes(json);
        var first = new BufferSegment(bytes.AsMemory(0, 27));
        var last = first.Append(bytes.AsMemory(27, 16)).Append(bytes.AsMemory(43));
        var sequence = new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);
        var reader = new Utf8JsonReader(sequence);
        Assert.Equal(GeometryAt(7), JsonSerializer.Deserialize<Geometry>(ref reader, Options));
        Assert.Equal(JsonTokenType.EndObject, reader.TokenType);
        Assert.False(reader.Read());
    }

    [Fact]
    public async Task StreamSerialization_SupportsAsyncReadingAndWriting()
    {
        var geometries = Enumerable.Range(0, 11).Select(GeometryAt).ToArray();
        await using var stream = new MemoryStream();
        await JsonSerializer.SerializeAsync(stream, geometries, Options, TestContext.Current.CancellationToken);
        stream.Position = 0;
        var options = new JsonSerializerOptions(Options) { DefaultBufferSize = 1 };
        Assert.Equal(geometries, await JsonSerializer.DeserializeAsync<Geometry[]>(stream, options, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void NullReferences_UseNormalSystemTextJsonNullHandling()
    {
        Assert.Equal("null", JsonSerializer.Serialize<Geometry?>(null, Options));
        Assert.Equal("null", JsonSerializer.Serialize<Point?>(null, Options));
        Assert.Null(JsonSerializer.Deserialize<Geometry>("null", Options));
        Assert.Null(JsonSerializer.Deserialize<Point>("null", Options));
        Assert.Null(JsonSerializer.Deserialize<PolyLine>("null", Options));
        Assert.Null(JsonSerializer.Deserialize<Polygon>("null", Options));
        Assert.Null(JsonSerializer.Deserialize<MultiPoint>("null", Options));
    }

    public static TheoryData<string> InvalidObjects => new()
    {
        "{}",
        "[]",
        "42",
        "true",
        "\"Point\"",
        """{"type":"Point"}""",
        """{"coordinates":[1,2]}""",
        """{"type":null,"coordinates":[1,2]}""",
        """{"type":1,"coordinates":[1,2]}""",
        """{"type":"point","coordinates":[1,2]}""",
        """{"Type":"Point","coordinates":[1,2]}""",
        """{"type":"Point","Coordinates":[1,2]}""",
        """{"type":"Point","type":"Point","coordinates":[1,2]}""",
        """{"type":"Point","coordinates":[1,2],"coordinates":[3,4]}""",
        """{"type":"Point","coordinates":null}""",
        """{"type":"Point","coordinates":{}}""",
        """{"type":"Point","coordinates":"1,2"}""",
        """{"type":"Point","coordinates":[1]}""",
        """{"type":"Point","coordinates":[1,2,3,4]}""",
        """{"type":"Point","coordinates":[[1,2]]}""",
        """{"type":"Point","coordinates":[1,"2"]}""",
        """{"type":"Point","coordinates":[true,2]}""",
        """{"type":"Point","coordinates":[null,2]}""",
        """{"type":"Point","coordinates":[1e400,2]}""",
        """{"type":"Point","coordinates":[1,2,1e400]}""",
        """{"type":"Point","coordinates":[1,2]""",
        """{"type":"Point","coordinates":[1,2""",
        """{"type":"Point","coordinates":[1,2],"foreign":{"nested":[1,2]}"""
    };

    [Theory]
    [MemberData(nameof(InvalidObjects))]
    public void Deserialize_MalformedPointObjects_ThrowJsonException(string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Point>(json, Options));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Geometry>(json, Options));
    }

    [Theory]
    [InlineData("MultiPoint", "[[1]]")]
    [InlineData("MultiPoint", "[[]]")]
    [InlineData("MultiPoint", "[null]")]
    [InlineData("MultiPoint", "[1,2]")]
    [InlineData("LineString", "[[1,2]]")]
    [InlineData("LineString", "[[1,2],[]]")]
    [InlineData("LineString", "[[1,2],[3,4,5,6]]")]
    [InlineData("MultiLineString", "[[[1,2]]]")]
    [InlineData("MultiLineString", "[[]]")]
    [InlineData("MultiLineString", "[[1,2],[3,4]]")]
    [InlineData("Polygon", "[[]]")]
    [InlineData("Polygon", "[[[0,0],[1,0],[0,0]]]")]
    [InlineData("Polygon", "[[[0,0],[1,0],[0,1],[1,1]]]")]
    [InlineData("Polygon", "[[[0,0,1],[1,0,1],[0,1,1],[0,0,2]]]")]
    [InlineData("Polygon", "[[[0,0],[1,0],[0,1],[0,0]],[]]")]
    public void Deserialize_InvalidCompositeCoordinates_ThrowJsonException(string type, string coordinates)
    {
        var json = $$"""{"type":"{{type}}","coordinates":{{coordinates}}}""";
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Geometry>(json, Options));
        var concreteType = type switch
        {
            "MultiPoint" => typeof(MultiPoint),
            "Polygon" => typeof(Polygon),
            _ => typeof(PolyLine)
        };
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize(json, concreteType, Options));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"type\":\"Point\",\"coordinates\":[1,2]}")]
    [InlineData("{\"type\":\"MultiPatch\",\"coordinates\":[]}")]
    public void MultiPatch_TypedReadAndWrite_AreExplicitlyUnsupported(string json)
    {
        Assert.Throws<NotSupportedException>(() => JsonSerializer.Deserialize<MultiPatch>(json, Options));
        Assert.Throws<NotSupportedException>(() => JsonSerializer.Serialize(MultiPatch.Empty, Options));
        Assert.Throws<NotSupportedException>(() => JsonSerializer.Serialize<Geometry>(MultiPatch.Empty, Options));
    }

    [Theory]
    [InlineData("{\"type\":\"MultiPolygon\",\"coordinates\":[]}")]
    [InlineData("{\"type\":\"GeometryCollection\",\"geometries\":[]}")]
    [InlineData("{\"type\":\"MultiPatch\",\"coordinates\":[]}")]
    [InlineData("{\"type\":\"Feature\",\"geometry\":null,\"properties\":{}}")]
    [InlineData("{\"type\":\"FeatureCollection\",\"features\":[]}")]
    public void Deserialize_UnrepresentedTypes_ThrowNotSupportedException(string json)
    {
        Assert.Throws<NotSupportedException>(() => JsonSerializer.Deserialize<Geometry>(json, Options));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Serialize_NonFiniteCoordinates_ThrowJsonException(double number)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Serialize(new Point(number, 2), Options));
        Assert.Throws<JsonException>(() => JsonSerializer.Serialize(new Point(1, number), Options));
        Assert.Throws<JsonException>(() => JsonSerializer.Serialize(PointZ(1, 2, number), Options));
        Assert.Throws<JsonException>(() => JsonSerializer.Serialize(new MultiPoint([new Point(number, 2)]), Options));
    }

    [Fact]
    public void Serialize_InvalidCompositeGeometries_RejectsInvalidPositionsLinesAndRings()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Serialize(new MultiPoint([Point.Empty]), Options));
        Assert.Throws<JsonException>(() => JsonSerializer.Serialize(new PolyLine([new LineString([new Point(1, 2)])]), Options));
        Assert.Throws<JsonException>(() => JsonSerializer.Serialize(
            new PolyLine(
            [
                new LineString([]),
                new LineString([new Point(1, 2), new Point(3, 4)])
            ]),
            Options));
        Assert.Throws<JsonException>(() => JsonSerializer.Serialize(
            new Polygon(
            [
                new LinearRing([new Point(0, 0), new Point(1, 0), new Point(0, 0)])
            ]),
            Options));
    }

    [Fact]
    public void Deserialize_ConcreteConverter_RejectsOtherGeometryTypes()
    {
        const string point = """{"type":"Point","coordinates":[1,2]}""";
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<MultiPoint>(point, Options));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<PolyLine>(point, Options));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Polygon>(point, Options));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Point>("""{"type":"MultiPoint","coordinates":[[1,2]]}""", Options));
    }

    [Fact]
    public void Factory_OnlyHandlesGeometryClasses_LeavesOtherValuesToSystemTextJson()
    {
        var factory = new GeoJsonConverterFactory();
        foreach (var type in new[] { typeof(Geometry), typeof(Point), typeof(MultiPoint), typeof(PolyLine), typeof(Polygon), typeof(MultiPatch) })
            Assert.True(factory.CanConvert(type));
        Assert.False(factory.CanConvert(typeof(FeatureAttributes)));
        Assert.Equal("{\"Id\":1,\"Name\":\"A\"}", JsonSerializer.Serialize(new FeatureAttributes(1, "A"), Options));
        Assert.Throws<NotSupportedException>(() => factory.CreateConverter(typeof(string), Options));
    }

    public sealed record Envelope(Geometry Geometry, string Label);

    private sealed class BufferSegment : ReadOnlySequenceSegment<byte>
    {
        public BufferSegment(ReadOnlyMemory<byte> memory) => Memory = memory;

        public BufferSegment Append(ReadOnlyMemory<byte> memory)
        {
            var next = new BufferSegment(memory) { RunningIndex = RunningIndex + Memory.Length };
            Next = next;
            return next;
        }
    }
}
