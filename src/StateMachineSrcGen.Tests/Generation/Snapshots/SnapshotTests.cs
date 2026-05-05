// Snapshot/approval tests for generated output stability
// Verifies generated output matches expected snapshots for representative state machines
// **Validates: Requirements 7.4**

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
    /// contains all expected structural elements.
    /// </summary>
    [Fact]
    public void Snapshot_MinimalStateMachine_ContainsExpectedStructure()
    {
        var idle = new ValidatedState { Name = "Idle", IsInitial = true, IsTerminal = false };
        var running = new ValidatedState { Name = "Running", IsInitial = false, IsTerminal = true };

        var input = new ValidatedStateMachine
        {
            Namespace = "MyApp",
            ClassName = "SimpleMachine",
            StateTypeName = "MyState",
            EventTypeName = "MyEvent",
            EventIdTypeName = "string",
            States = new EquatableArray<ValidatedState>(ImmutableArray.Create(idle, running)),
            InitialState = idle,
            Transitions = new EquatableArray<ValidatedTransition>(ImmutableArray.Create(
                new ValidatedTransition
                {
                    FromState = "Idle",
                    ToState = "Running",
                    Trigger = "Start",
                    EventId = "start",
                    HandlerMethodName = "HandleStart",
                    GuardMethodName = null,
                    SideEffectMethodName = null,
                    DeclarationOrder = 0
                }))
        };

        var (source, diagnostics) = GenerationPipeline.Generate(input);

        Assert.NotNull(source);
        Assert.Empty(diagnostics);

        // Structural assertions - the generated code must contain these elements
        Assert.Contains("namespace MyApp", source);
        Assert.Contains("partial class SimpleMachine", source);
        Assert.Contains("HandleAsync", source);
        Assert.Contains("@event.GetEventId()", source);
        Assert.Contains("case \"start\"", source);
        Assert.Contains("HandleStart", source);
        Assert.Contains("TransitionResult.Success", source);
        Assert.Contains("TransitionResult.NotHandled", source);
        Assert.Contains("InMemoryPersistence", source);
        Assert.Contains("NoOpLock", source);
        Assert.Contains("LoadAsync", source);
        Assert.Contains("SaveAsync", source);
        Assert.Contains("AcquireAsync", source);
        Assert.Contains("ReleaseAsync", source);
    }

    /// <summary>
    /// Verifies the generated output for a state machine with guards and side effects.
    /// </summary>
    [Fact]
    public void Snapshot_MachineWithGuardsAndSideEffects_ContainsExpectedStructure()
    {
        var idle = new ValidatedState { Name = "Idle", IsInitial = true, IsTerminal = false };
        var active = new ValidatedState { Name = "Active", IsInitial = false, IsTerminal = false };
        var done = new ValidatedState { Name = "Done", IsInitial = false, IsTerminal = true };

        var input = new ValidatedStateMachine
        {
            Namespace = "WorkflowApp",
            ClassName = "WorkflowMachine",
            StateTypeName = "WfState",
            EventTypeName = "WfEvent",
            EventIdTypeName = "string",
            States = new EquatableArray<ValidatedState>(ImmutableArray.Create(idle, active, done)),
            InitialState = idle,
            Transitions = new EquatableArray<ValidatedTransition>(ImmutableArray.Create(
                new ValidatedTransition
                {
                    FromState = "Idle",
                    ToState = "Active",
                    Trigger = "Activate",
                    EventId = "activate",
                    HandlerMethodName = "HandleActivate",
                    GuardMethodName = "CanActivate",
                    SideEffectMethodName = "OnActivated",
                    DeclarationOrder = 0
                },
                new ValidatedTransition
                {
                    FromState = "Active",
                    ToState = "Done",
                    Trigger = "Complete",
                    EventId = "complete",
                    HandlerMethodName = "HandleComplete",
                    GuardMethodName = null,
                    SideEffectMethodName = "OnCompleted",
                    DeclarationOrder = 1
                }))
        };

        var (source, diagnostics) = GenerationPipeline.Generate(input);

        Assert.NotNull(source);
        Assert.Empty(diagnostics);

        // Verify guard invocation
        Assert.Contains("CanActivate", source);

        // Verify side effect invocations
        Assert.Contains("OnActivated", source);
        Assert.Contains("OnCompleted", source);

        // Verify both transitions
        Assert.Contains("case \"activate\"", source);
        Assert.Contains("case \"complete\"", source);
        Assert.Contains("HandleActivate", source);
        Assert.Contains("HandleComplete", source);

        // Verify state checks
        Assert.Contains("\"Idle\"", source);
        Assert.Contains("\"Active\"", source);
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
        var idle = new ValidatedState { Name = "Idle", IsInitial = true, IsTerminal = false };
        var stateA = new ValidatedState { Name = "StateA", IsInitial = false, IsTerminal = true };
        var stateB = new ValidatedState { Name = "StateB", IsInitial = false, IsTerminal = true };

        var input = new ValidatedStateMachine
        {
            Namespace = "MultiTrans",
            ClassName = "MultiMachine",
            StateTypeName = "string",
            EventTypeName = "MEvent",
            EventIdTypeName = "string",
            States = new EquatableArray<ValidatedState>(ImmutableArray.Create(idle, stateA, stateB)),
            InitialState = idle,
            Transitions = new EquatableArray<ValidatedTransition>(ImmutableArray.Create(
                new ValidatedTransition
                {
                    FromState = "Idle",
                    ToState = "StateA",
                    Trigger = "GoA",
                    EventId = "goA",
                    HandlerMethodName = "HandleGoA",
                    GuardMethodName = null,
                    SideEffectMethodName = null,
                    DeclarationOrder = 0
                },
                new ValidatedTransition
                {
                    FromState = "Idle",
                    ToState = "StateB",
                    Trigger = "GoB",
                    EventId = "goB",
                    HandlerMethodName = "HandleGoB",
                    GuardMethodName = null,
                    SideEffectMethodName = null,
                    DeclarationOrder = 1
                }))
        };

        var (source, diagnostics) = GenerationPipeline.Generate(input);

        Assert.NotNull(source);
        Assert.Empty(diagnostics);

        // Both event IDs should be in the dispatch
        Assert.Contains("case \"goA\"", source);
        Assert.Contains("case \"goB\"", source);
        Assert.Contains("HandleGoA", source);
        Assert.Contains("HandleGoB", source);
    }
}
