using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using StateMachineSrcGen;

namespace StateMachineSrcGen.Tests.Analysis;

/// <summary>
/// Helper class for analysis property tests. Creates ParsedStateMachine instances
/// directly without needing Roslyn compilation.
/// </summary>
internal static class AnalysisTestHelper
{
    /// <summary>
    /// Creates a minimal valid ParsedStateMachine with one initial state, one trigger,
    /// and one transition handler.
    /// </summary>
    public static ParsedStateMachine CreateValidStateMachine(
        string className = "TestMachine",
        string ns = "TestNamespace",
        string stateType = "MyState",
        string eventType = "MyEvent")
    {
        return new ParsedStateMachine
        {
            Namespace = ns,
            ClassName = className,
            StateTypeName = stateType,
            EventTypeName = eventType,
            StateIdEnumTypeName = "string",
            EventIdEnumTypeName = "string",
            States = new EquatableArray<ParsedState>(ImmutableArray.Create(
                new ParsedState { Name = "Idle", IsInitial = true, Location = Location.None },
                new ParsedState { Name = "Running", IsInitial = false, Location = Location.None })),
            Events = new EquatableArray<ParsedEvent>(ImmutableArray.Create(
                new ParsedEvent { Name = "Start", IntValue = 0, Location = Location.None })),
            Handlers = new EquatableArray<ParsedHandler>(ImmutableArray.Create(
                CreateTransitionHandler("HandleStart", "Idle", "Running", "Start", stateType, eventType))),
            Modifiers = ClassModifiers.Public | ClassModifiers.Partial | ClassModifiers.Static,
            InitialStateName = "Idle",
            TerminalStateNames = new EquatableArray<string>(ImmutableArray<string>.Empty),
            EntryCallbacks = new EquatableArray<ParsedEntryCallback>(ImmutableArray<ParsedEntryCallback>.Empty),
            CleanupHandler = null,
            Location = Location.None
        };
    }

    /// <summary>
    /// Creates a ParsedStateMachine with the specified states, triggers, and handlers.
    /// </summary>
    public static ParsedStateMachine CreateStateMachine(
        ParsedState[] states,
        ParsedTrigger[] triggers,
        ParsedHandler[] handlers,
        ClassModifiers modifiers = ClassModifiers.Public | ClassModifiers.Partial | ClassModifiers.Static,
        bool implementsIDispatchableEvent = true,
        string? eventIdTypeName = "string",
        string stateType = "MyState",
        string eventType = "MyEvent")
    {
        return new ParsedStateMachine
        {
            Namespace = "TestNamespace",
            ClassName = "TestMachine",
            StateTypeName = stateType,
            EventTypeName = eventType,
            StateIdEnumTypeName = "string",
            EventIdEnumTypeName = eventIdTypeName ?? "string",
            States = new EquatableArray<ParsedState>(states.ToImmutableArray()),
            Events = new EquatableArray<ParsedEvent>(triggers.Select((t, i) => new ParsedEvent { Name = t.Name, IntValue = i, Location = Location.None }).ToImmutableArray()),
            Handlers = new EquatableArray<ParsedHandler>(handlers.ToImmutableArray()),
            Modifiers = modifiers,
            InitialStateName = states.FirstOrDefault(s => s.IsInitial).Name,
            TerminalStateNames = new EquatableArray<string>(ImmutableArray<string>.Empty),
            EntryCallbacks = new EquatableArray<ParsedEntryCallback>(ImmutableArray<ParsedEntryCallback>.Empty),
            CleanupHandler = null,
            Location = Location.None
        };
    }

    /// <summary>
    /// Creates a transition handler with a valid signature.
    /// </summary>
    public static ParsedHandler CreateTransitionHandler(
        string methodName,
        string fromState,
        string toState,
        string trigger,
        string stateType = "MyState",
        string eventType = "MyEvent",
        string? eventId = null)
    {
        return new ParsedHandler
        {
            MethodName = methodName,
            FromState = fromState,
            ToState = toState,
            Trigger = trigger,
            EventId = eventId,
            Kind = HandlerKind.Transition,
            Signature = CreateValidSignature(stateType, stateType, eventType),
            Location = Location.None
        };
    }

    /// <summary>
    /// Creates a guard handler with a valid signature.
    /// </summary>
    public static ParsedHandler CreateGuardHandler(
        string methodName,
        string fromState,
        string toState,
        string trigger,
        string stateType = "MyState",
        string eventType = "MyEvent")
    {
        return new ParsedHandler
        {
            MethodName = methodName,
            FromState = fromState,
            ToState = toState,
            Trigger = trigger,
            EventId = null,
            Kind = HandlerKind.Guard,
            Signature = CreateValidSignature("bool", stateType, eventType),
            Location = Location.None
        };
    }

    /// <summary>
    /// Creates a side effect handler with a valid signature.
    /// </summary>
    public static ParsedHandler CreateSideEffectHandler(
        string methodName,
        string fromState,
        string toState,
        string trigger,
        string stateType = "MyState",
        string eventType = "MyEvent")
    {
        return new ParsedHandler
        {
            MethodName = methodName,
            FromState = fromState,
            ToState = toState,
            Trigger = trigger,
            EventId = null,
            Kind = HandlerKind.SideEffect,
            Signature = CreateValidSignature("void", stateType, eventType),
            Location = Location.None
        };
    }

    /// <summary>
    /// Creates a handler with a custom (potentially invalid) signature.
    /// </summary>
    public static ParsedHandler CreateHandlerWithSignature(
        string methodName,
        string fromState,
        string toState,
        string trigger,
        HandlerKind kind,
        bool isPublic,
        bool isStatic,
        string returnType,
        ParameterInfo[] parameters)
    {
        return new ParsedHandler
        {
            MethodName = methodName,
            FromState = fromState,
            ToState = toState,
            Trigger = trigger,
            EventId = null,
            Kind = kind,
            Signature = new MethodSignature
            {
                IsPublic = isPublic,
                IsStatic = isStatic,
                ReturnType = returnType,
                Parameters = new EquatableArray<ParameterInfo>(parameters.ToImmutableArray())
            },
            Location = Location.None
        };
    }

    /// <summary>
    /// Creates a valid method signature for a handler.
    /// </summary>
    public static MethodSignature CreateValidSignature(
        string returnType,
        string stateType = "MyState",
        string eventType = "MyEvent")
    {
        return new MethodSignature
        {
            IsPublic = true,
            IsStatic = true,
            ReturnType = returnType,
            Parameters = new EquatableArray<ParameterInfo>(ImmutableArray.Create(
                new ParameterInfo { Name = "state", TypeName = stateType },
                new ParameterInfo { Name = "event", TypeName = eventType }))
        };
    }

    /// <summary>
    /// Creates a ParsedState.
    /// </summary>
    public static ParsedState CreateState(string name, bool isInitial = false)
    {
        return new ParsedState { Name = name, IsInitial = isInitial, Location = Location.None };
    }

    /// <summary>
    /// Creates a ParsedTrigger.
    /// </summary>
    public static ParsedTrigger CreateTrigger(string name)
    {
        return new ParsedTrigger { Name = name, Location = Location.None };
    }
}
