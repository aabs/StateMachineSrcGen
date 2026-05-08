using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using StateMachineSrcGen.Diagnostics;

namespace StateMachineSrcGen.Analysis;

/// <summary>
/// Validates trigger declarations: duplicate trigger names.
/// </summary>
internal static class TriggerValidator
{
    /// <summary>
    /// Validates trigger declarations and returns any diagnostics.
    /// In the new generic API, triggers are derived from enum members (Events).
    /// Duplicate detection is based on enum member names.
    /// </summary>
    public static ImmutableArray<Diagnostic> Validate(ParsedStateMachine input)
    {
        var diagnostics = new List<Diagnostic>();

        // SMSG008: Duplicate trigger/event names
        var eventNameGroups = input.Events
            .GroupBy(t => t.Name)
            .Where(g => g.Count() > 1);

        foreach (var group in eventNameGroups)
        {
            foreach (var evt in group.Skip(1))
            {
                diagnostics.Add(Diagnostic.Create(
                    DiagnosticDescriptors.DuplicateTriggerNames,
                    evt.Location,
                    group.Key));
            }
        }

        return diagnostics.ToImmutableArray();
    }
}
