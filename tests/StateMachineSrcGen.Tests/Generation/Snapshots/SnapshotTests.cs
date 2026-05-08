// Snapshot/approval tests for generated output stability
// Verifies generated output matches expected snapshots for representative state machines
// **Validates: Requirements 7.1, 7.2, 7.3, 7.4**

using System.Collections.Immutable;
using System.Linq;
using StateMachineSrcGen;
using StateMachineSrcGen.Generation;
using Xunit;

namespace StateMachineSrcGen.Tests.Generation.Snapshots;

/// <summary>
/// Snapshot tests that verify generated output stability across refactors.
/// Uses simple string comparison against expected output patterns.
/// </summary>
public class SnapshotTests
{
    /// <summary>
    /// Verifies the generated output for a minimal single-transition state machine
    /// contains all expected structural elements using enum-based dispatch.
    /// </summary>
    [Fact]
    public void Snapshot_MinimalStateMachine_ContainsExpectedStructure()
    {
        var idle = new ValidatedState { Name = "Idle", EnumValue = 0, IsInitial = true, IsTerminal = false };
        var running = new ValidatedState { Name = "Running", EnumValue = 1, IsInitial = false, IsTerminal = true };

        var input = new ValidatedStateMachine
        {
            Namespace = "MyApp",
            ClassName = "SimpleMachine",
            StateTypeName = "MyState",
            EventTypeName = "MyEvent",
            StateIdEnumTypeName = "MyStateId",
            EventIdEnumTypeName = "MyEventId",
            States = new EquatableArray<ValidatedState>(ImmutableArray.Create(idle, running)),
            InitialState = idle,
            Transitions = new EquatableArray<ValidatedTransition>(ImmutableArray.Create(
                new ValidatedTransition
                {
                    FromState = "Idle",
                    ToState = "Running",
                    Trigger = "Start",
                    EventId = "Start",
                    HandlerMethodName = "HandleStart",
                    GuardMethodName = null,
                    SideEffectMethodName = null,
                    FromStateEnumValue = 0,
                    ToStateEnumValue = 1,
                    TriggerEnumValue = 0,
                    IsTerminal = true,
                    DeclarationOrder = 0
                })),
            EntryCallbacks = new EquatableArray<ValidatedEntryCallback>(ImmutableArray<ValidatedEntryCallback>.Empty),
            CleanupHandlerMethodName = null
        };

        var (source, diagnostics) = GenerationPipeline.Generate(input);

        Assert.NotNull(source);
        Assert.Empty(diagnostics);

        // Structural assertions - the generated code must contain these elements
        Assert.Contains("namespace MyApp", source);
        Assert.Contains("partial class SimpleMachine", source);
        Assert.Contains("HandleAsync", source);
        Assert.Contains("@event.GetEventId()", source);
        Assert.Contains("case MyEventId.Start:", source);
        Assert.Contains("HandleStart", source);
        Assert.Contains("TransitionResult<MyState>.Succeeded(newState)", source);
        Assert.Contains("TransitionResult<MyState>.NoTransition", source);
        Assert.Contains("InMemoryPersistence", source);
        Assert.Contains("NoOpLock", source);
        Assert.Contains("LoadAsync", source);
        Assert.Contains("SaveAsync", source);
        Assert.Contains("AcquireAsync", source);
        Assert.Contains("ReleaseAsync", source);
        // Enum-based state comparison
        Assert.Contains("currentState.GetStateId() == MyStateId.Idle", source);
    }

    /// <summary>
    /// Verifies the generated output for a state machine with guards and side effects.
    /// </summary>
    [Fact]
    public void Snapshot_MachineWithGuardsAndSideEffects_ContainsExpectedStructure()
    {
        var idle = new ValidatedState { Name = "Idle", EnumValue = 0, IsInitial = true, IsTerminal = false };
        var active = new ValidatedState { Name = "Active", EnumValue = 1, IsInitial = false, IsTerminal = false };
        var done = new ValidatedState { Name = "Done", EnumValue = 2, IsInitial = false, IsTerminal = true };

        var input = new ValidatedStateMachine
        {
            Namespace = "WorkflowApp",
            ClassName = "WorkflowMachine",
            StateTypeName = "WfState",
            EventTypeName = "WfEvent",
            StateIdEnumTypeName = "WfStateId",
            EventIdEnumTypeName = "WfEventId",
            States = new EquatableArray<ValidatedState>(ImmutableArray.Create(idle, active, done)),
            InitialState = idle,
            Transitions = new EquatableArray<ValidatedTransition>(ImmutableArray.Create(
                new ValidatedTransition
                {
                    FromState = "Idle",
                    ToState = "Active",
                    Trigger = "Activate",
                    EventId = "Activate",
                    HandlerMethodName = "HandleActivate",
                    GuardMethodName = "CanActivate",
                    SideEffectMethodName = "OnActivated",
                    FromStateEnumValue = 0,
                    ToStateEnumValue = 1,
                    TriggerEnumValue = 0,
                    IsTerminal = false,
                    DeclarationOrder = 0
                },
                new ValidatedTransition
                {
                    FromState = "Active",
                    ToState = "Done",
                    Trigger = "Complete",
                    EventId = "Complete",
                    HandlerMethodName = "HandleComplete",
                    GuardMethodName = null,
                    SideEffectMethodName = "OnCompleted",
                    FromStateEnumValue = 1,
                    ToStateEnumValue = 2,
                    TriggerEnumValue = 1,
                    IsTerminal = false,
                    DeclarationOrder = 1
                })),
            EntryCallbacks = new EquatableArray<ValidatedEntryCallback>(ImmutableArray<ValidatedEntryCallback>.Empty),
            CleanupHandlerMethodName = null
        };

        var (source, diagnostics) = GenerationPipeline.Generate(input);

        Assert.NotNull(source);
        Assert.Empty(diagnostics);

        // Verify guard invocation
        Assert.Contains("CanActivate", source);

        // Verify side effect invocations
        Assert.Contains("OnActivated", source);
        Assert.Contains("OnCompleted", source);

        // Verify both transitions with enum case labels
        Assert.Contains("case WfEventId.Activate:", source);
        Assert.Contains("case WfEventId.Complete:", source);
        Assert.Contains("HandleActivate", source);
        Assert.Contains("HandleComplete", source);

        // Verify enum-based state checks
        Assert.Contains("currentState.GetStateId() == WfStateId.Idle", source);
        Assert.Contains("currentState.GetStateId() == WfStateId.Active", source);
    }

    /// <summary>
    /// Verifies that the generated output is deterministic - same input always produces same output.
    /// </summary>
    [Fact]
    public void Snapshot_SameInput_ProducesIdenticalOutput()
    {
        var input = GenerationTestHelper.CreateValidStateMachine(
            className: "DetMachine", ns: "DetNs");

        var (source1, _) = GenerationPipeline.Generate(input);
        var (source2, _) = GenerationPipeline.Generate(input);

        Assert.NotNull(source1);
        Assert.NotNull(source2);
        Assert.Equal(source1, source2);
    }

    /// <summary>
    /// Verifies the generated output includes XML documentation on public members.
    /// </summary>
    [Fact]
    public void Snapshot_GeneratedOutput_IncludesXmlDocumentation()
    {
        var input = GenerationTestHelper.CreateValidStateMachine();

        var (source, _) = GenerationPipeline.Generate(input);

        Assert.NotNull(source);

        // Should contain XML doc comments
        Assert.Contains("/// <summary>", source);
    }

    /// <summary>
    /// Verifies the generated output includes nullable enable directive.
    /// </summary>
    [Fact]
    public void Snapshot_GeneratedOutput_IncludesNullableEnable()
    {
        var input = GenerationTestHelper.CreateValidStateMachine();

        var (source, _) = GenerationPipeline.Generate(input);

        Assert.NotNull(source);
        Assert.Contains("#nullable enable", source);
    }

    /// <summary>
    /// Verifies the generated output for multiple transitions from the same state.
    /// </summary>
    [Fact]
    public void Snapshot_MultipleTransitionsFromSameState_GeneratesCorrectDispatch()
    {
        var idle = new ValidatedState { Name = "Idle", EnumValue = 0, IsInitial = true, IsTerminal = false };
        var stateA = new ValidatedState { Name = "StateA", EnumValue = 1, IsInitial = false, IsTerminal = true };
        var stateB = new ValidatedState { Name = "StateB", EnumValue = 2, IsInitial = false, IsTerminal = true };

        var input = new ValidatedStateMachine
        {
            Namespace = "MultiTrans",
            ClassName = "MultiMachine",
            StateTypeName = "MState",
            EventTypeName = "MEvent",
            StateIdEnumTypeName = "MStateId",
            EventIdEnumTypeName = "MEventId",
            States = new EquatableArray<ValidatedState>(ImmutableArray.Create(idle, stateA, stateB)),
            InitialState = idle,
            Transitions = new EquatableArray<ValidatedTransition>(ImmutableArray.Create(
                new ValidatedTransition
                {
                    FromState = "Idle",
                    ToState = "StateA",
                    Trigger = "GoA",
                    EventId = "GoA",
                    HandlerMethodName = "HandleGoA",
                    GuardMethodName = null,
                    SideEffectMethodName = null,
                    FromStateEnumValue = 0,
                    ToStateEnumValue = 1,
                    TriggerEnumValue = 0,
                    IsTerminal = false,
                    DeclarationOrder = 0
                },
                new ValidatedTransition
                {
                    FromState = "Idle",
                    ToState = "StateB",
                    Trigger = "GoB",
                    EventId = "GoB",
                    HandlerMethodName = "HandleGoB",
                    GuardMethodName = null,
                    SideEffectMethodName = null,
                    FromStateEnumValue = 0,
                    ToStateEnumValue = 2,
                    TriggerEnumValue = 1,
                    IsTerminal = false,
                    DeclarationOrder = 1
                })),
            EntryCallbacks = new EquatableArray<ValidatedEntryCallback>(ImmutableArray<ValidatedEntryCallback>.Empty),
            CleanupHandlerMethodName = null
        };

        var (source, diagnostics) = GenerationPipeline.Generate(input);

        Assert.NotNull(source);
        Assert.Empty(diagnostics);

        // Both event IDs should be in the dispatch with enum case labels
        Assert.Contains("case MEventId.GoA:", source);
        Assert.Contains("case MEventId.GoB:", source);
        Assert.Contains("HandleGoA", source);
        Assert.Contains("HandleGoB", source);
    }
}
