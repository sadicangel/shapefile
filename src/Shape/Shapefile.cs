using DBase;
using Shape.Geometries;

namespace Shape;

public abstract class Shapefile<TGeometry, TAttributes>
    where TGeometry : Geometry
{
}

public sealed class Shapefile
{
    public int RecordCount { get; }

    public Dbf Dbf { get; }

    internal Shapefile(Dbf dbf)
    {
        Dbf = dbf;
    }

    public static Shapefile Open(string path)
    {
        throw new NotImplementedException();
    }

    public ShapeRecord<Geometry, DbfRecord> GetRecord(int index)
    {
        // Move stream to the start of the record.
        // Read the record header.
        // Read the record data.
        // TGeometry geometry = TGeometry.Read(data);
        var attributes = Dbf.GetRecord(index);
        // return new ShapeRecord<Geometry, DbfRecord>(geometry, attributes);
        throw new NotImplementedException();
    }

    public ShapeRecord<TGeometry, DbfRecord> GetRecord<TGeometry>(int index)
        where TGeometry : Geometry, IBinaryGeometry<TGeometry>
    {
        // Move stream to the start of the record.
        // Read the record header.
        // Read the record data.
        // TGeometry geometry = TGeometry.Read(data);
        var attributes = Dbf.GetRecord(index);
        // return new ShapeRecord<TGeometry, DbfRecord>(geometry, attributes);
        throw new NotImplementedException();
    }

    public ShapeRecord<TGeometry, TAttributes> GetRecord<TGeometry, TAttributes>(int index)
        where TGeometry : Geometry
    {
        // Move stream to the start of the record.
        // Read the record header.
        // Read the record data.
        // TGeometry geometry = TGeometry.Read(data);
        var attributes = Dbf.GetRecord<TAttributes>(index);
        // return new ShapeRecord<TGeometry, TAttributes>(geometry, attributes);
        throw new NotImplementedException();
    }

    public IEnumerable<ShapeRecord<Geometry, DbfRecord>> EnumerateRecords()
    {
        for (var i = 0; i < RecordCount; ++i)
            yield return GetRecord<Geometry, DbfRecord>(i);
    }

    public IEnumerable<ShapeRecord<TGeometry, DbfRecord>> EnumerateRecords<TGeometry>()
        where TGeometry : Geometry, IBinaryGeometry<TGeometry>
    {
        for (var i = 0; i < RecordCount; ++i)
            yield return GetRecord<TGeometry>(i);
    }

    public IEnumerable<ShapeRecord<TGeometry, TAttributes>> EnumerateRecords<TGeometry, TAttributes>()
        where TGeometry : Geometry
    {
        for (var i = 0; i < RecordCount; ++i)
            yield return GetRecord<TGeometry, TAttributes>(i);
    }
}

public sealed record class ShapeRecord<TGeometry, TAttributes>(TGeometry Geometry, TAttributes Attributes)
    where TGeometry : Geometry;
