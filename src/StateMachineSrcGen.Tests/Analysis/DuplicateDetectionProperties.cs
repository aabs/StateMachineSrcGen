// Feature: state-machine-source-generator, Property 2: Duplicate handler detection
// Feature: state-machine-source-generator, Property 6: Duplicate state name detection
// Feature: state-machine-source-generator, Property 7: Duplicate trigger name detection
// **Validates: Requirements 1.3, 2.4, 2.5**

using System.Collections.Immutable;
using System.Linq;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.CodeAnalysis;
using StateMachineSrcGen;
using StateMachineSrcGen.Analysis;

namespace StateMachineSrcGen.Tests.Analysis;

/// <summary>
/// Property 2: Duplicate handler detection — same (From, To, Trigger) triple emits SMSG001
/// Property 6: Duplicate state name detection — same state name emits SMSG007
/// Property 7: Duplicate trigger name detection — same trigger name emits SMSG008
/// </summary>
public class DuplicateDetectionProperties
{
    [Property]
    public bool DuplicateHandler_SameTriple_EmitsSMSG001(
        NonEmptyString fromRaw, NonEmptyString toRaw, NonEmptyString triggerRaw)
    {
        var fromState = ToIdentifier(fromRaw);
        var toState = ToIdentifier(toRaw);
        var trigger = ToIdentifier(triggerRaw);

        // Create a state machine with two handlers sharing the same (From, To, Trigger) triple
        var states = new[]
        {
            AnalysisTestHelper.CreateState(fromState, isInitial: true),
            AnalysisTestHelper.CreateState(toState)
        };
        var triggers = new[] { AnalysisTestHelper.CreateTrigger(trigger) };
        var handlers = new[]
        {
            AnalysisTestHelper.CreateTransitionHandler("Handler1", fromState, toState, trigger),
            AnalysisTestHelper.CreateTransitionHandler("Handler2", fromState, toState, trigger)
        };

        var input = AnalysisTestHelper.CreateStateMachine(states, triggers, handlers);
        var (_, diagnostics) = AnalysisPipeline.Analyze(input);

        return diagnostics.Any(d => d.Id == "SMSG001");
    }

    [Property]
    public bool DuplicateHandler_DifferentTriples_NoSMSG001(
        NonEmptyString fromRaw, NonEmptyString toRaw,
        NonEmptyString trigger1Raw, NonEmptyString trigger2Raw)
    {
        var fromState = ToIdentifier(fromRaw);
        var toState = ToIdentifier(toRaw);
        var trigger1 = ToIdentifier(trigger1Raw) + "A";
        var trigger2 = ToIdentifier(trigger2Raw) + "B";

        // Ensure triggers are different
        if (trigger1 == trigger2)
            trigger2 += "X";

        var states = new[]
        {
            AnalysisTestHelper.CreateState(fromState, isInitial: true),
            AnalysisTestHelper.CreateState(toState)
        };
        var triggers = new[]
        {
            AnalysisTestHelper.CreateTrigger(trigger1),
            AnalysisTestHelper.CreateTrigger(trigger2)
        };
        var handlers = new[]
        {
            AnalysisTestHelper.CreateTransitionHandler("Handler1", fromState, toState, trigger1),
            AnalysisTestHelper.CreateTransitionHandler("Handler2", fromState, toState, trigger2)
        };

        var input = AnalysisTestHelper.CreateStateMachine(states, triggers, handlers);
        var (_, diagnostics) = AnalysisPipeline.Analyze(input);

        return !diagnostics.Any(d => d.Id == "SMSG001");
    }

    [Property]
    public bool DuplicateStateName_EmitsSMSG007(NonEmptyString stateRaw)
    {
        var stateName = ToIdentifier(stateRaw);

        // Create a state machine with duplicate state names
        var states = new[]
        {
            AnalysisTestHelper.CreateState(stateName, isInitial: true),
            AnalysisTestHelper.CreateState(stateName, isInitial: false)
        };
        var triggers = new[] { AnalysisTestHelper.CreateTrigger("Go") };
        var handlers = new[]
        {
            AnalysisTestHelper.CreateTransitionHandler("Handle", stateName, stateName, "Go")
        };

        var input = AnalysisTestHelper.CreateStateMachine(states, triggers, handlers);
        var (_, diagnostics) = AnalysisPipeline.Analyze(input);

        return diagnostics.Any(d => d.Id == "SMSG007");
    }

    [Property]
    public bool UniqueStateNames_NoSMSG007(NonEmptyString state1Raw, NonEmptyString state2Raw)
    {
        var state1 = ToIdentifier(state1Raw) + "A";
        var state2 = ToIdentifier(state2Raw) + "B";

        // Ensure states are different
        if (state1 == state2)
            state2 += "X";

        var states = new[]
        {
            AnalysisTestHelper.CreateState(state1, isInitial: true),
            AnalysisTestHelper.CreateState(state2)
        };
        var triggers = new[] { AnalysisTestHelper.CreateTrigger("Go") };
        var handlers = new[]
        {
            AnalysisTestHelper.CreateTransitionHandler("Handle", state1, state2, "Go")
        };

        var input = AnalysisTestHelper.CreateStateMachine(states, triggers, handlers);
        var (_, diagnostics) = AnalysisPipeline.Analyze(input);

        return !diagnostics.Any(d => d.Id == "SMSG007");
    }

    [Property]
    public bool DuplicateTriggerName_EmitsSMSG008(NonEmptyString triggerRaw)
    {
        var triggerName = ToIdentifier(triggerRaw);

        // Create a state machine with duplicate trigger names
        var states = new[]
        {
            AnalysisTestHelper.CreateState("Idle", isInitial: true),
            AnalysisTestHelper.CreateState("Running")
        };
        var triggers = new[]
        {
            AnalysisTestHelper.CreateTrigger(triggerName),
            AnalysisTestHelper.CreateTrigger(triggerName)
        };
        var handlers = new[]
        {
            AnalysisTestHelper.CreateTransitionHandler("Handle", "Idle", "Running", triggerName)
        };

        var input = AnalysisTestHelper.CreateStateMachine(states, triggers, handlers);
        var (_, diagnostics) = AnalysisPipeline.Analyze(input);

        return diagnostics.Any(d => d.Id == "SMSG008");
    }

    [Property]
    public bool UniqueTriggerNames_NoSMSG008(NonEmptyString trigger1Raw, NonEmptyString trigger2Raw)
    {
        var trigger1 = ToIdentifier(trigger1Raw) + "A";
        var trigger2 = ToIdentifier(trigger2Raw) + "B";

        // Ensure triggers are different
        if (trigger1 == trigger2)
            trigger2 += "X";

        var states = new[]
        {
            AnalysisTestHelper.CreateState("Idle", isInitial: true),
            AnalysisTestHelper.CreateState("Running")
        };
        var triggers = new[]
        {
            AnalysisTestHelper.CreateTrigger(trigger1),
            AnalysisTestHelper.CreateTrigger(trigger2)
        };
        var handlers = new[]
        {
            AnalysisTestHelper.CreateTransitionHandler("Handle1", "Idle", "Running", trigger1),
            AnalysisTestHelper.CreateTransitionHandler("Handle2", "Idle", "Running", trigger2)
        };

        var input = AnalysisTestHelper.CreateStateMachine(states, triggers, handlers);
        var (_, diagnostics) = AnalysisPipeline.Analyze(input);

        return !diagnostics.Any(d => d.Id == "SMSG008");
    }

    // ─── Helpers ────────────────────────────────────────────────────────────────

    private static string ToIdentifier(NonEmptyString raw)
    {
        var filtered = new string(raw.Get.Where(char.IsLetterOrDigit).ToArray());
        if (string.IsNullOrEmpty(filtered) || !char.IsLetter(filtered[0]))
            return "X" + filtered;
        return filtered;
    }
}
