using System;
using Microsoft.CodeAnalysis;

namespace StateMachineSrcGen;

/// <summary>
/// Represents a parsed state declaration from a state machine definition.
/// </summary>
public readonly record struct ParsedState : IEquatable<ParsedState>
{
    /// <summary>Gets the name of the state.</summary>
    public required string Name { get; init; }

    /// <summary>Gets whether this state is the initial state.</summary>
    public required bool IsInitial { get; init; }

    /// <summary>Gets the source location of the state declaration.</summary>
    public required Location Location { get; init; }
}
