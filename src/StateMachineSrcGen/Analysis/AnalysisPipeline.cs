using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using StateMachineSrcGen.Diagnostics;

namespace StateMachineSrcGen.Analysis;

/// <summary>
/// Entry point for the analysis stage. Accepts a <see cref="ParsedStateMachine"/>
/// and returns either a validated model or diagnostics.
/// </summary>
public static class AnalysisPipeline
{
    /// <summary>
    /// Analyzes a parsed state machine model, validating its semantic correctness.
    /// Returns a validated model if no errors are found, or null with diagnostics on failure.
    /// </summary>
    /// <param name="input">The parsed state machine to analyze.</param>
    /// <returns>A tuple of the validated result (or null on error) and any diagnostics emitted.</returns>
    public static (ValidatedStateMachine? Result, ImmutableArray<Diagnostic> Diagnostics) Analyze(
        ParsedStateMachine input)
    {
        try
        {
            var allDiagnostics = new List<Diagnostic>();

            // Run all validators
            allDiagnostics.AddRange(StructureValidator.Validate(input));
            allDiagnostics.AddRange(StateValidator.Validate(input));
            allDiagnostics.AddRange(TriggerValidator.Validate(input));
            allDiagnostics.AddRange(TransitionValidator.Validate(input));
            allDiagnostics.AddRange(SignatureValidator.Validate(input));

            var diagnostics = allDiagnostics.ToImmutableArray();

            // If any Error-severity diagnostic exists, return null result
            if (diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error))
            {
                return (null, diagnostics);
            }

            // Build ValidatedStateMachine
            var validatedResult = BuildValidatedModel(input);
            return (validatedResult, diagnostics);
        }
        catch (Exception ex)
        {
            // SMSG015: Internal generator error — never let exceptions escape
            var diagnostic = Diagnostic.Create(
                DiagnosticDescriptors.InternalGeneratorError,
                input.Location,
                ex.Message);

            return (null, ImmutableArray.Create(diagnostic));
        }
    }

    private static ValidatedStateMachine BuildValidatedModel(ParsedStateMachine input)
    {
        // Collect all target states from transitions to determine terminal states
        var targetStates = new HashSet<string>(
            input.Handlers
                .Where(h => h.Kind == HandlerKind.Transition)
                .Select(h => h.ToState));

        // Collect all source states from transitions to determine terminal states
        var sourceStates = new HashSet<string>(
            input.Handlers
                .Where(h => h.Kind == HandlerKind.Transition)
                .Select(h => h.FromState));

        // Build validated states
        var validatedStates = input.States.Select(s => new ValidatedState
        {
            Name = s.Name,
            IsInitial = s.IsInitial,
            IsTerminal = !sourceStates.Contains(s.Name)
        }).ToImmutableArray();

        var initialState = validatedStates.First(s => s.IsInitial);

        // Build validated transitions from transition handlers
        var transitionHandlers = input.Handlers
            .Where(h => h.Kind == HandlerKind.Transition)
            .ToList();

        // Build a lookup for guards and side effects by (From, To, Trigger)
        var guards = input.Handlers
            .Where(h => h.Kind == HandlerKind.Guard)
            .ToDictionary(h => (h.FromState, h.ToState, h.Trigger), h => h.MethodName);

        var sideEffects = input.Handlers
            .Where(h => h.Kind == HandlerKind.SideEffect)
            .ToDictionary(h => (h.FromState, h.ToState, h.Trigger), h => h.MethodName);

        var validatedTransitions = transitionHandlers.Select((h, index) =>
        {
            var key = (h.FromState, h.ToState, h.Trigger);
            guards.TryGetValue(key, out var guardMethod);
            sideEffects.TryGetValue(key, out var sideEffectMethod);

            return new ValidatedTransition
            {
                FromState = h.FromState,
                ToState = h.ToState,
                Trigger = h.Trigger,
                EventId = h.EventId ?? "",
                HandlerMethodName = h.MethodName,
                GuardMethodName = guardMethod,
                SideEffectMethodName = sideEffectMethod,
                DeclarationOrder = index
            };
        }).ToImmutableArray();

        return new ValidatedStateMachine
        {
            Namespace = input.Namespace,
            ClassName = input.ClassName,
            StateTypeName = input.StateTypeName,
            EventTypeName = input.EventTypeName,
            EventIdTypeName = input.EventIdTypeName ?? "string",
            States = new EquatableArray<ValidatedState>(validatedStates),
            InitialState = initialState,
            Transitions = new EquatableArray<ValidatedTransition>(validatedTransitions)
        };
    }
}
