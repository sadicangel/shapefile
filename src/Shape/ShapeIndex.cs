
using System.Buffers.Binary;

namespace Shape;
public sealed class ShapeIndex : IDisposable
{
    internal const int HeaderLength = 100;

    private readonly Stream _shx;
    private bool _dirty;

    public int RecordCount => (int)((_shx.Length - HeaderLength) / ShapeIndexRecord.Size);

    public ShapeIndexRecord this[int index] { get => GetRecord(index); }

    private ShapeIndex(Stream shx)
    {
        _shx = shx;
    }

    public static ShapeIndex Open(string fileName) => Open(new FileStream(fileName, FileMode.Open, FileAccess.ReadWrite));

    public static ShapeIndex Open(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        stream.Position = 0;
        Span<byte> header = stackalloc byte[HeaderLength];
        stream.ReadExactly(header);

        if (BinaryPrimitives.ReadInt32BigEndian(header[0..]) != 9994)
            throw new InvalidOperationException("Invalid file format. Not a shapefile index.");

        if (BinaryPrimitives.ReadInt32LittleEndian(header[28..]) != 1000)
            throw new InvalidOperationException($"Invalid version. Expected: '1000'. Actual: '{BinaryPrimitives.ReadInt32BigEndian(header[32..])}'");

        return new ShapeIndex(stream);
    }

    public void Dispose()
    {
        Flush();
        _shx.Dispose();
    }

    public void Flush()
    {
        if (_dirty)
        {
            _dirty = false;
            Span<byte> hLength = stackalloc byte[4];
            BinaryPrimitives.WriteInt32BigEndian(hLength, (100 + RecordCount * ShapeIndexRecord.Size) / 2);
            _shx.Position = 24;
            _shx.Write(hLength);
        }
        _shx.Flush();
    }

    internal void SetStreamPositionForIndex(int index) => _shx.Position = HeaderLength + index * ShapeIndexRecord.Size;

    public ShapeIndexRecord GetRecord(int index)
    {
        if (index < 0 || index >= RecordCount)
            throw new ArgumentOutOfRangeException(nameof(index));

        // TODO: It's probably OK to cache the indices in a list.

        SetStreamPositionForIndex(index);

        Span<byte> buffer = stackalloc byte[ShapeIndexRecord.Size];
        _shx.ReadExactly(buffer);

        var offset = BinaryPrimitives.ReadInt32BigEndian(buffer[0..]);
        var length = BinaryPrimitives.ReadInt32BigEndian(buffer[4..]);

        return new ShapeIndexRecord(offset * 2, length * 2);
    }

    public void Add(ShapeIndexRecord record)
    {
        _dirty = true;
        SetStreamPositionForIndex(RecordCount);
        Span<byte> buffer = stackalloc byte[ShapeIndexRecord.Size];
        BinaryPrimitives.WriteInt32BigEndian(buffer[0..], record.Offset / 2);
        BinaryPrimitives.WriteInt32BigEndian(buffer[4..], record.Length / 2);
        _shx.Write(buffer);
    }

    public IEnumerable<ShapeIndexRecord> EnumerateRecords()
    {
        var count = RecordCount;
        for (var i = 0; i < count; i++)
        {
            yield return GetRecord(i);
        }
    }
}
