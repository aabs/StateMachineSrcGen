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
            allDiagnostics.AddRange(EnumValidator.Validate(input));
            allDiagnostics.AddRange(StateValidator.Validate(input));
            allDiagnostics.AddRange(TriggerValidator.Validate(input));
            allDiagnostics.AddRange(TransitionValidator.Validate(input));
            allDiagnostics.AddRange(SignatureValidator.Validate(input));
            allDiagnostics.AddRange(EntryCallbackValidator.Validate(input));

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
        // Build a lookup of state enum values from ParsedState/ParsedEvent
        var stateEnumValues = new Dictionary<string, int>();
        var stateIndex = 0;
        foreach (var state in input.States)
        {
            stateEnumValues[state.Name] = stateIndex++;
        }

        var eventEnumValues = new Dictionary<string, int>();
        foreach (var evt in input.Events)
        {
            eventEnumValues[evt.Name] = evt.IntValue;
        }

        // Determine terminal state names set
        var terminalStateNames = new HashSet<string>(input.TerminalStateNames);

        // Collect all source states from transitions to determine terminal states
        // (states with no outbound transitions are also considered terminal)
        var sourceStates = new HashSet<string>(
            input.Handlers
                .Where(h => h.Kind == HandlerKind.Transition)
                .Select(h => h.FromState));

        // Build validated states
        var validatedStates = input.States.Select(s => new ValidatedState
        {
            Name = s.Name,
            EnumValue = stateEnumValues.TryGetValue(s.Name, out var ev) ? ev : 0,
            IsInitial = s.IsInitial,
            IsTerminal = terminalStateNames.Contains(s.Name) || !sourceStates.Contains(s.Name)
        }).ToImmutableArray();

        var initialState = validatedStates.FirstOrDefault(s => s.IsInitial);

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

            var fromEnumValue = stateEnumValues.TryGetValue(h.FromState, out var fev) ? fev : 0;
            var toEnumValue = stateEnumValues.TryGetValue(h.ToState, out var tev) ? tev : 0;
            var triggerEnumValue = eventEnumValues.TryGetValue(h.Trigger, out var trv) ? trv : 0;

            return new ValidatedTransition
            {
                FromState = h.FromState,
                ToState = h.ToState,
                Trigger = h.Trigger,
                FromStateEnumValue = fromEnumValue,
                ToStateEnumValue = toEnumValue,
                TriggerEnumValue = triggerEnumValue,
                EventId = h.EventId ?? h.Trigger,
                HandlerMethodName = h.MethodName,
                GuardMethodName = guardMethod,
                SideEffectMethodName = sideEffectMethod,
                IsTerminal = terminalStateNames.Contains(h.ToState),
                DeclarationOrder = index
            };
        }).ToImmutableArray();

        // Build validated entry callbacks
        var validatedEntryCallbacks = input.EntryCallbacks.Select(cb => new ValidatedEntryCallback
        {
            MethodName = cb.MethodName,
            TargetStateName = cb.TargetStateName,
            IsCatchAll = cb.IsCatchAll,
            ReturnsTState = !cb.IsCatchAll // Targeted returns TState, catch-all returns void
        }).ToImmutableArray();

        // Get cleanup handler method name
        var cleanupHandlerMethodName = input.CleanupHandler?.MethodName;

        return new ValidatedStateMachine
        {
            Namespace = input.Namespace,
            ClassName = input.ClassName,
            StateIdEnumTypeName = input.StateIdEnumTypeName,
            EventIdEnumTypeName = input.EventIdEnumTypeName,
            StateTypeName = input.StateTypeName,
            EventTypeName = input.EventTypeName,
            States = new EquatableArray<ValidatedState>(validatedStates),
            InitialState = initialState,
            Transitions = new EquatableArray<ValidatedTransition>(validatedTransitions),
            EntryCallbacks = new EquatableArray<ValidatedEntryCallback>(validatedEntryCallbacks),
            CleanupHandlerMethodName = cleanupHandlerMethodName
        };
    }
}
