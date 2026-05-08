using System;
using Microsoft.CodeAnalysis;

namespace StateMachineSrcGen;

/// <summary>
/// Represents a parsed state-entry callback.
/// </summary>
public readonly record struct ParsedEntryCallback : IEquatable<ParsedEntryCallback>
{
    /// <summary>Gets the name of the entry callback method.</summary>
    public required string MethodName { get; init; }

    /// <summary>Gets the target state name, or null for catch-all.</summary>
    public required string? TargetStateName { get; init; }

    /// <summary>Gets whether this is a catch-all entry callback (no specific target state).</summary>
    public required bool IsCatchAll { get; init; }

    /// <summary>Gets the method signature of the entry callback.</summary>
    public required MethodSignature Signature { get; init; }

    /// <summary>Gets the source location of the entry callback declaration.</summary>
    public required Location Location { get; init; }
}
