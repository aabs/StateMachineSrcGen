// Feature: state-machine-source-generator, Property 23: Pipeline stages never throw (analysis stage)
// **Validates: Requirements 9.1, 9.2, 9.3, 9.4**

using System;
using System.Collections.Immutable;
using System.Linq;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.CodeAnalysis;
using StateMachineSrcGen;
using StateMachineSrcGen.Analysis;

namespace StateMachineSrcGen.Tests.Analysis;

/// <summary>
/// Property 23: Pipeline stages never throw (analysis stage)
/// For any input (including malformed, null-containing, or edge-case inputs),
/// the analysis stage shall not throw an unhandled exception. It shall return
/// either a valid result or a collection of diagnostics.
/// </summary>
public class AnalysisReliabilityProperties
{
    [Property]
    public bool Analysis_ValidInput_NeverThrows(
        NonEmptyString classRaw, NonEmptyString stateRaw, NonEmptyString triggerRaw)
    {
        var className = ToIdentifier(classRaw);
        var stateName = ToIdentifier(stateRaw);
        var triggerName = ToIdentifier(triggerRaw);

        try
        {
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

            var (_, _) = AnalysisPipeline.Analyze(input);
            return true;
        }
        catch
        {
            return false;
        }
    }

    [Property]
    public bool Analysis_EmptyStates_NeverThrows(NonEmptyString triggerRaw)
    {
        var triggerName = ToIdentifier(triggerRaw);

        try
        {
            var input = AnalysisTestHelper.CreateStateMachine(
                states: Array.Empty<ParsedState>(),
                triggers: new[] { AnalysisTestHelper.CreateTrigger(triggerName) },
                handlers: Array.Empty<ParsedHandler>());

            var (_, _) = AnalysisPipeline.Analyze(input);
            return true;
        }
        catch
        {
            return false;
        }
    }

    [Property]
    public bool Analysis_EmptyTriggers_NeverThrows(NonEmptyString stateRaw)
    {
        var stateName = ToIdentifier(stateRaw);

        try
        {
            var input = AnalysisTestHelper.CreateStateMachine(
                states: new[] { AnalysisTestHelper.CreateState(stateName, isInitial: true) },
                triggers: Array.Empty<ParsedTrigger>(),
                handlers: Array.Empty<ParsedHandler>());

            var (_, _) = AnalysisPipeline.Analyze(input);
            return true;
        }
        catch
        {
            return false;
        }
    }

    [Property]
    public bool Analysis_EmptyHandlers_NeverThrows(NonEmptyString stateRaw, NonEmptyString triggerRaw)
    {
        var stateName = ToIdentifier(stateRaw);
        var triggerName = ToIdentifier(triggerRaw);

        try
        {
            var input = AnalysisTestHelper.CreateStateMachine(
                states: new[] { AnalysisTestHelper.CreateState(stateName, isInitial: true) },
                triggers: new[] { AnalysisTestHelper.CreateTrigger(triggerName) },
                handlers: Array.Empty<ParsedHandler>());

            var (_, _) = AnalysisPipeline.Analyze(input);
            return true;
        }
        catch
        {
            return false;
        }
    }

    [Property]
    public bool Analysis_DuplicateEverything_NeverThrows(NonEmptyString nameRaw)
    {
        var name = ToIdentifier(nameRaw);

        try
        {
            // Maximally duplicated input
            var input = AnalysisTestHelper.CreateStateMachine(
                states: new[]
                {
                    AnalysisTestHelper.CreateState(name, isInitial: true),
                    AnalysisTestHelper.CreateState(name, isInitial: true),
                    AnalysisTestHelper.CreateState(name, isInitial: false)
                },
                triggers: new[]
                {
                    AnalysisTestHelper.CreateTrigger(name),
                    AnalysisTestHelper.CreateTrigger(name)
                },
                handlers: new[]
                {
                    AnalysisTestHelper.CreateTransitionHandler("H1", name, name, name),
                    AnalysisTestHelper.CreateTransitionHandler("H2", name, name, name)
                });

            var (_, _) = AnalysisPipeline.Analyze(input);
            return true;
        }
        catch
        {
            return false;
        }
    }

    [Property]
    public bool Analysis_MissingModifiers_NeverThrows(NonEmptyString stateRaw)
    {
        var stateName = ToIdentifier(stateRaw);

        try
        {
            var input = AnalysisTestHelper.CreateStateMachine(
                states: new[] { AnalysisTestHelper.CreateState(stateName, isInitial: true) },
                triggers: new[] { AnalysisTestHelper.CreateTrigger("Go") },
                handlers: Array.Empty<ParsedHandler>(),
                modifiers: ClassModifiers.None,
                implementsIStateMachine: false,
                implementsIStatePersistence: false,
                implementsIDispatchableEvent: false);

            var (_, _) = AnalysisPipeline.Analyze(input);
            return true;
        }
        catch
        {
            return false;
        }
    }

    [Property]
    public bool Analysis_UndefinedReferences_NeverThrows(
        NonEmptyString fromRaw, NonEmptyString toRaw, NonEmptyString triggerRaw)
    {
        var from = ToIdentifier(fromRaw);
        var to = ToIdentifier(toRaw);
        var trigger = ToIdentifier(triggerRaw);

        try
        {
            // Handler references states/triggers that don't exist
            var input = AnalysisTestHelper.CreateStateMachine(
                states: new[] { AnalysisTestHelper.CreateState("OnlyState", isInitial: true) },
                triggers: new[] { AnalysisTestHelper.CreateTrigger("OnlyTrigger") },
                handlers: new[]
                {
                    AnalysisTestHelper.CreateTransitionHandler("Handle", from, to, trigger)
                });

            var (_, _) = AnalysisPipeline.Analyze(input);
            return true;
        }
        catch
        {
            return false;
        }
    }

    [Property]
    public bool Analysis_InvalidSignatures_NeverThrows(NonEmptyString stateRaw)
    {
        var stateName = ToIdentifier(stateRaw);

        try
        {
            var input = AnalysisTestHelper.CreateStateMachine(
                states: new[]
                {
                    AnalysisTestHelper.CreateState(stateName, isInitial: true),
                    AnalysisTestHelper.CreateState(stateName + "Target")
                },
                triggers: new[] { AnalysisTestHelper.CreateTrigger("Go") },
                handlers: new[]
                {
                    AnalysisTestHelper.CreateHandlerWithSignature(
                        "BadHandler", stateName, stateName + "Target", "Go",
                        HandlerKind.Transition,
                        isPublic: false, isStatic: false,
                        returnType: "void",
                        parameters: Array.Empty<ParameterInfo>())
                });

            var (_, _) = AnalysisPipeline.Analyze(input);
            return true;
        }
        catch
        {
            return false;
        }
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
