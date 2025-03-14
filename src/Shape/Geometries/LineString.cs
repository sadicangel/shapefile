﻿using System.Collections;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace Shape.Geometries;

[CollectionBuilder(typeof(LineStringBuilder), "Create")]
public readonly record struct LineString(ImmutableArray<Point> Points) : IReadOnlyList<Point>
{
    public Point this[int index] => Points[index];

    public int Count => Points.Length;

    public IEnumerator<Point> GetEnumerator()
    {
        foreach (var point in Points)
            yield return point;
    }
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public bool Equals(LineString other) => Points.SequenceEqual(other.Points);
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var point in Points)
            hash.Add(point);
        return hash.ToHashCode();
    }
}

internal static class LineStringBuilder
{
    internal static LineString Create(ReadOnlySpan<Point> values) => new(ImmutableArray.Create(values));
}
