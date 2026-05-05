using System;

namespace StateMachineSrcGen;

/// <summary>
/// Validated and enriched state machine model ready for code generation.
/// This is the output of the analysis stage and input to the generation stage.
/// </summary>
public readonly record struct ValidatedStateMachine : IEquatable<ValidatedStateMachine>
{
    /// <summary>Gets the namespace containing the state machine class.</summary>
    public required string Namespace { get; init; }

    /// <summary>Gets the class name of the state machine.</summary>
    public required string ClassName { get; init; }

    /// <summary>Gets the state type name (TState generic parameter).</summary>
    public required string StateTypeName { get; init; }

    /// <summary>Gets the event type name (TEvent generic parameter).</summary>
    public required string EventTypeName { get; init; }

    /// <summary>Gets the TEventId type name from IDispatchableEvent.</summary>
    public required string EventIdTypeName { get; init; }

    /// <summary>Gets the validated states.</summary>
    public required EquatableArray<ValidatedState> States { get; init; }

    /// <summary>Gets the initial state.</summary>
    public required ValidatedState InitialState { get; init; }

    /// <summary>Gets the validated transitions.</summary>
    public required EquatableArray<ValidatedTransition> Transitions { get; init; }
}
