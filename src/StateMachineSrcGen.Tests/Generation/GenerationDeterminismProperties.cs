// Feature: state-machine-source-generator, Property 25: Pipeline stage determinism (generation stage)
// **Validates: Requirements 11.3**

using System;
using System.Collections.Immutable;
using System.Linq;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.CodeAnalysis;
using StateMachineSrcGen;
using StateMachineSrcGen.Generation;

namespace StateMachineSrcGen.Tests.Generation;

/// <summary>
/// Property 25: Pipeline stage determinism (generation stage)
/// For any input to the generation stage, invoking the stage function multiple times
/// with the same input shall produce identical output each time.
/// </summary>
public class GenerationDeterminismProperties
{
    [Property]
    public bool Generation_SameInput_ProducesIdenticalOutput(
        NonEmptyString classRaw, NonEmptyString nsRaw)
    {
        var className = ToIdentifier(classRaw);
        var ns = ToIdentifier(nsRaw);

        var input = GenerationTestHelper.CreateValidStateMachine(className: className, ns: ns);

        var (source1, diag1) = GenerationPipeline.Generate(input);
        var (source2, diag2) = GenerationPipeline.Generate(input);
        var (source3, diag3) = GenerationPipeline.Generate(input);

        var sourcesEqual = source1 == source2 && source2 == source3;
        var diagsEqual = DiagnosticsAreEqual(diag1, diag2) && DiagnosticsAreEqual(diag2, diag3);

        return sourcesEqual && diagsEqual;
    }

    [Property]
    public bool Generation_ComplexInput_DeterministicOutput(
        NonEmptyString classRaw, NonEmptyString nsRaw)
    {
        var className = ToIdentifier(classRaw);
        var ns = ToIdentifier(nsRaw);

        var input = GenerationTestHelper.CreateComplexStateMachine(className: className, ns: ns);

        var (source1, diag1) = GenerationPipeline.Generate(input);
        var (source2, diag2) = GenerationPipeline.Generate(input);
        var (source3, diag3) = GenerationPipeline.Generate(input);

        var sourcesEqual = source1 == source2 && source2 == source3;
        var diagsEqual = DiagnosticsAreEqual(diag1, diag2) && DiagnosticsAreEqual(diag2, diag3);

        return sourcesEqual && diagsEqual;
    }

    [Property]
    public bool Generation_EmptyTransitions_DeterministicOutput(
        NonEmptyString classRaw, NonEmptyString nsRaw)
    {
        var className = ToIdentifier(classRaw);
        var ns = ToIdentifier(nsRaw);

        var idle = new ValidatedState { Name = "Idle", IsInitial = true, IsTerminal = true };

        var input = GenerationTestHelper.CreateStateMachine(
            className: className,
            ns: ns,
            stateType: "string",
            eventType: "TestEvent",
            eventIdType: "string",
            states: new[] { idle },
            transitions: Array.Empty<ValidatedTransition>());

        var (source1, diag1) = GenerationPipeline.Generate(input);
        var (source2, diag2) = GenerationPipeline.Generate(input);

        var sourcesEqual = source1 == source2;
        var diagsEqual = DiagnosticsAreEqual(diag1, diag2);

        return sourcesEqual && diagsEqual;
    }

    [Property]
    public bool Generation_MultipleTransitions_DeterministicOutput(PositiveInt transitionCount)
    {
        var count = Math.Min(transitionCount.Get, 10);

        var states = Enumerable.Range(0, count + 1)
            .Select(i => new ValidatedState
            {
                Name = $"State{i}",
                IsInitial = i == 0,
                IsTerminal = i == count
            })
            .ToArray();

        var transitions = Enumerable.Range(0, count)
            .Select(i => new ValidatedTransition
            {
                FromState = $"State{i}",
                ToState = $"State{i + 1}",
                Trigger = $"Go{i}",
                EventId = $"Go{i}",
                HandlerMethodName = $"Handle{i}",
                GuardMethodName = null,
                SideEffectMethodName = null,
                DeclarationOrder = i
            })
            .ToArray();

        var input = GenerationTestHelper.CreateStateMachine(
            className: "TestMachine",
            ns: "TestNamespace",
            stateType: "string",
            eventType: "TestEvent",
            eventIdType: "string",
            states: states,
            transitions: transitions);

        var (source1, diag1) = GenerationPipeline.Generate(input);
        var (source2, diag2) = GenerationPipeline.Generate(input);

        var sourcesEqual = source1 == source2;
        var diagsEqual = DiagnosticsAreEqual(diag1, diag2);

        return sourcesEqual && diagsEqual;
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
