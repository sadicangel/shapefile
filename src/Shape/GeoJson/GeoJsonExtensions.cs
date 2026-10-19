using System.Collections.Immutable;
using System.Text.Json;
using Shape.Geometries;

namespace Shape.GeoJson;

internal static class GeoJsonExtensions
{
    extension(ref Utf8JsonReader reader)
    {
        // Keep a reader positioned at coordinates while scanning the complete object.
        // This supports either property order without a DOM or serializing JSON back to bytes.
        public void ReadGeometryObject(out string type, out Utf8JsonReader coordinates, bool requireCoordinates = true)
        {
            if (reader.TokenType is not JsonTokenType.StartObject)
                throw new JsonException("Expected a GeoJSON geometry object.");

            string? typeName = null;
            var hasCoordinates = false;
            coordinates = default;
            while (reader.Read())
            {
                if (reader.TokenType is JsonTokenType.EndObject)
                {
                    if (typeName is null || (requireCoordinates && !hasCoordinates))
                        throw new JsonException("A geometry requires type and coordinates members.");
                    type = typeName;
                    return;
                }
                if (reader.TokenType is not JsonTokenType.PropertyName)
                    throw new JsonException();

                if (reader.ValueTextEquals(GeoJsonPropertyNames.Type))
                {
                    if (typeName is not null || !reader.Read() || reader.TokenType is not JsonTokenType.String)
                        throw new JsonException("The geometry type must be specified once as a string.");
                    typeName = reader.GetString();
                }
                else if (reader.ValueTextEquals(GeoJsonPropertyNames.Coordinates))
                {
                    if (hasCoordinates || !reader.Read() || reader.TokenType is not JsonTokenType.StartArray)
                        throw new JsonException("Geometry coordinates must be specified once as an array.");
                    coordinates = reader;
                    hasCoordinates = true;
                    if (!reader.TrySkip()) throw new JsonException("Incomplete geometry coordinates.");
                }
                else
                {
                    if (!reader.TrySkip()) throw new JsonException("Incomplete geometry member.");
                }
            }
            throw new JsonException();
        }

        private Point ReadCoordinate(scoped Span<double> coordinates)
        {
            if (reader.TokenType is not JsonTokenType.StartArray)
                throw new JsonException();

            var count = 0;
            while (reader.Read())
            {
                if (reader.TokenType is JsonTokenType.EndArray)
                    return count switch
                    {
                        0 => Point.Empty,
                        2 => new Point(coordinates[0], coordinates[1]),
                        3 => new Point(coordinates[0], coordinates[1], coordinates[2], NumericLimits.NoValue),
                        _ => throw new JsonException()
                    };

                if (reader.TokenType is not JsonTokenType.Number || count >= coordinates.Length)
                    throw new JsonException();

                var value = reader.GetDouble();
                if (!double.IsFinite(value))
                    throw new JsonException();
                coordinates[count++] = value;
            }
            throw new JsonException();
        }

        public Point ReadCoordinate() => reader.ReadCoordinate(stackalloc double[3]);

        private ImmutableArray<Point> ReadCoordinates(scoped Span<double> coordinates)
        {
            if (reader.TokenType is not JsonTokenType.StartArray)
                throw new JsonException();

            var points = ImmutableArray.CreateBuilder<Point>();
            while (reader.Read())
            {
                if (reader.TokenType is JsonTokenType.EndArray)
                    return points.DrainToImmutable();

                var point = reader.ReadCoordinate(coordinates);
                if (point.ShapeType is ShapeType.Null)
                    throw new JsonException("An embedded position must contain two or three numbers.");
                points.Add(point);
            }
            throw new JsonException();
        }

        public ImmutableArray<Point> ReadCoordinates() => reader.ReadCoordinates(stackalloc double[3]);

        public ImmutableArray<ImmutableArray<Point>> ReadCoordinatesArray()
        {
            if (reader.TokenType is not JsonTokenType.StartArray)
                throw new JsonException();

            Span<double> coordinates = stackalloc double[3];
            var points = ImmutableArray.CreateBuilder<ImmutableArray<Point>>();
            while (reader.Read())
            {
                if (reader.TokenType is JsonTokenType.EndArray)
                    return points.DrainToImmutable();

                points.Add(reader.ReadCoordinates(coordinates));
            }
            throw new JsonException();
        }
    }

    extension(Utf8JsonWriter writer)
    {
        public void WriteCoordinate(Point point)
        {
            writer.WriteStartArray();
            if (point.ShapeType is not ShapeType.Null)
            {
                if (!double.IsFinite(point.X) || !double.IsFinite(point.Y) || !double.IsFinite(point.Z))
                    throw new JsonException("GeoJSON positions must contain finite numbers.");
                writer.WriteNumberValue(point.X);
                writer.WriteNumberValue(point.Y);
                if (point.HasZ) writer.WriteNumberValue(point.Z);
            }
            writer.WriteEndArray();
        }

        public void WriteCoordinates(IEnumerable<Point> points)
        {
            writer.WriteStartArray();
            foreach (var point in points)
            {
                if (point is null || point.ShapeType is ShapeType.Null)
                    throw new JsonException("An embedded position cannot be empty.");
                writer.WriteCoordinate(point);
            }
            writer.WriteEndArray();
        }

        public void WriteCoordinatesArray(IEnumerable<LineString> lineStrings)
        {
            writer.WriteStartArray();
            foreach (var lineString in lineStrings)
                writer.WriteCoordinates(lineString);
            writer.WriteEndArray();
        }

        public void WriteCoordinatesArray(IEnumerable<LinearRing> linearRings)
        {
            writer.WriteStartArray();
            var exterior = true;
            foreach (var linearRing in linearRings)
            {
                // GeoJSON uses the opposite exterior winding to the shapefile format.
                // Read either winding, but emit counterclockwise exteriors and clockwise holes.
                double area = 0;
                for (var index = 1; index < linearRing.Length; index++)
                    area += linearRing[index - 1].X * linearRing[index].Y - linearRing[index].X * linearRing[index - 1].Y;
                if ((exterior && area < 0) || (!exterior && area > 0))
                    writer.WriteCoordinates(linearRing.Reverse());
                else
                    writer.WriteCoordinates(linearRing);
                exterior = false;
            }
            writer.WriteEndArray();
        }
    }

    public static void ValidateLine(ImmutableArray<Point> points, bool allowEmpty = true)
    {
        if (points.Length < 2 && (!allowEmpty || points.Length != 0))
            throw new JsonException("A LineString requires at least two positions.");
    }

    public static void ValidateRing(ImmutableArray<Point> points)
    {
        if (points.Length < 4 || !SamePosition(points[0], points[^1]))
            throw new JsonException("A polygon ring requires at least four positions and must be closed.");
    }

    private static bool SamePosition(Point first, Point second) =>
        first.X == second.X && first.Y == second.Y && first.HasZ == second.HasZ &&
        (!first.HasZ || first.Z == second.Z);
}
