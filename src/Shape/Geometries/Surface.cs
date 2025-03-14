﻿using System.Collections;
using System.Collections.Immutable;

namespace Shape.Geometries;

public readonly record struct Surface(SurfaceType Type, ImmutableArray<Point> Points) : IReadOnlyList<Point>
{
    public Point this[int index] => Points[index];
    public int Count => Points.Length;
    public IEnumerator<Point> GetEnumerator()
    {
        foreach (var point in Points)
            yield return point;
    }
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public bool Equals(Surface other) => Type == other.Type && Points.SequenceEqual(other.Points);
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Type);
        foreach (var point in Points)
            hash.Add(point);
        return hash.ToHashCode();
    }
}

public enum SurfaceType
{
    TriangleStrip,
    TriangleFan,
    OuterRing,
    InnerRing,
    FirstRing,
    Ring
}
