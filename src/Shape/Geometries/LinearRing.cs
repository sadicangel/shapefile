﻿using System.Collections;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace Shape.Geometries;

[CollectionBuilder(typeof(LinearRingBuilder), "Create")]
public readonly record struct LinearRing(ImmutableArray<Point> Points) : IReadOnlyList<Point>
{
    public Point this[int index] => Points[index];

    public int Count => Points.Length;

    public IEnumerator<Point> GetEnumerator()
    {
        foreach (var point in Points)
            yield return point;
    }
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public bool Equals(LinearRing other) => Points.SequenceEqual(other.Points);
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var point in Points)
            hash.Add(point);
        return hash.ToHashCode();
    }
}

internal static class LinearRingBuilder
{
    internal static LinearRing Create(ReadOnlySpan<Point> values) => new(ImmutableArray.Create(values));
}
