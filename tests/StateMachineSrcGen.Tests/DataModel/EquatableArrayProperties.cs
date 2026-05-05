// Feature: state-machine-source-generator, EquatableArray correctness
// **Validates: Requirements 10.2**

using System;
using System.Collections.Immutable;
using System.Linq;
using FsCheck;
using FsCheck.Xunit;
using StateMachineSrcGen;

namespace StateMachineSrcGen.Tests.DataModel;

/// <summary>
/// Property tests for EquatableArray&lt;T&gt; correctness.
/// Validates element-wise equality, hash code consistency, and IEnumerable implementation.
/// </summary>
public class EquatableArrayProperties
{
    // ─── Element-wise Equality ─────────────────────────────────────────────────

    [Property]
    public bool SameElements_SameOrder_AreEqual(int[] elements)
    {
        var array = ImmutableArray.Create(elements ?? Array.Empty<int>());
        var a = new EquatableArray<int>(array);
        var b = new EquatableArray<int>(array);

        return a.Equals(b) && b.Equals(a) && a == b;
    }

    [Property]
    public bool SameElements_SameOrder_AreEqual_Strings(string[] elements)
    {
        // Filter out nulls since string implements IEquatable<string> but null elements
        // would complicate equality semantics
        var filtered = (elements ?? Array.Empty<string>()).Where(e => e != null).ToArray();
        var array = ImmutableArray.Create(filtered);
        var a = new EquatableArray<string>(array);
        var b = new EquatableArray<string>(array);

        return a.Equals(b) && b.Equals(a) && a == b;
    }

    // ─── Inequality on Different Elements ──────────────────────────────────────

    [Property]
    public bool DifferentElements_AreNotEqual(int[] elements1, int[] elements2)
    {
        var arr1 = elements1 ?? Array.Empty<int>();
        var arr2 = elements2 ?? Array.Empty<int>();

        // Skip if arrays happen to be equal
        if (arr1.SequenceEqual(arr2)) return true;

        var a = new EquatableArray<int>(ImmutableArray.Create(arr1));
        var b = new EquatableArray<int>(ImmutableArray.Create(arr2));

        return !a.Equals(b) && a != b;
    }

    [Property]
    public bool DifferentSingleElement_AreNotEqual(int x, int y)
    {
        if (x == y) return true;

        var a = new EquatableArray<int>(ImmutableArray.Create(x));
        var b = new EquatableArray<int>(ImmutableArray.Create(y));

        return !a.Equals(b) && a != b;
    }

    // ─── Inequality on Different Lengths ───────────────────────────────────────

    [Property]
    public bool DifferentLengths_AreNotEqual(int[] elements, int extra)
    {
        var arr = elements ?? Array.Empty<int>();
        var longer = arr.Append(extra).ToArray();

        var a = new EquatableArray<int>(ImmutableArray.Create(arr));
        var b = new EquatableArray<int>(ImmutableArray.Create(longer));

        return !a.Equals(b) && a != b;
    }

    [Property]
    public bool EmptyVsNonEmpty_AreNotEqual(NonEmptyArray<int> elements)
    {
        var a = new EquatableArray<int>(ImmutableArray<int>.Empty);
        var b = new EquatableArray<int>(ImmutableArray.Create(elements.Get));

        return !a.Equals(b) && a != b;
    }

    // ─── GetHashCode Consistency with Equality ─────────────────────────────────

    [Property]
    public bool EqualArrays_HaveSameHashCode(int[] elements)
    {
        var array = ImmutableArray.Create(elements ?? Array.Empty<int>());
        var a = new EquatableArray<int>(array);
        var b = new EquatableArray<int>(array);

        return a.GetHashCode() == b.GetHashCode();
    }

    [Property]
    public bool EqualArrays_HaveSameHashCode_Strings(string[] elements)
    {
        var filtered = (elements ?? Array.Empty<string>()).Where(e => e != null).ToArray();
        var array = ImmutableArray.Create(filtered);
        var a = new EquatableArray<string>(array);
        var b = new EquatableArray<string>(array);

        return a.GetHashCode() == b.GetHashCode();
    }

    [Property]
    public bool EqualArrays_ConstructedSeparately_HaveSameHashCode(int[] elements)
    {
        var arr = elements ?? Array.Empty<int>();
        var a = new EquatableArray<int>(ImmutableArray.Create(arr));
        var b = new EquatableArray<int>(ImmutableArray.Create(arr));

        // If they are equal, hash codes must match
        if (a.Equals(b))
            return a.GetHashCode() == b.GetHashCode();

        return true; // not equal, no hash code constraint
    }

    // ─── IEnumerable<T> Implementation ─────────────────────────────────────────

    [Property]
    public bool Enumerable_YieldsAllElements_InOrder(int[] elements)
    {
        var arr = elements ?? Array.Empty<int>();
        var equatableArray = new EquatableArray<int>(ImmutableArray.Create(arr));

        var enumerated = equatableArray.ToArray();

        return enumerated.SequenceEqual(arr);
    }

    [Property]
    public bool Enumerable_YieldsAllElements_InOrder_Strings(string[] elements)
    {
        var filtered = (elements ?? Array.Empty<string>()).Where(e => e != null).ToArray();
        var equatableArray = new EquatableArray<string>(ImmutableArray.Create(filtered));

        var enumerated = equatableArray.ToArray();

        return enumerated.SequenceEqual(filtered);
    }

    [Property]
    public bool Enumerable_Count_MatchesArrayLength(int[] elements)
    {
        var arr = elements ?? Array.Empty<int>();
        var equatableArray = new EquatableArray<int>(ImmutableArray.Create(arr));

        return equatableArray.Count() == arr.Length;
    }

    // ─── Empty Arrays ──────────────────────────────────────────────────────────

    [Property]
    public bool EmptyArrays_AreEqual()
    {
        var a = new EquatableArray<int>(ImmutableArray<int>.Empty);
        var b = new EquatableArray<int>(ImmutableArray<int>.Empty);

        return a.Equals(b) && b.Equals(a) && a == b;
    }

    [Property]
    public bool EmptyArrays_HaveSameHashCode()
    {
        var a = new EquatableArray<int>(ImmutableArray<int>.Empty);
        var b = new EquatableArray<int>(ImmutableArray<int>.Empty);

        return a.GetHashCode() == b.GetHashCode();
    }

    [Property]
    public bool EmptyArrays_Enumerable_YieldsNoElements()
    {
        var equatableArray = new EquatableArray<int>(ImmutableArray<int>.Empty);

        return !equatableArray.Any();
    }

    // ─── Default Construction ──────────────────────────────────────────────────

    [Property]
    public bool DefaultInstances_AreEqual()
    {
        var a = default(EquatableArray<int>);
        var b = default(EquatableArray<int>);

        return a.Equals(b) && a == b;
    }

    [Property]
    public bool DefaultInstance_HashCode_DoesNotThrow()
    {
        var a = default(EquatableArray<int>);

        // Should not throw; just verify it returns a consistent value
        var hash1 = a.GetHashCode();
        var hash2 = a.GetHashCode();

        return hash1 == hash2;
    }

    [Property]
    public bool DefaultInstance_Enumerable_YieldsNoElements()
    {
        var equatableArray = default(EquatableArray<int>);

        return !equatableArray.Any();
    }
}
