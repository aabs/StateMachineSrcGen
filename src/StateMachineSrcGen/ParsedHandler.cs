using System;
using Microsoft.CodeAnalysis;

namespace StateMachineSrcGen;

/// <summary>
/// Represents a parsed handler method from a state machine definition.
/// </summary>
public readonly record struct ParsedHandler : IEquatable<ParsedHandler>
{
    /// <summary>Gets the name of the handler method.</summary>
    public required string MethodName { get; init; }

    /// <summary>Gets the source state name for this handler.</summary>
    public required string FromState { get; init; }

    /// <summary>Gets the target state name for this handler.</summary>
    public required string ToState { get; init; }

    /// <summary>Gets the trigger name for this handler.</summary>
    public required string Trigger { get; init; }

    /// <summary>Gets the event ID value this handler responds to, or null if not specified.</summary>
    public required string? EventId { get; init; }

    /// <summary>Gets the kind of handler (Transition, Guard, or SideEffect).</summary>
    public required HandlerKind Kind { get; init; }

    /// <summary>Gets the method signature of the handler.</summary>
    public required MethodSignature Signature { get; init; }

    /// <summary>Gets the source location of the handler declaration.</summary>
    public required Location Location { get; init; }
}
