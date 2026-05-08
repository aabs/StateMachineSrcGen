using System;
using Microsoft.CodeAnalysis;

namespace StateMachineSrcGen;

/// <summary>
/// Represents a parsed event/trigger derived from an enum member.
/// </summary>
public readonly record struct ParsedEvent : IEquatable<ParsedEvent>
{
    /// <summary>Gets the name of the event (enum member name).</summary>
    public required string Name { get; init; }

    /// <summary>Gets the integer value of the enum member.</summary>
    public required int IntValue { get; init; }

    /// <summary>Gets the source location of the enum member declaration.</summary>
    public required Location Location { get; init; }
}
