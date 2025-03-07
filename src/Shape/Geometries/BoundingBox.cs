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
        var minX = double.MaxValue;
        var minY = double.MaxValue;
        var minZ = double.MaxValue;
        var minM = double.MaxValue;
        var maxX = double.MinValue;
        var maxY = double.MinValue;
        var maxZ = double.MinValue;
        var maxM = double.MinValue;
        foreach (var point in points)
        {
            minX = Math.Min(minX, point.X);
            minY = Math.Min(minY, point.Y);
            minZ = Math.Min(minZ, point.Z);
            minM = Math.Min(minM, point.M);
            maxX = Math.Max(maxX, point.X);
            maxY = Math.Max(maxY, point.Y);
            maxZ = Math.Max(maxZ, point.Z);
            maxM = Math.Max(maxM, point.M);
        }
        return new BoundingBox(new Point(minX, minY, minZ, minM), new Point(maxX, maxY, maxZ, maxM));
    }
}
