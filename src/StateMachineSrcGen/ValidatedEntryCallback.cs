using System;

namespace StateMachineSrcGen;

/// <summary>
/// Represents a validated state-entry callback, enriched with analysis results.
/// </summary>
public readonly record struct ValidatedEntryCallback : IEquatable<ValidatedEntryCallback>
{
    /// <summary>Gets the name of the entry callback method.</summary>
    public required string MethodName { get; init; }

    /// <summary>Gets the target state name, or null for catch-all.</summary>
    public required string? TargetStateName { get; init; }

    /// <summary>Gets whether this is a catch-all entry callback (no specific target state).</summary>
    public required bool IsCatchAll { get; init; }

    /// <summary>Gets whether the method returns TState (true for targeted, false for catch-all).</summary>
    public required bool ReturnsTState { get; init; }
}
