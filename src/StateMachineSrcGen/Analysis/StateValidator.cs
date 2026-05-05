using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using StateMachineSrcGen.Diagnostics;

namespace StateMachineSrcGen.Analysis;

/// <summary>
/// Validates state declarations: empty states, initial state cardinality,
/// duplicate state names, and unreachable states.
/// </summary>
internal static class StateValidator
{
    /// <summary>
    /// Validates state declarations and returns any diagnostics.
    /// </summary>
    public static ImmutableArray<Diagnostic> Validate(ParsedStateMachine input)
    {
        var diagnostics = new List<Diagnostic>();

        // SMSG004: No states declared
        if (!input.States.Any())
        {
            diagnostics.Add(Diagnostic.Create(
                DiagnosticDescriptors.NoStatesDeclared,
                input.Location));
            return diagnostics.ToImmutableArray();
        }

        // SMSG007: Duplicate state names
        var stateNameGroups = input.States
            .GroupBy(s => s.Name)
            .Where(g => g.Count() > 1);

        foreach (var group in stateNameGroups)
        {
            foreach (var state in group.Skip(1))
            {
                diagnostics.Add(Diagnostic.Create(
                    DiagnosticDescriptors.DuplicateStateNames,
                    state.Location,
                    group.Key));
            }
        }

        // SMSG005/SMSG006: Initial state cardinality
        var initialStates = input.States.Where(s => s.IsInitial).ToList();
        if (initialStates.Count == 0)
        {
            diagnostics.Add(Diagnostic.Create(
                DiagnosticDescriptors.NoInitialState,
                input.Location));
        }
        else if (initialStates.Count > 1)
        {
            var names = string.Join(", ", initialStates.Select(s => s.Name));
            diagnostics.Add(Diagnostic.Create(
                DiagnosticDescriptors.MultipleInitialStates,
                input.Location,
                names));
        }

        // SMSG009: Unreachable states (warning)
        var targetStates = new HashSet<string>(
            input.Handlers
                .Where(h => h.Kind == HandlerKind.Transition)
                .Select(h => h.ToState));

        foreach (var state in input.States)
        {
            if (!state.IsInitial && !targetStates.Contains(state.Name))
            {
                diagnostics.Add(Diagnostic.Create(
                    DiagnosticDescriptors.UnreachableState,
                    state.Location,
                    state.Name));
            }
        }

        return diagnostics.ToImmutableArray();
    }
}
