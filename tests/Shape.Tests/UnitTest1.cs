using System.Diagnostics;
using Shape.Geometries;

namespace Shape.Tests;

public class UnitTest1
{
    [Fact]
    public void Test1()
    {
#if !DEBUG
        Assert.Skip("Local file path is not available in CI");
#endif

        var path = @"D:\Data\railways.shp";

        var start = Stopwatch.GetTimestamp();

        {
            using var shp1 = Shapefile.Open(path);
            foreach (var (geometry, attributes) in shp1)
            {
                Debug.WriteLine(geometry);
                Debug.WriteLine(attributes);
            }
        }

        Debug.WriteLine(Stopwatch.GetElapsedTime(start));
        start = Stopwatch.GetTimestamp();

        {
            using var shp2 = Shapefile.Open<PolyLine>(path);
            foreach (var (geometry, attributes) in shp2)
            {
                Debug.WriteLine(geometry);
                Debug.WriteLine(attributes);
            }
        }

        Debug.WriteLine(Stopwatch.GetElapsedTime(start));
        start = Stopwatch.GetTimestamp();


        {
            using var shp23 = Shapefile.Open<PolyLine, Attributes>(path);
            foreach (var (geometry, attributes) in shp23)
            {
                Debug.WriteLine(geometry);
                Debug.WriteLine(attributes);
            }
        }

        Debug.WriteLine(Stopwatch.GetElapsedTime(start));
    }

    public record Attributes(long Id, string Name, string Type);
}
