using System;

namespace StateMachineSrcGen;

/// <summary>
/// Represents a validated transition in the state machine, ready for code generation.
/// </summary>
public readonly record struct ValidatedTransition : IEquatable<ValidatedTransition>
{
    /// <summary>Gets the source state name.</summary>
    public required string FromState { get; init; }

    /// <summary>Gets the target state name.</summary>
    public required string ToState { get; init; }

    /// <summary>Gets the trigger name.</summary>
    public required string Trigger { get; init; }

    /// <summary>Gets the integer enum value of the source state.</summary>
    public required int FromStateEnumValue { get; init; }

    /// <summary>Gets the integer enum value of the target state.</summary>
    public required int ToStateEnumValue { get; init; }

    /// <summary>Gets the integer enum value of the trigger/event.</summary>
    public required int TriggerEnumValue { get; init; }

    /// <summary>Gets the event ID value this transition responds to.</summary>
    public required string EventId { get; init; }

    /// <summary>Gets the handler method name for this transition.</summary>
    public required string HandlerMethodName { get; init; }

    /// <summary>Gets the guard method name, or null if no guard.</summary>
    public required string? GuardMethodName { get; init; }

    /// <summary>Gets the side effect method name, or null if no side effect.</summary>
    public required string? SideEffectMethodName { get; init; }

    /// <summary>Gets whether this transition targets a terminal state.</summary>
    public required bool IsTerminal { get; init; }

    /// <summary>Gets the declaration order for guard evaluation priority.</summary>
    public required int DeclarationOrder { get; init; }
}
