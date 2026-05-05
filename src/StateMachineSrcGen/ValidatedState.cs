using System;

namespace StateMachineSrcGen;

/// <summary>
/// Represents a validated state in the state machine, enriched with analysis results.
/// </summary>
public readonly record struct ValidatedState : IEquatable<ValidatedState>
{
    /// <summary>Gets the name of the state.</summary>
    public required string Name { get; init; }

    /// <summary>Gets whether this state is the initial state.</summary>
    public required bool IsInitial { get; init; }

    /// <summary>Gets whether this state is terminal (no outbound transitions).</summary>
    public required bool IsTerminal { get; init; }
}
