// Feature: state-machine-source-generator, Property 25: Pipeline stage determinism (analysis stage)
// **Validates: Requirements 11.2**

using System.Collections.Immutable;
using System.Linq;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.CodeAnalysis;
using StateMachineSrcGen;
using StateMachineSrcGen.Analysis;

namespace StateMachineSrcGen.Tests.Analysis;

/// <summary>
/// Property 25: Pipeline stage determinism (analysis stage)
/// For any input to the analysis stage, invoking the stage function multiple times
/// with the same input shall produce identical output each time.
/// </summary>
public class AnalysisDeterminismProperties
{
    [Property]
    public bool Analysis_SameInput_ProducesIdenticalOutput(
        NonEmptyString stateRaw, NonEmptyString triggerRaw)
    {
        var stateName = ToIdentifier(stateRaw);
        var triggerName = ToIdentifier(triggerRaw);

        var input = AnalysisTestHelper.CreateStateMachine(
            states: new[]
            {
                AnalysisTestHelper.CreateState(stateName, isInitial: true),
                AnalysisTestHelper.CreateState(stateName + "Target")
            },
            triggers: new[] { AnalysisTestHelper.CreateTrigger(triggerName) },
            handlers: new[]
            {
                AnalysisTestHelper.CreateTransitionHandler("Handle", stateName, stateName + "Target", triggerName)
            });

        var (result1, diag1) = AnalysisPipeline.Analyze(input);
        var (result2, diag2) = AnalysisPipeline.Analyze(input);
        var (result3, diag3) = AnalysisPipeline.Analyze(input);

        var resultsEqual = Equals(result1, result2) && Equals(result2, result3);
        var diagsEqual = DiagnosticsAreEqual(diag1, diag2) && DiagnosticsAreEqual(diag2, diag3);

        return resultsEqual && diagsEqual;
    }

    [Property]
    public bool Analysis_InvalidInput_DeterministicDiagnostics(NonEmptyString stateRaw)
    {
        var stateName = ToIdentifier(stateRaw);

        // Input with errors (no initial state, duplicate states)
        var input = AnalysisTestHelper.CreateStateMachine(
            states: new[]
            {
                AnalysisTestHelper.CreateState(stateName, isInitial: false),
                AnalysisTestHelper.CreateState(stateName, isInitial: false)
            },
            triggers: new[] { AnalysisTestHelper.CreateTrigger("Go") },
            handlers: new[]
            {
                AnalysisTestHelper.CreateTransitionHandler("Handle", stateName, "Nonexistent", "Go")
            });

        var (result1, diag1) = AnalysisPipeline.Analyze(input);
        var (result2, diag2) = AnalysisPipeline.Analyze(input);
        var (result3, diag3) = AnalysisPipeline.Analyze(input);

        var resultsEqual = Equals(result1, result2) && Equals(result2, result3);
        var diagsEqual = DiagnosticsAreEqual(diag1, diag2) && DiagnosticsAreEqual(diag2, diag3);

        return resultsEqual && diagsEqual;
    }

    [Property]
    public bool Analysis_EmptyInput_DeterministicDiagnostics()
    {
        var input = AnalysisTestHelper.CreateStateMachine(
            states: System.Array.Empty<ParsedState>(),
            triggers: System.Array.Empty<ParsedTrigger>(),
            handlers: System.Array.Empty<ParsedHandler>());

        var (result1, diag1) = AnalysisPipeline.Analyze(input);
        var (result2, diag2) = AnalysisPipeline.Analyze(input);

        var resultsEqual = Equals(result1, result2);
        var diagsEqual = DiagnosticsAreEqual(diag1, diag2);

        return resultsEqual && diagsEqual;
    }

    [Property]
    public bool Analysis_WarningInput_DeterministicOutput(
        NonEmptyString initialRaw, NonEmptyString unreachableRaw)
    {
        var initial = ToIdentifier(initialRaw) + "Init";
        var unreachable = ToIdentifier(unreachableRaw) + "Unreach";
        if (initial == unreachable) unreachable += "X";

        // Input that produces warnings (unreachable state) but no errors
        var input = AnalysisTestHelper.CreateStateMachine(
            states: new[]
            {
                AnalysisTestHelper.CreateState(initial, isInitial: true),
                AnalysisTestHelper.CreateState(unreachable)
            },
            triggers: new[] { AnalysisTestHelper.CreateTrigger("Go") },
            handlers: new[]
            {
                AnalysisTestHelper.CreateTransitionHandler("Handle", initial, initial, "Go")
            });

        var (result1, diag1) = AnalysisPipeline.Analyze(input);
        var (result2, diag2) = AnalysisPipeline.Analyze(input);

        var resultsEqual = Equals(result1, result2);
        var diagsEqual = DiagnosticsAreEqual(diag1, diag2);

        return resultsEqual && diagsEqual;
    }

    // ─── Helpers ────────────────────────────────────────────────────────────────

    private static bool DiagnosticsAreEqual(ImmutableArray<Diagnostic> a, ImmutableArray<Diagnostic> b)
    {
        if (a.Length != b.Length)
            return false;

        for (int i = 0; i < a.Length; i++)
        {
            if (a[i].Id != b[i].Id ||
                a[i].Severity != b[i].Severity ||
                a[i].GetMessage() != b[i].GetMessage())
            {
                return false;
            }
        }

        return true;
    }

    private static string ToIdentifier(NonEmptyString raw)
    {
        var filtered = new string(raw.Get.Where(char.IsLetterOrDigit).ToArray());
        if (string.IsNullOrEmpty(filtered) || !char.IsLetter(filtered[0]))
            return "X" + filtered;
        return filtered;
    }
}
