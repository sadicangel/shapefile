namespace Shape;

public enum Dimension
{
    None,
    Xy = 2,
    Xym = 3,
    Xyzm = 4,
}

internal static class DimensionExtensions
{
    extension(Dimension dimension)
    {
        public int PointByteSize => 8 * (int)dimension;

        public int BoundingBoxSize => 2 * (int)dimension * dimension.PointByteSize;
    }
}
