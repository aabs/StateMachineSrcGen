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

    /// <summary>Gets the TStateId concrete enum type name.</summary>
    public required string StateIdEnumTypeName { get; init; }

    /// <summary>Gets the TEventId concrete enum type name.</summary>
    public required string EventIdEnumTypeName { get; init; }

    /// <summary>Gets the TState concrete type name.</summary>
    public required string StateTypeName { get; init; }

    /// <summary>Gets the TEvent concrete type name.</summary>
    public required string EventTypeName { get; init; }

    /// <summary>Gets the validated states.</summary>
    public required EquatableArray<ValidatedState> States { get; init; }

    /// <summary>Gets the initial state.</summary>
    public required ValidatedState InitialState { get; init; }

    /// <summary>Gets the validated transitions.</summary>
    public required EquatableArray<ValidatedTransition> Transitions { get; init; }

    /// <summary>Gets the validated entry callbacks.</summary>
    public required EquatableArray<ValidatedEntryCallback> EntryCallbacks { get; init; }

    /// <summary>Gets the cleanup handler method name, or null if not declared.</summary>
    public required string? CleanupHandlerMethodName { get; init; }
}
