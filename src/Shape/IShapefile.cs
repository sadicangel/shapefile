using DBase;
using Shape.Geometries;

namespace Shape;

public interface IShapefile
{
    ShapeType ShapeType { get; }
    int RecordCount { get; }

    ShapeRecord<Geometry, DbfRecord> GetRecord(int index);

    IEnumerator<ShapeRecord<Geometry, DbfRecord>> GetEnumerator()
    {
        var recordCount = RecordCount;
        for (var i = 0; i < recordCount; ++i)
            yield return GetRecord(i);
    }
}
