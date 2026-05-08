using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using StateMachineSrcGen.Diagnostics;

namespace StateMachineSrcGen.Parsing;

/// <summary>
/// Assembles parsed components into a complete ParsedStateMachine value type.
/// Wraps all assembly logic in try-catch to emit SMSG015 on unexpected errors.
/// </summary>
internal static class ParsedModelFactory
{
    /// <summary>
    /// Assembles a ParsedStateMachine from the declaration and handler parsing results.
    /// </summary>
    public static (ParsedStateMachine? Result, ImmutableArray<Diagnostic> Diagnostics) Assemble(
        DeclarationParser.DeclarationResult declaration,
        ImmutableArray<ParsedHandler> handlers,
        ImmutableArray<Diagnostic> declarationDiagnostics,
        ImmutableArray<Diagnostic> handlerDiagnostics)
    {
        try
        {
            // Combine all diagnostics
            var allDiagnostics = declarationDiagnostics.AddRange(handlerDiagnostics);

            // Always produce a result so downstream can inspect the parsed model
            // even if there are diagnostics (the analysis stage decides whether to proceed)
            var result = new ParsedStateMachine
            {
                Namespace = declaration.Namespace,
                ClassName = declaration.ClassName,
                StateTypeName = declaration.StateTypeName,
                EventTypeName = declaration.EventTypeName,
                States = new EquatableArray<ParsedState>(declaration.States),
                Triggers = new EquatableArray<ParsedTrigger>(declaration.Triggers),
                Handlers = new EquatableArray<ParsedHandler>(handlers),
                Modifiers = declaration.Modifiers,
                ImplementsIDispatchableEvent = declaration.ImplementsIDispatchableEvent,
                EventIdTypeName = declaration.EventIdTypeName,
                ImplementsIStateMachineState = declaration.ImplementsIStateMachineState,
                StateIdTypeName = declaration.StateIdTypeName,
                Location = declaration.Location
            };

            return (result, allDiagnostics);
        }
        catch (Exception ex)
        {
            var diagnostic = Diagnostic.Create(
                DiagnosticDescriptors.InternalGeneratorError,
                declaration.Location,
                ex.Message);

            return (null, ImmutableArray.Create(diagnostic));
        }
    }
}
