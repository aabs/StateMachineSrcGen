using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace StateMachineSrcGen;

/// <summary>
/// Wrapper around ImmutableArray that provides value equality for incremental caching.
/// Two instances are equal if and only if they contain the same elements in the same order.
/// </summary>
public readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IEnumerable<T>
    where T : IEquatable<T>
{
    private readonly ImmutableArray<T> _array;

    /// <summary>
    /// Initializes a new instance wrapping the specified immutable array.
    /// </summary>
    public EquatableArray(ImmutableArray<T> array)
    {
        _array = array;
    }

    /// <summary>
    /// Gets the underlying array, returning an empty array if uninitialized.
    /// </summary>
    private ImmutableArray<T> Array => _array.IsDefault ? ImmutableArray<T>.Empty : _array;

    /// <summary>
    /// Determines whether this instance is equal to another by comparing elements in order.
    /// </summary>
    public bool Equals(EquatableArray<T> other)
    {
        var thisArray = Array;
        var otherArray = other.Array;

        if (thisArray.Length != otherArray.Length)
            return false;

        for (int i = 0; i < thisArray.Length; i++)
        {
            if (!thisArray[i].Equals(otherArray[i]))
                return false;
        }

        return true;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        return obj is EquatableArray<T> other && Equals(other);
    }

    /// <summary>
    /// Computes a hash code by combining element hashes.
    /// </summary>
    public override int GetHashCode()
    {
        var array = Array;

        unchecked
        {
            int hash = 17;
            for (int i = 0; i < array.Length; i++)
            {
                hash = hash * 31 + array[i].GetHashCode();
            }
            return hash;
        }
    }

    /// <summary>
    /// Returns an enumerator that iterates through the elements.
    /// </summary>
    public IEnumerator<T> GetEnumerator()
    {
        return ((IEnumerable<T>)Array).GetEnumerator();
    }

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    /// <summary>
    /// Determines whether two instances are equal.
    /// </summary>
    public static bool operator ==(EquatableArray<T> left, EquatableArray<T> right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Determines whether two instances are not equal.
    /// </summary>
    public static bool operator !=(EquatableArray<T> left, EquatableArray<T> right)
    {
        return !left.Equals(right);
    }
}
