using System.Buffers.Binary;

namespace Shape.Tests.Serialization;

public abstract class SerializerTests
{
    protected static void WriteI32(byte[] buf, int offset, int value) =>
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(offset, 4), value);

    protected static void WriteF64(byte[] buf, int offset, double value) =>
        BinaryPrimitives.WriteDoubleLittleEndian(buf.AsSpan(offset, 8), value);

    protected static int ReadI32(byte[] buf, int offset) =>
        BinaryPrimitives.ReadInt32LittleEndian(buf.AsSpan(offset, 4));

    protected static double ReadF64(byte[] buf, int offset) =>
        BinaryPrimitives.ReadDoubleLittleEndian(buf.AsSpan(offset, 8));
}
