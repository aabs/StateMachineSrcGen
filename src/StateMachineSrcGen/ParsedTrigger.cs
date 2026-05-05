using System;
using Microsoft.CodeAnalysis;

namespace StateMachineSrcGen;

/// <summary>
/// Represents a parsed trigger declaration from a state machine definition.
/// </summary>
public readonly record struct ParsedTrigger : IEquatable<ParsedTrigger>
{
    /// <summary>Gets the name of the trigger.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the source location of the trigger declaration.</summary>
    public required Location Location { get; init; }
}
