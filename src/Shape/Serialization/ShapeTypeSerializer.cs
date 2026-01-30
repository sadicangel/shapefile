using System.Buffers.Binary;
using Shape.Geometries;

namespace Shape.Serialization;

internal interface IShapeTypeOffsets<T> where T : allows ref struct
{
    public int ShapeType { get; }
}

internal static class ShapeTypeSerializer
{
    public static void Serialize<TOffsets>(Span<byte> target, TOffsets offsets, Geometry geometry)
        where TOffsets : IShapeTypeOffsets<TOffsets>, allows ref struct =>
        BinaryPrimitives.WriteInt32LittleEndian(target[offsets.ShapeType..], (int)geometry.ShapeType);

    public static ShapeType Deserialize<TOffsets>(ReadOnlySpan<byte> source, TOffsets offsets)
        where TOffsets : IShapeTypeOffsets<TOffsets>, allows ref struct =>
        (ShapeType)BinaryPrimitives.ReadInt32LittleEndian(source[offsets.ShapeType..]);
}
