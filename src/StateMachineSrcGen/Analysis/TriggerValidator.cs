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
    /// </summary>
    public static ImmutableArray<Diagnostic> Validate(ParsedStateMachine input)
    {
        var diagnostics = new List<Diagnostic>();

        // SMSG008: Duplicate trigger names
        var triggerNameGroups = input.Triggers
            .GroupBy(t => t.Name)
            .Where(g => g.Count() > 1);

        foreach (var group in triggerNameGroups)
        {
            foreach (var trigger in group.Skip(1))
            {
                diagnostics.Add(Diagnostic.Create(
                    DiagnosticDescriptors.DuplicateTriggerNames,
                    trigger.Location,
                    group.Key));
            }
        }

        return diagnostics.ToImmutableArray();
    }
}
