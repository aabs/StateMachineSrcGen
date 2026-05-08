using Microsoft.CodeAnalysis;

namespace StateMachineSrcGen.Diagnostics;

/// <summary>
/// Contains all diagnostic descriptors emitted by the state machine source generator.
/// Each descriptor has a unique SMSG### ID, severity, message format, and category.
/// </summary>
internal static class DiagnosticDescriptors
{
    private const string Category = "StateMachineSrcGen";

    /// <summary>SMSG001: Duplicate transition handler for the same (From, To, Trigger) triple.</summary>
    public static readonly DiagnosticDescriptor DuplicateTransitionHandler = new(
        id: "SMSG001",
        title: "Duplicate transition handler",
        messageFormat: "A transition handler for ({0} → {1}, Trigger: {2}) is already defined. Remove the duplicate handler.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>SMSG002: A handler references a state that is not declared.</summary>
    public static readonly DiagnosticDescriptor UndefinedStateReferenced = new(
        id: "SMSG002",
        title: "Undefined state referenced",
        messageFormat: "State '{0}' is not declared. Add a [State(\"{0}\")] attribute to the class declaration.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>SMSG003: A handler references a trigger that is not declared.</summary>
    public static readonly DiagnosticDescriptor UndefinedTriggerReferenced = new(
        id: "SMSG003",
        title: "Undefined trigger referenced",
        messageFormat: "Trigger '{0}' is not declared. Add a [Trigger(\"{0}\")] attribute to the class declaration.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>SMSG004: The state machine has no states declared.</summary>
    public static readonly DiagnosticDescriptor NoStatesDeclared = new(
        id: "SMSG004",
        title: "No states declared",
        messageFormat: "No states are declared. Add at least one [State] attribute to the class declaration.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>SMSG005: No state is marked as the initial state.</summary>
    public static readonly DiagnosticDescriptor NoInitialState = new(
        id: "SMSG005",
        title: "No initial state designated",
        messageFormat: "No initial state is designated. Set IsInitial = true on exactly one [State] attribute.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>SMSG006: Multiple states are marked as initial.</summary>
    public static readonly DiagnosticDescriptor MultipleInitialStates = new(
        id: "SMSG006",
        title: "Multiple initial states",
        messageFormat: "Multiple states are marked as initial ({0}). Only one state may have IsInitial = true.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>SMSG007: Two or more states share the same name.</summary>
    public static readonly DiagnosticDescriptor DuplicateStateNames = new(
        id: "SMSG007",
        title: "Duplicate state names",
        messageFormat: "State name '{0}' is declared more than once. Each state must have a unique name.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>SMSG008: Two or more triggers share the same name.</summary>
    public static readonly DiagnosticDescriptor DuplicateTriggerNames = new(
        id: "SMSG008",
        title: "Duplicate trigger names",
        messageFormat: "Trigger name '{0}' is declared more than once. Each trigger must have a unique name.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>SMSG009: A non-initial state has no inbound transitions and is unreachable.</summary>
    public static readonly DiagnosticDescriptor UnreachableState = new(
        id: "SMSG009",
        title: "Unreachable state detected",
        messageFormat: "State '{0}' is unreachable because it is not the initial state and no transition targets it",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>SMSG010: The class declaration is missing required modifiers or has incorrect generic parameters.</summary>
    public static readonly DiagnosticDescriptor InvalidClassDeclaration = new(
        id: "SMSG010",
        title: "Invalid class declaration",
        messageFormat: "The state machine class must be declared as 'public partial static' with exactly two generic type parameters. Missing: {0}.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>SMSG011: The class does not implement IStateMachine&lt;TState, TEvent&gt;.</summary>
    public static readonly DiagnosticDescriptor MissingIStateMachineImplementation = new(
        id: "SMSG011",
        title: "Missing IStateMachine implementation",
        messageFormat: "The class must implement IStateMachine<TState, TEvent>. Add the interface to the class declaration.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>SMSG012: A handler method has an invalid signature.</summary>
    public static readonly DiagnosticDescriptor InvalidHandlerSignature = new(
        id: "SMSG012",
        title: "Invalid handler signature",
        messageFormat: "Handler method '{0}' has an invalid signature; expected 'public static {1}' but found {2}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>SMSG013: The class does not provide an IStatePersistence&lt;TState&gt; implementation.</summary>
    public static readonly DiagnosticDescriptor MissingIStatePersistenceImplementation = new(
        id: "SMSG013",
        title: "Missing IStatePersistence implementation",
        messageFormat: "No IStatePersistence<TState> implementation is provided. Implement the interface or use the default InMemoryPersistence.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>SMSG014: A transition does not specify a target state.</summary>
    public static readonly DiagnosticDescriptor MissingTargetState = new(
        id: "SMSG014",
        title: "Missing target state in transition",
        messageFormat: "Transition from '{0}' with trigger '{1}' does not specify a target state. Set the 'To' parameter in the [Transition] attribute.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>SMSG015: An unexpected internal error occurred in the generator.</summary>
    public static readonly DiagnosticDescriptor InternalGeneratorError = new(
        id: "SMSG015",
        title: "Internal generator error",
        messageFormat: "An unexpected error occurred in the state machine generator: {0}. Please report this issue.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>SMSG016: The event type does not implement IDispatchableEvent&lt;TEventId&gt;.</summary>
    public static readonly DiagnosticDescriptor MissingIDispatchableEventImplementation = new(
        id: "SMSG016",
        title: "Event type does not implement IDispatchableEvent",
        messageFormat: "Event type '{0}' does not implement IDispatchableEvent<TEventId>. The event type must implement this interface for dispatch routing.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>SMSG018: An integer attribute argument does not correspond to a defined enum member.</summary>
    public static readonly DiagnosticDescriptor InvalidEnumValue = new(
        id: "SMSG018",
        title: "Invalid enum value in attribute",
        messageFormat: "Value {0} is not a member of enum {1}. Valid members: {2}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: "https://github.com/StateMachineSrcGen/docs/diagnostics/SMSG018.md");

    /// <summary>SMSG019: The state type does not implement IStateMachineState&lt;TStateId&gt;.</summary>
    public static readonly DiagnosticDescriptor MissingStateTypeConstraint = new(
        id: "SMSG019",
        title: "Missing type constraint",
        messageFormat: "State type \"{0}\" does not implement IStateMachineState<{1}>",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: "https://github.com/StateMachineSrcGen/docs/diagnostics/SMSG019.md");

    /// <summary>SMSG020: A state is designated as both initial and terminal.</summary>
    public static readonly DiagnosticDescriptor InitialAndTerminalState = new(
        id: "SMSG020",
        title: "State is both initial and terminal",
        messageFormat: "State \"{0}\" is designated as both initial and terminal",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: "https://github.com/StateMachineSrcGen/docs/diagnostics/SMSG020.md");

    /// <summary>SMSG021: Multiple methods are decorated with [OnTerminal].</summary>
    public static readonly DiagnosticDescriptor MultipleCleanupHandlers = new(
        id: "SMSG021",
        title: "Multiple cleanup handlers",
        messageFormat: "Only one [OnTerminal] method is permitted per state machine class",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: "https://github.com/StateMachineSrcGen/docs/diagnostics/SMSG021.md");

    /// <summary>SMSG022: Multiple parameterless [OnEnter] methods are defined.</summary>
    public static readonly DiagnosticDescriptor MultipleCatchAllOnEnter = new(
        id: "SMSG022",
        title: "Multiple catch-all entry callbacks",
        messageFormat: "Only one parameterless [OnEnter] method is permitted",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: "https://github.com/StateMachineSrcGen/docs/diagnostics/SMSG022.md");

    /// <summary>SMSG023: Multiple targeted [OnEnter] methods reference the same state.</summary>
    public static readonly DiagnosticDescriptor DuplicateTargetedOnEnter = new(
        id: "SMSG023",
        title: "Duplicate targeted entry callback",
        messageFormat: "Multiple [OnEnter] methods target state \"{0}\"",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: "https://github.com/StateMachineSrcGen/docs/diagnostics/SMSG023.md");

    /// <summary>SMSG024: An [OnEnter] method has an invalid signature.</summary>
    public static readonly DiagnosticDescriptor InvalidEntryCallbackSignature = new(
        id: "SMSG024",
        title: "Invalid entry callback signature",
        messageFormat: "[OnEnter] method \"{0}\" must have signature: {1}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: "https://github.com/StateMachineSrcGen/docs/diagnostics/SMSG024.md");

    /// <summary>SMSG025: An [OnTerminal] method has an invalid signature.</summary>
    public static readonly DiagnosticDescriptor InvalidCleanupHandlerSignature = new(
        id: "SMSG025",
        title: "Invalid cleanup handler signature",
        messageFormat: "[OnTerminal] method \"{0}\" must accept (TState) parameter and return Task",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: "https://github.com/StateMachineSrcGen/docs/diagnostics/SMSG025.md");

    /// <summary>SMSG026: A [Flags] enum is used as a state or event ID type.</summary>
    public static readonly DiagnosticDescriptor FlagsEnumNotSupported = new(
        id: "SMSG026",
        title: "Flags enum not supported",
        messageFormat: "Enum \"{0}\" has [Flags] attribute and cannot be used as a state/event ID type",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: "https://github.com/StateMachineSrcGen/docs/diagnostics/SMSG026.md");

    /// <summary>SMSG027: The state machine class does not declare exactly 4 type parameters.</summary>
    public static readonly DiagnosticDescriptor InvalidTypeParameterCount = new(
        id: "SMSG027",
        title: "Invalid class type parameter count",
        messageFormat: "State machine class must declare exactly 4 type parameters: TStateId, TEventId, TState, TEvent",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: "https://github.com/StateMachineSrcGen/docs/diagnostics/SMSG027.md");
}
