namespace Shape.Geometries;

public readonly record struct BoundingBox(Point Min, Point Max)
{
    public static BoundingBox Empty => new(Point.Empty, Point.Empty);

    public double MinX => Min.X;
    public double MaxX => Max.X;

    public double MinY => Min.Y;
    public double MaxY => Max.Y;

    public double MinZ => Min.Z;
    public double MaxZ => Max.Z;
    public bool HasZ => Min.HasZ && Max.HasZ;

    public double MinM => Min.M;
    public double MaxM => Max.M;
    public bool HasM => Min.HasM && Max.HasM;

    public static BoundingBox FromPoints(IEnumerable<Point> points)
    {
        using var enumerator = points.GetEnumerator();
        if (!enumerator.MoveNext()) return Empty;

        var minX = double.MaxValue;
        var minY = double.MaxValue;
        var minZ = double.MaxValue;
        var minM = double.MaxValue;
        var maxX = double.MinValue;
        var maxY = double.MinValue;
        var maxZ = double.MinValue;
        var maxM = double.MinValue;

        var shapeType = enumerator.Current.ShapeType;
        do
        {
            // Assume all points have the same dimension.
            var (_, x, y, z, m) = enumerator.Current;
            minX = Math.Min(minX, x);
            minY = Math.Min(minY, y);
            minZ = Math.Min(minZ, z);
            minM = Math.Min(minM, m);
            maxX = Math.Max(maxX, x);
            maxY = Math.Max(maxY, y);
            maxZ = Math.Max(maxZ, z);
            maxM = Math.Max(maxM, m);
        } while (enumerator.MoveNext());

        var (min, max) = shapeType switch
        {
            ShapeType.Point => (new Point(minX, minY), new Point(maxX, maxY)),
            ShapeType.PointM => (new Point(minX, minY, minM), new Point(maxX, maxY, maxM)),
            ShapeType.PointZ => (new Point(minX, minY, minZ, minM), new Point(maxX, maxY, maxZ, maxM)),
            _ => throw new InvalidOperationException($"Invalid {nameof(Point)} {nameof(ShapeType)} '{shapeType}'")
        };

        return new BoundingBox(min, max);
    }
}
