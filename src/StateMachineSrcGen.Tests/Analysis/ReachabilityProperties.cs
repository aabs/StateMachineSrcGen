// Feature: state-machine-source-generator, Property 8: Unreachable state warning
// Feature: state-machine-source-generator, Property 9: Terminal states are valid
// **Validates: Requirements 2.6, 2.7**

using System.Collections.Immutable;
using System.Linq;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.CodeAnalysis;
using StateMachineSrcGen;
using StateMachineSrcGen.Analysis;

namespace StateMachineSrcGen.Tests.Analysis;

/// <summary>
/// Property 8: Unreachable state warning — non-initial state with no inbound transition emits SMSG009
/// Property 9: Terminal states are valid — states with no outbound transitions produce no diagnostic
/// </summary>
public class ReachabilityProperties
{
    [Property]
    public bool UnreachableState_NonInitialNoInbound_EmitsSMSG009(
        NonEmptyString unreachableRaw, NonEmptyString initialRaw, NonEmptyString targetRaw)
    {
        var unreachable = ToIdentifier(unreachableRaw) + "Unreachable";
        var initial = ToIdentifier(initialRaw) + "Initial";
        var target = ToIdentifier(targetRaw) + "Target";

        // Ensure all names are unique
        if (initial == unreachable) unreachable += "X";
        if (initial == target) target += "Y";
        if (unreachable == target) target += "Z";

        // Create a state machine where 'unreachable' is not initial and not a target of any transition
        var states = new[]
        {
            AnalysisTestHelper.CreateState(initial, isInitial: true),
            AnalysisTestHelper.CreateState(target),
            AnalysisTestHelper.CreateState(unreachable)
        };
        var triggers = new[] { AnalysisTestHelper.CreateTrigger("Go") };
        var handlers = new[]
        {
            AnalysisTestHelper.CreateTransitionHandler("Handle", initial, target, "Go")
        };

        var input = AnalysisTestHelper.CreateStateMachine(states, triggers, handlers);
        var (_, diagnostics) = AnalysisPipeline.Analyze(input);

        // Should emit SMSG009 for the unreachable state
        return diagnostics.Any(d => d.Id == "SMSG009" && d.GetMessage().Contains(unreachable));
    }

    [Property]
    public bool InitialState_NeverUnreachable(NonEmptyString initialRaw)
    {
        var initial = ToIdentifier(initialRaw);

        // Initial state with no inbound transitions should NOT be flagged as unreachable
        var states = new[]
        {
            AnalysisTestHelper.CreateState(initial, isInitial: true),
            AnalysisTestHelper.CreateState(initial + "Other")
        };
        var triggers = new[] { AnalysisTestHelper.CreateTrigger("Go") };
        var handlers = new[]
        {
            AnalysisTestHelper.CreateTransitionHandler("Handle", initial, initial + "Other", "Go")
        };

        var input = AnalysisTestHelper.CreateStateMachine(states, triggers, handlers);
        var (_, diagnostics) = AnalysisPipeline.Analyze(input);

        // Initial state should never be flagged as unreachable
        return !diagnostics.Any(d => d.Id == "SMSG009" && d.GetMessage().Contains(initial)
            && !d.GetMessage().Contains(initial + "Other"));
    }

    [Property]
    public bool TerminalState_NoOutboundTransitions_NoDiagnostic(
        NonEmptyString initialRaw, NonEmptyString terminalRaw)
    {
        var initial = ToIdentifier(initialRaw) + "Init";
        var terminal = ToIdentifier(terminalRaw) + "Term";
        if (initial == terminal) terminal += "X";

        // Terminal state: has inbound transition but no outbound transitions
        var states = new[]
        {
            AnalysisTestHelper.CreateState(initial, isInitial: true),
            AnalysisTestHelper.CreateState(terminal)
        };
        var triggers = new[] { AnalysisTestHelper.CreateTrigger("Go") };
        var handlers = new[]
        {
            AnalysisTestHelper.CreateTransitionHandler("Handle", initial, terminal, "Go")
        };

        var input = AnalysisTestHelper.CreateStateMachine(states, triggers, handlers);
        var (result, diagnostics) = AnalysisPipeline.Analyze(input);

        // Terminal state should not produce any diagnostic
        // And the result should mark it as terminal
        var noTerminalDiagnostic = !diagnostics.Any(d =>
            d.GetMessage().Contains(terminal) && d.Severity == DiagnosticSeverity.Error);

        // If result is not null, verify terminal state is marked correctly
        if (result != null)
        {
            var terminalState = result.Value.States.FirstOrDefault(s => s.Name == terminal);
            return noTerminalDiagnostic && terminalState.IsTerminal;
        }

        return noTerminalDiagnostic;
    }

    [Property]
    public bool ReachableState_HasInboundTransition_NoSMSG009(
        NonEmptyString initialRaw, NonEmptyString reachableRaw)
    {
        var initial = ToIdentifier(initialRaw) + "Init";
        var reachable = ToIdentifier(reachableRaw) + "Reach";
        if (initial == reachable) reachable += "X";

        // State that IS a target of a transition should not be flagged
        var states = new[]
        {
            AnalysisTestHelper.CreateState(initial, isInitial: true),
            AnalysisTestHelper.CreateState(reachable)
        };
        var triggers = new[] { AnalysisTestHelper.CreateTrigger("Go") };
        var handlers = new[]
        {
            AnalysisTestHelper.CreateTransitionHandler("Handle", initial, reachable, "Go")
        };

        var input = AnalysisTestHelper.CreateStateMachine(states, triggers, handlers);
        var (_, diagnostics) = AnalysisPipeline.Analyze(input);

        return !diagnostics.Any(d => d.Id == "SMSG009" && d.GetMessage().Contains(reachable));
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
