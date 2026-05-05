// Feature: state-machine-source-generator, Property 23: Pipeline stages never throw (generation stage)
// **Validates: Requirements 9.3, 9.4**

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
/// Property 23: Pipeline stages never throw (generation stage)
/// For any valid input, the generation stage shall not throw an unhandled exception.
/// It shall return either a valid result or a collection of diagnostics.
/// </summary>
public class GenerationReliabilityProperties
{
    [Property]
    public bool Generation_ValidInput_NeverThrows(
        NonEmptyString classRaw, NonEmptyString nsRaw, NonEmptyString stateRaw)
    {
        var className = ToIdentifier(classRaw);
        var ns = ToIdentifier(nsRaw);
        var stateName = ToIdentifier(stateRaw);

        try
        {
            var input = GenerationTestHelper.CreateValidStateMachine(
                className: className, ns: ns, stateType: "string");

            var (_, _) = GenerationPipeline.Generate(input);
            return true;
        }
        catch
        {
            return false;
        }
    }

    [Property]
    public bool Generation_ComplexInput_NeverThrows(
        NonEmptyString classRaw, NonEmptyString nsRaw)
    {
        var className = ToIdentifier(classRaw);
        var ns = ToIdentifier(nsRaw);

        try
        {
            var input = GenerationTestHelper.CreateComplexStateMachine(
                className: className, ns: ns);

            var (_, _) = GenerationPipeline.Generate(input);
            return true;
        }
        catch
        {
            return false;
        }
    }

    [Property]
    public bool Generation_EmptyTransitions_NeverThrows(
        NonEmptyString classRaw, NonEmptyString nsRaw)
    {
        var className = ToIdentifier(classRaw);
        var ns = ToIdentifier(nsRaw);

        try
        {
            var idle = new ValidatedState { Name = "Idle", IsInitial = true, IsTerminal = true };

            var input = GenerationTestHelper.CreateStateMachine(
                className: className,
                ns: ns,
                stateType: "string",
                eventType: "TestEvent",
                eventIdType: "string",
                states: new[] { idle },
                transitions: Array.Empty<ValidatedTransition>());

            var (_, _) = GenerationPipeline.Generate(input);
            return true;
        }
        catch
        {
            return false;
        }
    }

    [Property]
    public bool Generation_ManyTransitions_NeverThrows(
        NonEmptyString classRaw, PositiveInt transitionCount)
    {
        var className = ToIdentifier(classRaw);
        var count = Math.Min(transitionCount.Get, 20); // Cap at 20 for performance

        try
        {
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
                className: className,
                ns: "TestNamespace",
                stateType: "string",
                eventType: "TestEvent",
                eventIdType: "string",
                states: states,
                transitions: transitions);

            var (_, _) = GenerationPipeline.Generate(input);
            return true;
        }
        catch
        {
            return false;
        }
    }

    [Property]
    public bool Generation_WithGuardsAndSideEffects_NeverThrows(
        NonEmptyString classRaw, NonEmptyString nsRaw)
    {
        var className = ToIdentifier(classRaw);
        var ns = ToIdentifier(nsRaw);

        try
        {
            var idle = new ValidatedState { Name = "Idle", IsInitial = true, IsTerminal = false };
            var active = new ValidatedState { Name = "Active", IsInitial = false, IsTerminal = true };

            var input = GenerationTestHelper.CreateStateMachine(
                className: className,
                ns: ns,
                stateType: "string",
                eventType: "TestEvent",
                eventIdType: "string",
                states: new[] { idle, active },
                transitions: new[]
                {
                    new ValidatedTransition
                    {
                        FromState = "Idle",
                        ToState = "Active",
                        Trigger = "Activate",
                        EventId = "Activate",
                        HandlerMethodName = "HandleActivate",
                        GuardMethodName = "CanActivate",
                        SideEffectMethodName = "OnActivated",
                        DeclarationOrder = 0
                    }
                });

            var (_, _) = GenerationPipeline.Generate(input);
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
