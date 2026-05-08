using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using StateMachineSrcGen.Diagnostics;

namespace StateMachineSrcGen.Analysis;

/// <summary>
/// Validates enum-related constraints for the state machine:
/// - Detects [Flags] enums used as state/event ID types (SMSG026)
/// - Detects invalid enum values in attributes that don't resolve to enum members (SMSG018)
/// 
/// Note: [Flags] detection for the full pipeline is handled during parsing (DeclarationParser)
/// since the Roslyn type symbol is available there. This validator handles SMSG018 for
/// cases where parsed attribute values don't resolve to valid enum members (indicated by
/// null/empty state names in handlers or null InitialStateName).
/// </summary>
internal static class EnumValidator
{
    /// <summary>
    /// Validates enum-related constraints and returns any diagnostics.
    /// </summary>
    public static ImmutableArray<Diagnostic> Validate(ParsedStateMachine input)
    {
        var diagnostics = new List<Diagnostic>();

        // Build the set of valid state and event names from the parsed enum members
        var validStateNames = new HashSet<string>(input.States.Select(s => s.Name));
        var validEventNames = new HashSet<string>(input.Events.Select(e => e.Name));

        // SMSG018: Validate handler references resolve to valid enum members
        foreach (var handler in input.Handlers)
        {
            if (handler.Kind == HandlerKind.Cleanup || handler.Kind == HandlerKind.EntryCallback)
                continue;

            // Check FromState references a valid enum member
            if (!string.IsNullOrEmpty(handler.FromState) && validStateNames.Count > 0 &&
                !validStateNames.Contains(handler.FromState))
            {
                var validMembers = string.Join(", ", validStateNames);
                diagnostics.Add(Diagnostic.Create(
                    DiagnosticDescriptors.InvalidEnumValue,
                    handler.Location,
                    handler.FromState,
                    input.StateIdEnumTypeName,
                    validMembers));
            }

            // Check ToState references a valid enum member
            if (!string.IsNullOrEmpty(handler.ToState) && validStateNames.Count > 0 &&
                !validStateNames.Contains(handler.ToState))
            {
                var validMembers = string.Join(", ", validStateNames);
                diagnostics.Add(Diagnostic.Create(
                    DiagnosticDescriptors.InvalidEnumValue,
                    handler.Location,
                    handler.ToState,
                    input.StateIdEnumTypeName,
                    validMembers));
            }

            // Check Trigger references a valid enum member
            if (!string.IsNullOrEmpty(handler.Trigger) && validEventNames.Count > 0 &&
                !validEventNames.Contains(handler.Trigger))
            {
                var validMembers = string.Join(", ", validEventNames);
                diagnostics.Add(Diagnostic.Create(
                    DiagnosticDescriptors.InvalidEnumValue,
                    handler.Location,
                    handler.Trigger,
                    input.EventIdEnumTypeName,
                    validMembers));
            }
        }

        // SMSG018: Validate TerminalStateNames resolve to valid enum members
        if (validStateNames.Count > 0)
        {
            foreach (var terminalName in input.TerminalStateNames)
            {
                if (!validStateNames.Contains(terminalName))
                {
                    var validMembers = string.Join(", ", validStateNames);
                    diagnostics.Add(Diagnostic.Create(
                        DiagnosticDescriptors.InvalidEnumValue,
                        input.Location,
                        terminalName,
                        input.StateIdEnumTypeName,
                        validMembers));
                }
            }
        }

        return diagnostics.ToImmutableArray();
    }
}
