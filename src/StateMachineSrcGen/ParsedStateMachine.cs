using System;
using Microsoft.CodeAnalysis;

namespace StateMachineSrcGen;

/// <summary>
/// Complete parsed representation of a state machine definition.
/// This is the output of the parsing stage and input to the analysis stage.
/// </summary>
public readonly record struct ParsedStateMachine : IEquatable<ParsedStateMachine>
{
    /// <summary>Gets the namespace containing the state machine class.</summary>
    public required string Namespace { get; init; }

    /// <summary>Gets the class name of the state machine.</summary>
    public required string ClassName { get; init; }

    /// <summary>Gets the state type name (TState generic parameter).</summary>
    public required string StateTypeName { get; init; }

    /// <summary>Gets the event type name (TEvent generic parameter).</summary>
    public required string EventTypeName { get; init; }

    /// <summary>Gets the declared states.</summary>
    public required EquatableArray<ParsedState> States { get; init; }

    /// <summary>Gets the declared triggers.</summary>
    public required EquatableArray<ParsedTrigger> Triggers { get; init; }

    /// <summary>Gets the declared handlers.</summary>
    public required EquatableArray<ParsedHandler> Handlers { get; init; }

    /// <summary>Gets the class modifiers (public, partial, static).</summary>
    public required ClassModifiers Modifiers { get; init; }

    /// <summary>Gets whether the event type implements IDispatchableEvent.</summary>
    public required bool ImplementsIDispatchableEvent { get; init; }

    /// <summary>Gets the TEventId type name from IDispatchableEvent, or null if not implemented.</summary>
    public required string? EventIdTypeName { get; init; }

    /// <summary>Gets whether the state type implements IStateMachineState.</summary>
    public required bool ImplementsIStateMachineState { get; init; }

    /// <summary>Gets the TStateId type name from IStateMachineState, or null if not implemented.</summary>
    public required string? StateIdTypeName { get; init; }

    /// <summary>Gets the source location of the class declaration.</summary>
    public required Location Location { get; init; }
}
