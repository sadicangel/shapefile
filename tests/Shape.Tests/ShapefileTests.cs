using System.Buffers.Binary;
using System.Collections;
using System.Collections.Immutable;
using DBase;
using Shape.Geometries;

namespace Shape.Tests;

public sealed class ShapefileTests
{
    [Fact]
    public void GetRecord_AllRepresentations_ReadSamePhysicalPair()
    {
        var point = new Point(1.25, -2.5);
        using var fixture = new ShapefileFixture(ShapeType.Point, point);
        using var file = Shapefile.Open(fixture.Path);

        var raw = file.GetRecord(0);
        var geometryTyped = file.GetRecord<Point>(0);
        var attributesTyped = file.GetRecord<Geometry, FeatureAttributes>(0);
        var typed = file.GetRecord<Point, FeatureAttributes>(0);

        Assert.Equal(point, raw.Geometry);
        Assert.Equal(raw.Geometry, geometryTyped.Geometry);
        Assert.Equal(raw.Attributes, geometryTyped.Attributes);
        Assert.Equal(point, attributesTyped.Geometry);
        Assert.Equal(ShapefileFixture.AttributesAt(0), attributesTyped.Attributes);
        Assert.Equal(point, typed.Geometry);
        Assert.Equal(attributesTyped.Attributes, typed.Attributes);
    }

    [Fact]
    public void EnumerateRecords_AllRepresentations_MatchIndexedReadsAndCanBeRepeated()
    {
        using var fixture = new ShapefileFixture(ShapeType.Point, new Point(1, 2), new Point(3, 4));
        using var file = Shapefile.Open(fixture.Path);
        var typed = file.EnumerateRecords<Point, FeatureAttributes>();

        Assert.Equal(typed.ToArray(), typed.ToArray());
        Assert.Equal(file.EnumerateRecords().ToArray(), file.ToArray());
        Assert.Equal(file.ToArray(), ((IEnumerable)file).Cast<ShapeRecord<Geometry, DbfRecord>>().ToArray());
        for (var index = 0; index < file.RecordCount; index++)
        {
            Assert.Equal(file.GetRecord(index), file.EnumerateRecords().ElementAt(index));
            Assert.Equal(file.GetRecord<Point>(index), file.EnumerateRecords<Point>().ElementAt(index));
            Assert.Equal(file.GetRecord<Geometry, FeatureAttributes>(index),
                file.EnumerateRecords<Geometry, FeatureAttributes>().ElementAt(index));
            Assert.Equal(file.GetRecord<Point, FeatureAttributes>(index), typed.ElementAt(index));
        }
    }

    [Fact]
    public void EnumerateRecords_InterleavedEnumerators_KeepIndependentIndices()
    {
        using var fixture = new ShapefileFixture(ShapeType.Point, new Point(1, 2), new Point(3, 4));
        using var file = Shapefile.Open(fixture.Path);
        using var raw = file.EnumerateRecords().GetEnumerator();
        using var typed = file.EnumerateRecords<Point, FeatureAttributes>().GetEnumerator();

        Assert.True(raw.MoveNext());
        Assert.True(typed.MoveNext());
        Assert.Equal(raw.Current.Geometry, typed.Current.Geometry);
        Assert.True(typed.MoveNext());
        Assert.True(raw.MoveNext());
        Assert.Equal(raw.Current.Geometry, typed.Current.Geometry);
    }

    [Fact]
    public void EnumerateRecords_LazyStart_CapturesCountOnFirstMoveNext()
    {
        using var fixture = new ShapefileFixture(ShapeType.Point);
        using var file = Shapefile.Open(fixture.Path);
        var sequence = file.EnumerateRecords<Point, FeatureAttributes>();
        file.Add(new Point(1, 2), ShapefileFixture.AttributesAt(0));

        using var iterator = sequence.GetEnumerator();
        Assert.True(iterator.MoveNext());
        file.Add(new Point(3, 4), ShapefileFixture.AttributesAt(1));
        Assert.False(iterator.MoveNext());
        Assert.Equal(2, sequence.Count());
    }

    [Fact]
    public void Reads_DeletedDbfRow_PreservePhysicalPairing()
    {
        using var fixture = new ShapefileFixture(ShapeType.Point, new Point(1, 2), new Point(3, 4));
        fixture.MarkDeleted(0);
        using var file = Shapefile.Open(fixture.Path);

        Assert.Equal(2, file.EnumerateRecords().Count());
        Assert.True(file.GetRecord(0).Attributes.IsDeleted);
        Assert.Equal(ShapefileFixture.AttributesAt(0), file.GetRecord<Point, FeatureAttributes>(0).Attributes);
        Assert.Equal(ShapefileFixture.AttributesAt(1), file.GetRecord<Point, FeatureAttributes>(1).Attributes);
    }

    [Fact]
    public void Reads_IncompatibleGeometry_RejectIndexedReadAndEvenEmptyEnumeration()
    {
        using var fixture = new ShapefileFixture(ShapeType.Point);
        using var file = Shapefile.Open(fixture.Path);
        Assert.Throws<InvalidOperationException>(() => file.GetRecord<Polygon>(0));
        var sequence = file.EnumerateRecords<Polygon, FeatureAttributes>();
        Assert.Throws<InvalidOperationException>(() => sequence.ToArray());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public void GetRecord_InvalidIndex_Throws(int index)
    {
        using var fixture = new ShapefileFixture(ShapeType.Point, new Point(1, 2));
        using var file = Shapefile.Open(fixture.Path);
        Assert.Throws<ArgumentOutOfRangeException>(() => file.GetRecord(index));
        Assert.Throws<ArgumentOutOfRangeException>(() => file.GetRecord<Point, FeatureAttributes>(index));
    }

    [Fact]
    public void Reads_NullShapes_UseSameFamilyEmptyInTypedAndUntypedAccess()
    {
        using var fixture = new ShapefileFixture(ShapeType.Polygon, Point.Empty);
        using var file = Shapefile.Open(fixture.Path);
        Assert.Equal(Polygon.Empty, file.GetRecord(0).Geometry);
        Assert.Equal(Polygon.Empty, file.GetRecord<Polygon>(0).Geometry);
        Assert.Equal(Polygon.Empty, file.EnumerateRecords<Geometry, FeatureAttributes>().Single().Geometry);
    }

    [Fact]
    public void Reads_NullOnlyFile_UsesPointEmptyForUntypedAccess()
    {
        using var fixture = new ShapefileFixture(ShapeType.Null, Point.Empty);
        using var file = Shapefile.Open(fixture.Path);
        Assert.Equal(Point.Empty, file.GetRecord(0).Geometry);
    }

    [Fact]
    public void GetRecord_AllConcreteGeometryFamilies_ResolveTheirCodecs()
    {
        AssertFamily(new Point(1, 2));
        AssertFamily(new PolyLine([new LineString([new Point(1, 2), new Point(3, 4)])]));
        AssertFamily(new Polygon([new LinearRing([new Point(0, 0), new Point(0, 2), new Point(2, 0), new Point(0, 0)])]));
        AssertFamily(new MultiPoint([new Point(1, 2), new Point(3, 4)]));
        AssertFamily(new MultiPatch([new Surface(SurfaceType.TriangleStrip,
            [new Point(0, 0, 1, 2), new Point(0, 2, 1, 2), new Point(2, 0, 1, 2)])]));
    }

    private static void AssertFamily<TGeometry>(TGeometry geometry) where TGeometry : Geometry
    {
        using var fixture = new ShapefileFixture(geometry.ShapeType, geometry);
        using var file = Shapefile.Open(fixture.Path);
        Assert.Equal(geometry, file.GetRecord<TGeometry>(0).Geometry);
        Assert.Equal(geometry, file.GetRecord(0).Geometry);
    }

    [Fact]
    public void Add_TypedAndUntypedAfterReadingAndFlushing_PreservesPairsAndHeadersOnReopen()
    {
        var first = new Point(1, 2);
        var second = new Point(-3, 5);
        var third = new Point(8, -4);
        using var fixture = new ShapefileFixture(ShapeType.Point, first);
        using (var file = Shapefile.Open(fixture.Path))
        {
            var raw = file.GetRecord(0);
            file.Add(second, ShapefileFixture.AttributesAt(1));
            file.Flush();
            _ = file.GetRecord<Point>(0);
            file.Add(new ShapeRecord<Geometry, DbfRecord>(third, raw.Attributes));
            Assert.Equal(3, file.RecordCount);
            Assert.Equal(second, file.GetRecord<Point>(1).Geometry);
        }

        var shp = File.ReadAllBytes(fixture.Path);
        var shx = File.ReadAllBytes(fixture.IndexPath);
        Assert.Equal(184, shp.Length); // 100 + three (8-byte header + 20-byte Point) records.
        Assert.Equal(124, shx.Length);
        Assert.Equal(shp.Length / 2, BinaryPrimitives.ReadInt32BigEndian(shp.AsSpan(24)));
        Assert.Equal(shx.Length / 2, BinaryPrimitives.ReadInt32BigEndian(shx.AsSpan(24)));
        Assert.All(shp.AsSpan(4, 20).ToArray(), value => Assert.Equal(0, value));
        Assert.Equal(shp.AsSpan(32, 68).ToArray(), shx.AsSpan(32, 68).ToArray());
        for (var index = 0; index < 3; index++)
        {
            var offset = 100 + index * 28;
            Assert.Equal(index + 1, BinaryPrimitives.ReadInt32BigEndian(shp.AsSpan(offset)));
            Assert.Equal(10, BinaryPrimitives.ReadInt32BigEndian(shp.AsSpan(offset + 4)));
            Assert.Equal(offset / 2, BinaryPrimitives.ReadInt32BigEndian(shx.AsSpan(100 + index * 8)));
            Assert.Equal(10, BinaryPrimitives.ReadInt32BigEndian(shx.AsSpan(104 + index * 8)));
        }
        using var reopened = Shapefile.Open(fixture.Path);
        Assert.Equal(new[] { first, second, third }, reopened.EnumerateRecords<Point>().Select(record => record.Geometry).ToArray());
        Assert.Equal(new FeatureAttributes[] { ShapefileFixture.AttributesAt(0), ShapefileFixture.AttributesAt(1), ShapefileFixture.AttributesAt(0) },
            reopened.EnumerateRecords<Point, FeatureAttributes>().Select(record => record.Attributes).ToArray());
        Assert.Equal(new Point(-3, -4), reopened.BoundingBox.Min);
        Assert.Equal(new Point(8, 5), reopened.BoundingBox.Max);
    }

    [Fact]
    public void Add_EmptyFileAndNullShape_RoundTripsWithoutExpandingBoundsForNull()
    {
        using var fixture = new ShapefileFixture(ShapeType.Point);
        using (var file = Shapefile.Open(fixture.Path))
        {
            file.Add(Point.Empty, ShapefileFixture.AttributesAt(0));
            file.Add(new Point(7, 9), ShapefileFixture.AttributesAt(1));
        }
        using var reopened = Shapefile.Open(fixture.Path);
        Assert.Equal(Point.Empty, reopened.GetRecord(0).Geometry);
        Assert.Equal(new Point(7, 9), reopened.BoundingBox.Min);
        Assert.Equal(reopened.BoundingBox.Min, reopened.BoundingBox.Max);
        Assert.Equal(2, reopened.RecordCount);
    }

    [Fact]
    public void Add_IncompatibleGeometryOrAttributes_LeavesFilesUnchanged()
    {
        using var fixture = new ShapefileFixture(ShapeType.Point, new Point(1, 2));
        var originalShape = File.ReadAllBytes(fixture.Path);
        var originalIndex = File.ReadAllBytes(fixture.IndexPath);
        using (var file = Shapefile.Open(fixture.Path))
        {
            Assert.Throws<ArgumentException>(() => file.Add(new Point(1, 2, 3), ShapefileFixture.AttributesAt(1)));
            Assert.Throws<InvalidOperationException>(() => file.Add(Polygon.Empty, ShapefileFixture.AttributesAt(1)));
            Assert.Throws<ArgumentNullException>(() => file.Add<Point, FeatureAttributes>(null!, ShapefileFixture.AttributesAt(1)));
            Assert.Throws<InvalidOperationException>(() => file.Add(new Point(3, 4), new { Unexpected = 1 }));
            Assert.Equal(1, file.RecordCount);
            Assert.Equal(new Point(1, 2), file.GetRecord(0).Geometry);
        }
        Assert.Equal(originalShape, File.ReadAllBytes(fixture.Path));
        Assert.Equal(originalIndex, File.ReadAllBytes(fixture.IndexPath));
        using var reopened = Shapefile.Open(fixture.Path);
        Assert.Equal(1, reopened.RecordCount);
    }

    [Fact]
    public void Dispose_AllOperationsAndPendingEnumerators_RejectFurtherAccess()
    {
        using var fixture = new ShapefileFixture(ShapeType.Point, new Point(1, 2), new Point(3, 4));
        var file = Shapefile.Open(fixture.Path);
        var sequence = file.EnumerateRecords<Point, FeatureAttributes>();
        using var iterator = sequence.GetEnumerator();
        Assert.True(iterator.MoveNext());
        file.Dispose();
        file.Dispose();
        Assert.Throws<ObjectDisposedException>(() => file.GetRecord(0));
        Assert.Throws<ObjectDisposedException>(() => file.GetRecord<Point>(0));
        Assert.Throws<ObjectDisposedException>(() => file.RecordCount);
        Assert.Throws<ObjectDisposedException>(() => sequence.ToArray());
        Assert.Throws<ObjectDisposedException>(() => iterator.MoveNext());
        Assert.Throws<ObjectDisposedException>(() => file.Add(new Point(5, 6), ShapefileFixture.AttributesAt(2)));
        Assert.Throws<ObjectDisposedException>(() => file.Flush());
    }

    [Fact]
    public void Open_MismatchedRecordCounts_RejectsFileAndReleasesHandles()
    {
        using var fixture = new ShapefileFixture(ShapeType.Point, new Point(1, 2));
        using (var dbf = Dbf.Open(fixture.DbfPath)) dbf.Add(ShapefileFixture.AttributesAt(1));
        Assert.Throws<InvalidDataException>(() => Shapefile.Open(fixture.Path));
        using var shp = File.Open(fixture.Path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        using var shx = File.Open(fixture.IndexPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        using var attributes = File.Open(fixture.DbfPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }

    [Fact]
    public void Open_InvalidIndexHeader_ReleasesShapeAndIndexHandles()
    {
        using var fixture = new ShapefileFixture(ShapeType.Point, new Point(1, 2));
        ShapefileFixture.WriteInt32(fixture.IndexPath, 0, 0);
        Assert.Throws<InvalidOperationException>(() => Shapefile.Open(fixture.Path));
        using var shp = File.Open(fixture.Path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        using var shx = File.Open(fixture.IndexPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }

    [Theory]
    [InlineData(100, 99, true)] // Wrong record number.
    [InlineData(104, 9, true)] // Length disagrees with the index.
    [InlineData(108, (int)ShapeType.PointM, false)] // Geometry disagrees with the file's exact type.
    public void GetRecord_InvalidRecordHeaderOrType_RejectsMalformedData(int offset, int value, bool bigEndian)
    {
        using var fixture = new ShapefileFixture(ShapeType.Point, new Point(1, 2));
        ShapefileFixture.WriteInt32(fixture.Path, offset, value, bigEndian);
        using var file = Shapefile.Open(fixture.Path);
        Assert.Throws<InvalidDataException>(() => file.GetRecord(0));
        Assert.Throws<InvalidDataException>(() => file.GetRecord<Point>(0));
    }

    [Fact]
    public void GetRecord_IndexOutsideShapeFile_RejectsMalformedData()
    {
        using var fixture = new ShapefileFixture(ShapeType.Point, new Point(1, 2));
        ShapefileFixture.WriteInt32(fixture.IndexPath, 100, 500, bigEndian: true);
        using var file = Shapefile.Open(fixture.Path);
        Assert.Throws<InvalidDataException>(() => file.GetRecord(0));
    }

    [Theory]
    [InlineData(ShapeType.PointM)]
    [InlineData(ShapeType.PointZ)]
    public void Add_DimensionalPoints_PreservesDimensionsAndExtents(ShapeType shapeType)
    {
        var first = shapeType == ShapeType.PointM ? new Point(1, 2, 4) : new Point(1, 2, 3, 4);
        var second = shapeType == ShapeType.PointM ? new Point(5, 6, 8) : new Point(5, 6, 7, 8);
        using var fixture = new ShapefileFixture(shapeType, first);
        using (var file = Shapefile.Open(fixture.Path))
            file.Add(second, ShapefileFixture.AttributesAt(1));

        using var reopened = Shapefile.Open(fixture.Path);
        Assert.Equal(new[] { first, second }, reopened.EnumerateRecords<Point>().Select(record => record.Geometry).ToArray());
        Assert.Equal(first, reopened.BoundingBox.Min);
        Assert.Equal(second, reopened.BoundingBox.Max);
    }

    [Theory]
    [InlineData(ShapeType.PointM)]
    [InlineData(ShapeType.PointZ)]
    public void Add_MissingPointMeasure_PreservesNoDataOnReopen(ShapeType shapeType)
    {
        var point = shapeType == ShapeType.PointM
            ? new Point(1, 2, NumericLimits.NoValue)
            : new Point(1, 2, 3, NumericLimits.NoValue);
        using var fixture = new ShapefileFixture(shapeType);
        using (var file = Shapefile.Open(fixture.Path))
            file.Add(point, ShapefileFixture.AttributesAt(0));

        using var reopened = Shapefile.Open(fixture.Path);
        Assert.Equal(point, reopened.GetRecord<Point>(0).Geometry);
        Assert.Equal(point, reopened.BoundingBox.Min);
        Assert.Equal(point, reopened.BoundingBox.Max);
    }

    [Fact]
    public void Add_LargePolygonAndAllAttributeRepresentations_RoundTripsThroughPooledBuffers()
    {
        var points = Enumerable.Range(0, 32)
            .Select(index => new Point(Math.Cos(index * Math.PI / 16), Math.Sin(index * Math.PI / 16))).ToArray();
        var polygon = new Polygon([new LinearRing(points.Append(points[0]).ToImmutableArray())]);
        using var fixture = new ShapefileFixture(ShapeType.Polygon, polygon);
        using (var file = Shapefile.Open(fixture.Path))
        {
            var raw = file.GetRecord(0);
            var typed = file.EnumerateRecords<Polygon, FeatureAttributes>().Single();
            file.Add(polygon, ShapefileFixture.AttributesAt(1));
            file.Add(polygon, raw.Attributes);
            Geometry baseGeometry = polygon;
            file.Add(baseGeometry, ShapefileFixture.AttributesAt(3));
            file.Add(new ShapeRecord<Geometry, DbfRecord>(baseGeometry, raw.Attributes));
            Assert.Equal(polygon, typed.Geometry);
        }
        using var reopened = Shapefile.Open(fixture.Path);
        var records = reopened.EnumerateRecords<Polygon, FeatureAttributes>().ToArray();
        Assert.Equal(5, records.Length);
        Assert.All(records, record => Assert.Equal(polygon, record.Geometry));
        Assert.Equal(new long[] { 1, 2, 1, 4, 1 }, records.Select(record => record.Attributes.Id).ToArray());
    }

    [Fact]
    public void Add_ReopenedFileWithOnlyNullShapes_IgnoresUnspecifiedPreviousExtents()
    {
        using var fixture = new ShapefileFixture(ShapeType.Point, Point.Empty);
        // Other writers may store zeroes for the unspecified bounds of a null-only dataset.
        var bytes = File.ReadAllBytes(fixture.Path);
        bytes.AsSpan(36, 64).Clear();
        File.WriteAllBytes(fixture.Path, bytes);
        using (var file = Shapefile.Open(fixture.Path))
            file.Add(new Point(7, 9), ShapefileFixture.AttributesAt(1));

        using var reopened = Shapefile.Open(fixture.Path);
        Assert.Equal(new Point(7, 9), reopened.BoundingBox.Min);
        Assert.Equal(reopened.BoundingBox.Min, reopened.BoundingBox.Max);
    }

    [Fact]
    public void Add_BaseTypedGeometryWithMismatchedRuntimeFamily_RejectsBeforeWriting()
    {
        using var fixture = new ShapefileFixture(ShapeType.Polygon);
        using var file = Shapefile.Open(fixture.Path);
        Geometry geometry = new Point(1, 2) { ShapeType = ShapeType.Polygon };
        Assert.Throws<InvalidOperationException>(() => file.Add(geometry, ShapefileFixture.AttributesAt(0)));
        Assert.Equal(0, file.RecordCount);
    }
}
