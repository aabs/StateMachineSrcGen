// Feature: state-machine-source-generator, Property 21: Errors prevent source emission
// Feature: state-machine-source-generator, Property 22: Diagnostics include message and location
// **Validates: Requirements 8.1, 8.3, 8.5**

using System.Collections.Immutable;
using System.Linq;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.CodeAnalysis;
using StateMachineSrcGen;
using StateMachineSrcGen.Analysis;

namespace StateMachineSrcGen.Tests.Analysis;

/// <summary>
/// Property 21: Errors prevent source emission — any Error-severity diagnostic means zero source files emitted (null result)
/// Property 22: Diagnostics include message and location — every diagnostic has non-empty message and non-null location
/// </summary>
public class ErrorEmissionProperties
{
    [Property]
    public bool ErrorDiagnostic_PreventsOutput_NullResult(NonEmptyString stateRaw)
    {
        var state = ToIdentifier(stateRaw);

        // Create a state machine that will produce an error (no initial state)
        var states = new[]
        {
            AnalysisTestHelper.CreateState(state, isInitial: false),
            AnalysisTestHelper.CreateState(state + "Other", isInitial: false)
        };
        var triggers = new[] { AnalysisTestHelper.CreateTrigger("Go") };
        var handlers = new[]
        {
            AnalysisTestHelper.CreateTransitionHandler("Handle", state, state + "Other", "Go")
        };

        var input = AnalysisTestHelper.CreateStateMachine(states, triggers, handlers);
        var (result, diagnostics) = AnalysisPipeline.Analyze(input);

        // If there are any Error-severity diagnostics, result must be null
        var hasErrors = diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);
        return hasErrors && result == null;
    }

    [Property]
    public bool WarningDiagnostic_DoesNotPreventOutput(
        NonEmptyString initialRaw, NonEmptyString unreachableRaw)
    {
        var initial = ToIdentifier(initialRaw) + "Init";
        var unreachable = ToIdentifier(unreachableRaw) + "Unreach";
        if (initial == unreachable) unreachable += "X";

        // Create a state machine with an unreachable state (warning only)
        // but otherwise valid
        var states = new[]
        {
            AnalysisTestHelper.CreateState(initial, isInitial: true),
            AnalysisTestHelper.CreateState(unreachable)
        };
        var triggers = new[] { AnalysisTestHelper.CreateTrigger("Go") };
        // No transition targets 'unreachable', so it gets SMSG009 (warning)
        // But we need at least one handler that references valid states
        var handlers = new[]
        {
            AnalysisTestHelper.CreateTransitionHandler("Handle", initial, initial, "Go")
        };

        var input = AnalysisTestHelper.CreateStateMachine(states, triggers, handlers);
        var (result, diagnostics) = AnalysisPipeline.Analyze(input);

        // Should have a warning but still produce output
        var hasWarning = diagnostics.Any(d => d.Severity == DiagnosticSeverity.Warning);
        var hasNoErrors = !diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);

        return hasWarning && hasNoErrors && result != null;
    }

    [Property]
    public bool AllDiagnostics_HaveNonEmptyMessage(NonEmptyString stateRaw)
    {
        var state = ToIdentifier(stateRaw);

        // Create various error conditions
        var states = new[]
        {
            AnalysisTestHelper.CreateState(state, isInitial: false),
            AnalysisTestHelper.CreateState(state, isInitial: false) // duplicate + no initial
        };
        var triggers = new[] { AnalysisTestHelper.CreateTrigger("Go") };
        var handlers = new[]
        {
            AnalysisTestHelper.CreateTransitionHandler("Handle", state, "Nonexistent", "Go")
        };

        var input = AnalysisTestHelper.CreateStateMachine(states, triggers, handlers);
        var (_, diagnostics) = AnalysisPipeline.Analyze(input);

        // Every diagnostic must have a non-empty message
        return diagnostics.All(d => !string.IsNullOrEmpty(d.GetMessage()));
    }

    [Property]
    public bool AllDiagnostics_HaveNonNullLocation(NonEmptyString stateRaw)
    {
        var state = ToIdentifier(stateRaw);

        // Create various error conditions
        var states = new[]
        {
            AnalysisTestHelper.CreateState(state, isInitial: false),
            AnalysisTestHelper.CreateState(state, isInitial: false) // duplicate + no initial
        };
        var triggers = new[] { AnalysisTestHelper.CreateTrigger("Go") };
        var handlers = new[]
        {
            AnalysisTestHelper.CreateTransitionHandler("Handle", state, "Nonexistent", "Go")
        };

        var input = AnalysisTestHelper.CreateStateMachine(states, triggers, handlers);
        var (_, diagnostics) = AnalysisPipeline.Analyze(input);

        // Every diagnostic must have a non-null location
        return diagnostics.All(d => d.Location != null);
    }

    [Property]
    public bool ValidInput_NoErrors_ProducesResult()
    {
        var input = AnalysisTestHelper.CreateValidStateMachine();
        var (result, diagnostics) = AnalysisPipeline.Analyze(input);

        var hasNoErrors = !diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);
        return hasNoErrors && result != null;
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
