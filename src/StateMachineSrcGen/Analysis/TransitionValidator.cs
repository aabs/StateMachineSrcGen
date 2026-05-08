using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using StateMachineSrcGen.Diagnostics;

namespace StateMachineSrcGen.Analysis;

/// <summary>
/// Validates transition handlers: duplicate handlers (SMSG001), missing target states (SMSG014),
/// and undefined state/trigger references (SMSG002/SMSG003).
/// In the enum-based API, SMSG002/SMSG003 are effectively redundant because enum membership
/// ensures valid references. However, the checks are preserved for completeness and for
/// scenarios where the parsed model has inconsistencies.
/// </summary>
internal static class TransitionValidator
{
    /// <summary>
    /// Validates transition handlers and returns any diagnostics.
    /// </summary>
    public static ImmutableArray<Diagnostic> Validate(ParsedStateMachine input)
    {
        var diagnostics = new List<Diagnostic>();

        var declaredStates = new HashSet<string>(input.States.Select(s => s.Name));
        var declaredTriggers = new HashSet<string>(input.Events.Select(t => t.Name));

        // Track seen (From, To, Trigger) triples for duplicate detection
        var seenTransitions = new HashSet<(string From, string To, string Trigger)>();

        foreach (var handler in input.Handlers)
        {
            // Skip non-transition handlers for transition-specific checks
            if (handler.Kind == HandlerKind.Cleanup || handler.Kind == HandlerKind.EntryCallback)
                continue;

            if (handler.Kind == HandlerKind.Transition)
            {
                // SMSG001: Duplicate transition handler
                var triple = (handler.FromState, handler.ToState, handler.Trigger);
                if (!seenTransitions.Add(triple))
                {
                    diagnostics.Add(Diagnostic.Create(
                        DiagnosticDescriptors.DuplicateTransitionHandler,
                        handler.Location,
                        handler.FromState,
                        handler.ToState,
                        handler.Trigger));
                }

                // SMSG014: Missing target state (empty ToState)
                if (string.IsNullOrEmpty(handler.ToState))
                {
                    diagnostics.Add(Diagnostic.Create(
                        DiagnosticDescriptors.MissingTargetState,
                        handler.Location,
                        handler.FromState,
                        handler.Trigger));
                }
            }

            // SMSG002: Undefined state referenced (FromState)
            if (!string.IsNullOrEmpty(handler.FromState) && !declaredStates.Contains(handler.FromState))
            {
                diagnostics.Add(Diagnostic.Create(
                    DiagnosticDescriptors.UndefinedStateReferenced,
                    handler.Location,
                    handler.FromState));
            }

            // SMSG002: Undefined state referenced (ToState)
            if (!string.IsNullOrEmpty(handler.ToState) && !declaredStates.Contains(handler.ToState))
            {
                diagnostics.Add(Diagnostic.Create(
                    DiagnosticDescriptors.UndefinedStateReferenced,
                    handler.Location,
                    handler.ToState));
            }

            // SMSG003: Undefined trigger referenced
            if (!string.IsNullOrEmpty(handler.Trigger) && !declaredTriggers.Contains(handler.Trigger))
            {
                diagnostics.Add(Diagnostic.Create(
                    DiagnosticDescriptors.UndefinedTriggerReferenced,
                    handler.Location,
                    handler.Trigger));
            }
        }

        return diagnostics.ToImmutableArray();
    }
}
