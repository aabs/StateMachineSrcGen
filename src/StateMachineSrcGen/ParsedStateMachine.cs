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

    /// <summary>Gets the class modifiers (public, partial, static).</summary>
    public required ClassModifiers Modifiers { get; init; }

    /// <summary>Gets the source location of the class declaration.</summary>
    public required Location Location { get; init; }

    /// <summary>Gets the TStateId concrete enum type name.</summary>
    public required string StateIdEnumTypeName { get; init; }

    /// <summary>Gets the TEventId concrete enum type name.</summary>
    public required string EventIdEnumTypeName { get; init; }

    /// <summary>Gets the TState concrete type name.</summary>
    public required string StateTypeName { get; init; }

    /// <summary>Gets the TEvent concrete type name.</summary>
    public required string EventTypeName { get; init; }

    /// <summary>Gets the states derived from TStateId enum members.</summary>
    public required EquatableArray<ParsedState> States { get; init; }

    /// <summary>Gets the events derived from TEventId enum members.</summary>
    public required EquatableArray<ParsedEvent> Events { get; init; }

    /// <summary>Gets the declared handlers.</summary>
    public required EquatableArray<ParsedHandler> Handlers { get; init; }

    /// <summary>Gets the initial state name, or null if not declared.</summary>
    public required string? InitialStateName { get; init; }

    /// <summary>Gets the terminal state names.</summary>
    public required EquatableArray<string> TerminalStateNames { get; init; }

    /// <summary>Gets the parsed entry callbacks.</summary>
    public required EquatableArray<ParsedEntryCallback> EntryCallbacks { get; init; }

    /// <summary>Gets the parsed cleanup handler, or null if not declared.</summary>
    public required ParsedCleanupHandler? CleanupHandler { get; init; }
}
