using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using StateMachineSrcGen.Diagnostics;

namespace StateMachineSrcGen.Analysis;

/// <summary>
/// Validates the structural shape of the state machine class declaration:
/// public, partial, static modifiers and IDispatchableEvent on the event type.
/// </summary>
internal static class StructureValidator
{
    /// <summary>
    /// Validates the class structure and returns any diagnostics.
    /// </summary>
    public static ImmutableArray<Diagnostic> Validate(ParsedStateMachine input)
    {
        var diagnostics = new List<Diagnostic>();

        // Check class modifiers: must be public, partial, static
        var missingModifiers = new List<string>();
        if (!input.Modifiers.HasFlag(ClassModifiers.Public))
            missingModifiers.Add("public");
        if (!input.Modifiers.HasFlag(ClassModifiers.Partial))
            missingModifiers.Add("partial");
        if (!input.Modifiers.HasFlag(ClassModifiers.Static))
            missingModifiers.Add("static");

        if (missingModifiers.Count > 0)
        {
            diagnostics.Add(Diagnostic.Create(
                DiagnosticDescriptors.InvalidClassDeclaration,
                input.Location,
                string.Join(", ", missingModifiers)));
        }

        // Check IDispatchableEvent implementation on event type
        if (!input.ImplementsIDispatchableEvent)
        {
            diagnostics.Add(Diagnostic.Create(
                DiagnosticDescriptors.MissingIDispatchableEventImplementation,
                input.Location,
                input.EventTypeName));
        }

        return diagnostics.ToImmutableArray();
    }
}
