// Feature: state-machine-source-generator, Property 3: Undefined state or trigger reference detection
// Feature: state-machine-source-generator, Property 4: Empty state set rejection
// Feature: state-machine-source-generator, Property 5: Initial state cardinality enforcement
// **Validates: Requirements 1.4, 2.1, 2.2, 2.3**

using System.Collections.Immutable;
using System.Linq;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.CodeAnalysis;
using StateMachineSrcGen;
using StateMachineSrcGen.Analysis;

namespace StateMachineSrcGen.Tests.Analysis;

/// <summary>
/// Property 3: Undefined state or trigger reference detection — handler referencing undeclared state/trigger emits error
/// Property 4: Empty state set rejection — empty States collection emits SMSG004
/// Property 5: Initial state cardinality enforcement — zero initial states emits SMSG005, multiple emits SMSG006
/// </summary>
public class ReferenceValidationProperties
{
    [Property]
    public bool UndefinedStateReference_EmitsError(
        NonEmptyString declaredStateRaw, NonEmptyString undeclaredStateRaw, NonEmptyString triggerRaw)
    {
        var declaredState = ToIdentifier(declaredStateRaw) + "Declared";
        var undeclaredState = ToIdentifier(undeclaredStateRaw) + "Undeclared";
        var trigger = ToIdentifier(triggerRaw);

        // Ensure they are different
        if (declaredState == undeclaredState)
            undeclaredState += "X";

        // Handler references an undeclared state as ToState
        var states = new[] { AnalysisTestHelper.CreateState(declaredState, isInitial: true) };
        var triggers = new[] { AnalysisTestHelper.CreateTrigger(trigger) };
        var handlers = new[]
        {
            AnalysisTestHelper.CreateTransitionHandler("Handle", declaredState, undeclaredState, trigger)
        };

        var input = AnalysisTestHelper.CreateStateMachine(states, triggers, handlers);
        var (_, diagnostics) = AnalysisPipeline.Analyze(input);

        return diagnostics.Any(d => d.Id == "SMSG002");
    }

    [Property]
    public bool UndefinedTriggerReference_EmitsError(
        NonEmptyString stateRaw, NonEmptyString declaredTriggerRaw, NonEmptyString undeclaredTriggerRaw)
    {
        var state = ToIdentifier(stateRaw);
        var declaredTrigger = ToIdentifier(declaredTriggerRaw) + "Declared";
        var undeclaredTrigger = ToIdentifier(undeclaredTriggerRaw) + "Undeclared";

        // Ensure they are different
        if (declaredTrigger == undeclaredTrigger)
            undeclaredTrigger += "X";

        // Handler references an undeclared trigger
        var states = new[]
        {
            AnalysisTestHelper.CreateState(state, isInitial: true),
            AnalysisTestHelper.CreateState(state + "Target")
        };
        var triggers = new[] { AnalysisTestHelper.CreateTrigger(declaredTrigger) };
        var handlers = new[]
        {
            AnalysisTestHelper.CreateTransitionHandler("Handle", state, state + "Target", undeclaredTrigger)
        };

        var input = AnalysisTestHelper.CreateStateMachine(states, triggers, handlers);
        var (_, diagnostics) = AnalysisPipeline.Analyze(input);

        return diagnostics.Any(d => d.Id == "SMSG003");
    }

    [Property]
    public bool EmptyStates_EmitsSMSG004(NonEmptyString classRaw)
    {
        // Empty states collection
        var input = AnalysisTestHelper.CreateStateMachine(
            states: System.Array.Empty<ParsedState>(),
            triggers: new[] { AnalysisTestHelper.CreateTrigger("Go") },
            handlers: System.Array.Empty<ParsedHandler>());

        var (result, diagnostics) = AnalysisPipeline.Analyze(input);

        return diagnostics.Any(d => d.Id == "SMSG004") && result == null;
    }

    [Property]
    public bool ZeroInitialStates_EmitsSMSG005(NonEmptyString state1Raw, NonEmptyString state2Raw)
    {
        var state1 = ToIdentifier(state1Raw) + "A";
        var state2 = ToIdentifier(state2Raw) + "B";
        if (state1 == state2) state2 += "X";

        // States with no initial state
        var states = new[]
        {
            AnalysisTestHelper.CreateState(state1, isInitial: false),
            AnalysisTestHelper.CreateState(state2, isInitial: false)
        };
        var triggers = new[] { AnalysisTestHelper.CreateTrigger("Go") };
        var handlers = new[]
        {
            AnalysisTestHelper.CreateTransitionHandler("Handle", state1, state2, "Go")
        };

        var input = AnalysisTestHelper.CreateStateMachine(states, triggers, handlers);
        var (result, diagnostics) = AnalysisPipeline.Analyze(input);

        return diagnostics.Any(d => d.Id == "SMSG005") && result == null;
    }

    [Property]
    public bool MultipleInitialStates_EmitsSMSG006(NonEmptyString state1Raw, NonEmptyString state2Raw)
    {
        var state1 = ToIdentifier(state1Raw) + "A";
        var state2 = ToIdentifier(state2Raw) + "B";
        if (state1 == state2) state2 += "X";

        // Multiple initial states
        var states = new[]
        {
            AnalysisTestHelper.CreateState(state1, isInitial: true),
            AnalysisTestHelper.CreateState(state2, isInitial: true)
        };
        var triggers = new[] { AnalysisTestHelper.CreateTrigger("Go") };
        var handlers = new[]
        {
            AnalysisTestHelper.CreateTransitionHandler("Handle", state1, state2, "Go")
        };

        var input = AnalysisTestHelper.CreateStateMachine(states, triggers, handlers);
        var (result, diagnostics) = AnalysisPipeline.Analyze(input);

        return diagnostics.Any(d => d.Id == "SMSG006") && result == null;
    }

    [Property]
    public bool ExactlyOneInitialState_NoSMSG005OrSMSG006(NonEmptyString stateRaw)
    {
        var state = ToIdentifier(stateRaw);

        var states = new[]
        {
            AnalysisTestHelper.CreateState(state, isInitial: true),
            AnalysisTestHelper.CreateState(state + "Other")
        };
        var triggers = new[] { AnalysisTestHelper.CreateTrigger("Go") };
        var handlers = new[]
        {
            AnalysisTestHelper.CreateTransitionHandler("Handle", state, state + "Other", "Go")
        };

        var input = AnalysisTestHelper.CreateStateMachine(states, triggers, handlers);
        var (_, diagnostics) = AnalysisPipeline.Analyze(input);

        return !diagnostics.Any(d => d.Id == "SMSG005") && !diagnostics.Any(d => d.Id == "SMSG006");
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
