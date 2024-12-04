// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using System.Collections;
using System.Collections.Immutable;

namespace Netwolf.Generator;

// not a record because we want structural equality for the collection rather than reference equality by default
internal readonly struct ValueCollection<T> : IEquatable<ValueCollection<T>>, IReadOnlyList<T>, IStructuralEquatable
{
    public readonly ImmutableArray<T> Values;

    public int Count => Values.Length;

    public T this[int index] => Values[index];

    public ValueCollection(ImmutableArray<T> values)
    {
        Values = values;
    }

    public override bool Equals(object obj)
    {
        return obj is ValueCollection<T> other && Equals(other);
    }

    public bool Equals(ValueCollection<T> other)
    {
        return Values.SequenceEqual(other.Values);
    }

    public static bool operator ==(ValueCollection<T> left, ValueCollection<T> right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(ValueCollection<T> left, ValueCollection<T> right)
    {
        return !left.Equals(right);
    }

    public override int GetHashCode()
    {
        return ((IStructuralEquatable)Values).GetHashCode(EqualityComparer<T>.Default);
    }

    public IEnumerator<T> GetEnumerator()
    {
        return ((IEnumerable<T>)Values).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return ((IEnumerable)Values).GetEnumerator();
    }

    bool IStructuralEquatable.Equals(object other, IEqualityComparer comparer)
    {
        return ((IStructuralEquatable)Values).Equals(other, comparer);
    }

    int IStructuralEquatable.GetHashCode(IEqualityComparer comparer)
    {
        return ((IStructuralEquatable)Values).GetHashCode(comparer);
    }

    public static implicit operator ValueCollection<T>(ImmutableArray<T> values)
    {
        return new(values);
    }
}
