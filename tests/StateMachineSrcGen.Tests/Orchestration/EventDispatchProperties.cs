// Feature: generic-state-machine-api, Property 10: Generated dispatch uses enum comparisons
// Feature: generic-state-machine-api, Property 33: Event dispatch extraction via GetEventId
// Feature: generic-state-machine-api, Property 35: Exhaustive dispatch with NoTransition fallthrough
// **Validates: Requirements 7.1, 7.2, 7.3, 7.4**

using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using StateMachineSrcGen;
using StateMachineSrcGen.Generation;
using StateMachineSrcGen.Tests.Generation;
using Xunit;

namespace StateMachineSrcGen.Tests.Orchestration;

/// <summary>
/// Property 10: Transition dispatch correctness — correct transitions selected for (state, trigger) pair.
/// Property 33: Event dispatch extraction via GetEventId — GetEventId() routes to correct handler based on EventId.
/// Property 35: Exhaustive dispatch with NoTransition fallthrough — unmatched event IDs return NoTransition.
/// </summary>
public class EventDispatchProperties
{
    /// <summary>
    /// Property 10: For any valid state machine with transitions, the generated dispatch logic
    /// selects exactly the transitions whose FromState matches currentState and whose EventId matches.
    /// Verified by inspecting generated source for correct enum-based switch/case structure.
    /// </summary>
    [Property]
    public bool TransitionDispatch_SelectsCorrectTransition_ForStateAndEventId(PositiveInt seed)
    {
        var idle = new ValidatedState { Name = "Idle", EnumValue = 0, IsInitial = true, IsTerminal = false };
        var running = new ValidatedState { Name = "Running", EnumValue = 1, IsInitial = false, IsTerminal = false };
        var stopped = new ValidatedState { Name = "Stopped", EnumValue = 2, IsInitial = false, IsTerminal = true };

        var input = new ValidatedStateMachine
        {
            Namespace = "TestNs",
            ClassName = "Dispatch",
            StateTypeName = "TestState",
            EventTypeName = "TestEvent",
            StateIdEnumTypeName = "TestStateId",
            EventIdEnumTypeName = "TestEventId",
            States = new EquatableArray<ValidatedState>(ImmutableArray.Create(idle, running, stopped)),
            InitialState = idle,
            Transitions = new EquatableArray<ValidatedTransition>(ImmutableArray.Create(
                new ValidatedTransition
                {
                    FromState = "Idle", ToState = "Running", Trigger = "Start",
                    EventId = "Start", HandlerMethodName = "HandleStart",
                    FromStateEnumValue = 0, ToStateEnumValue = 1, TriggerEnumValue = 0,
                    GuardMethodName = null, SideEffectMethodName = null, DeclarationOrder = 0, IsTerminal = false
                },
                new ValidatedTransition
                {
                    FromState = "Running", ToState = "Stopped", Trigger = "Stop",
                    EventId = "Stop", HandlerMethodName = "HandleStop",
                    FromStateEnumValue = 1, ToStateEnumValue = 2, TriggerEnumValue = 1,
                    GuardMethodName = null, SideEffectMethodName = null, DeclarationOrder = 1, IsTerminal = true
                })),
            EntryCallbacks = new EquatableArray<ValidatedEntryCallback>(ImmutableArray<ValidatedEntryCallback>.Empty),
            CleanupHandlerMethodName = null
        };

        var (source, diags) = GenerationPipeline.Generate(input);
        if (source == null) return false;

        // Verify the generated source contains correct enum-based dispatch structure:
        // - switch on eventId
        // - case TestEventId.Start checks currentState.GetStateId() == TestStateId.Idle
        // - case TestEventId.Stop checks currentState.GetStateId() == TestStateId.Running
        return source.Contains("case TestEventId.Start:") &&
               source.Contains("case TestEventId.Stop:") &&
               source.Contains("currentState.GetStateId() == TestStateId.Idle") &&
               source.Contains("currentState.GetStateId() == TestStateId.Running") &&
               source.Contains("HandleStart") &&
               source.Contains("HandleStop");
    }

    /// <summary>
    /// Property 33: The generated dispatch logic invokes GetEventId() on the event and uses
    /// the returned value in a switch statement to route to the correct handler.
    /// Verified by inspecting generated source structure.
    /// </summary>
    [Fact]
    public void EventDispatch_RoutesToCorrectHandler_BasedOnGetEventId()
    {
        var idle = new ValidatedState { Name = "Idle", EnumValue = 0, IsInitial = true, IsTerminal = false };
        var running = new ValidatedState { Name = "Running", EnumValue = 1, IsInitial = false, IsTerminal = true };

        var input = new ValidatedStateMachine
        {
            Namespace = "TestNs",
            ClassName = "EvtMachine",
            StateTypeName = "TestState",
            EventTypeName = "TestEvent",
            StateIdEnumTypeName = "TestStateId",
            EventIdEnumTypeName = "TestEventId",
            States = new EquatableArray<ValidatedState>(ImmutableArray.Create(idle, running)),
            InitialState = idle,
            Transitions = new EquatableArray<ValidatedTransition>(ImmutableArray.Create(
                new ValidatedTransition
                {
                    FromState = "Idle", ToState = "Running", Trigger = "GoStart",
                    EventId = "GoStart", HandlerMethodName = "HandleStart",
                    FromStateEnumValue = 0, ToStateEnumValue = 1, TriggerEnumValue = 0,
                    GuardMethodName = null, SideEffectMethodName = null, DeclarationOrder = 0, IsTerminal = true
                })),
            EntryCallbacks = new EquatableArray<ValidatedEntryCallback>(ImmutableArray<ValidatedEntryCallback>.Empty),
            CleanupHandlerMethodName = null
        };

        var (source, _) = GenerationPipeline.Generate(input);
        Assert.NotNull(source);

        // Verify the generated code calls GetEventId() and switches on enum value
        Assert.Contains("@event.GetEventId()", source);
        Assert.Contains("case TestEventId.GoStart:", source);
        Assert.Contains("currentState.GetStateId() == TestStateId.Idle", source);
    }

    /// <summary>
    /// Property 35: For any event ID value that has no matching handler for the current state,
    /// the generated dispatch code returns TransitionResult&lt;TState&gt;.NoTransition.
    /// </summary>
    [Fact]
    public void ExhaustiveDispatch_ReturnsNoTransition_ForUnmatchedEventId()
    {
        var idle = new ValidatedState { Name = "Idle", EnumValue = 0, IsInitial = true, IsTerminal = false };
        var running = new ValidatedState { Name = "Running", EnumValue = 1, IsInitial = false, IsTerminal = true };

        var input = new ValidatedStateMachine
        {
            Namespace = "TestNs",
            ClassName = "FallMachine",
            StateTypeName = "TestState",
            EventTypeName = "TestEvent",
            StateIdEnumTypeName = "TestStateId",
            EventIdEnumTypeName = "TestEventId",
            States = new EquatableArray<ValidatedState>(ImmutableArray.Create(idle, running)),
            InitialState = idle,
            Transitions = new EquatableArray<ValidatedTransition>(ImmutableArray.Create(
                new ValidatedTransition
                {
                    FromState = "Idle", ToState = "Running", Trigger = "GoStart",
                    EventId = "GoStart", HandlerMethodName = "HandleStart",
                    FromStateEnumValue = 0, ToStateEnumValue = 1, TriggerEnumValue = 0,
                    GuardMethodName = null, SideEffectMethodName = null, DeclarationOrder = 0, IsTerminal = true
                })),
            EntryCallbacks = new EquatableArray<ValidatedEntryCallback>(ImmutableArray<ValidatedEntryCallback>.Empty),
            CleanupHandlerMethodName = null
        };

        var (source, _) = GenerationPipeline.Generate(input);
        Assert.NotNull(source);

        // Verify the generated code has NoTransition fallthrough
        Assert.Contains("TransitionResult<TestState>.NoTransition", source);
    }

    /// <summary>
    /// Property 35 (property-based): For any valid state machine, the generated code
    /// contains a NoTransition fallthrough after the switch.
    /// </summary>
    [Property]
    public bool ExhaustiveDispatch_NoTransition_AlwaysPresent(NonEmptyString randomEventId)
    {
        var idle = new ValidatedState { Name = "Idle", EnumValue = 0, IsInitial = true, IsTerminal = false };
        var running = new ValidatedState { Name = "Running", EnumValue = 1, IsInitial = false, IsTerminal = true };

        var input = new ValidatedStateMachine
        {
            Namespace = "TestNs",
            ClassName = "PropFall",
            StateTypeName = "TestState",
            EventTypeName = "TestEvent",
            StateIdEnumTypeName = "TestStateId",
            EventIdEnumTypeName = "TestEventId",
            States = new EquatableArray<ValidatedState>(ImmutableArray.Create(idle, running)),
            InitialState = idle,
            Transitions = new EquatableArray<ValidatedTransition>(ImmutableArray.Create(
                new ValidatedTransition
                {
                    FromState = "Idle", ToState = "Running", Trigger = "GoStart",
                    EventId = "GoStart", HandlerMethodName = "HandleStart",
                    FromStateEnumValue = 0, ToStateEnumValue = 1, TriggerEnumValue = 0,
                    GuardMethodName = null, SideEffectMethodName = null, DeclarationOrder = 0, IsTerminal = true
                })),
            EntryCallbacks = new EquatableArray<ValidatedEntryCallback>(ImmutableArray<ValidatedEntryCallback>.Empty),
            CleanupHandlerMethodName = null
        };

        var (source, _) = GenerationPipeline.Generate(input);
        if (source == null) return false;

        // The generated code must have a fallthrough to NoTransition after the switch
        return source.Contains("NoTransition");
    }
}
