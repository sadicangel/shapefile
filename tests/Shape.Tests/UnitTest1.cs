﻿using System.Diagnostics;

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

        using var shp = Shapefile.Open(path);
        foreach (var (geometry, attributes) in shp.EnumerateRecords())
        {
            Debug.WriteLine(geometry);
            Debug.WriteLine(attributes);
        }
    }
}
