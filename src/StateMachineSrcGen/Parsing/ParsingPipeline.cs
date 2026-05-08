using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using StateMachineSrcGen.Diagnostics;

namespace StateMachineSrcGen.Parsing;

/// <summary>
/// Entry point for the parsing stage. Accepts a class declaration syntax node
/// and its semantic model, and returns either a parsed state machine model or diagnostics.
/// </summary>
public static class ParsingPipeline
{
    /// <summary>
    /// Parses a class declaration decorated with state machine attributes into a
    /// <see cref="ParsedStateMachine"/> model, or returns diagnostics if the declaration is invalid.
    /// </summary>
    /// <param name="classDeclaration">The class declaration syntax node to parse.</param>
    /// <param name="semanticModel">The semantic model for the compilation containing the class.</param>
    /// <returns>A tuple of the parsed result (or null on failure) and any diagnostics emitted.</returns>
    public static (ParsedStateMachine? Result, ImmutableArray<Diagnostic> Diagnostics) Parse(
        ClassDeclarationSyntax classDeclaration,
        SemanticModel semanticModel)
    {
        try
        {
            // Step 1: Check if this class has any state machine attributes
            if (!SyntaxExtractor.HasStateMachineAttributes(classDeclaration))
            {
                return (null, ImmutableArray<Diagnostic>.Empty);
            }

            // Step 2: Parse class declaration (modifiers, interfaces, states, triggers)
            var (declaration, declarationDiagnostics) = DeclarationParser.Parse(classDeclaration, semanticModel);

            // Step 3: Parse handler methods with enum type symbols for int-to-enum resolution
            var (handlerResult, handlerDiagnostics) = HandlerParser.Parse(
                classDeclaration,
                semanticModel,
                declaration.StateTypeName,
                declaration.EventTypeName,
                declaration.StateIdEnumSymbol,
                declaration.EventIdEnumSymbol);

            // Step 4: Assemble into ParsedStateMachine
            return ParsedModelFactory.Assemble(
                declaration,
                handlerResult.Handlers,
                handlerResult.EntryCallbacks,
                handlerResult.CleanupHandler,
                declarationDiagnostics,
                handlerDiagnostics);
        }
        catch (Exception ex)
        {
            // SMSG015: Internal generator error — never let exceptions escape
            var location = classDeclaration.Identifier.GetLocation();
            var diagnostic = Diagnostic.Create(
                DiagnosticDescriptors.InternalGeneratorError,
                location,
                ex.Message);

            return (null, ImmutableArray.Create(diagnostic));
        }
    }
}
