namespace Shape;

public readonly record struct ShapeIndexRecord(int Offset, int Length)
{
    public const int Size = 8;
}
