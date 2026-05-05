using System;
using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using StateMachineSrcGen.Diagnostics;

namespace StateMachineSrcGen.Generation;

/// <summary>
/// Orchestrates all emitters to produce complete source text from a validated state machine model.
/// Wraps in try-catch, emits SMSG015 on unexpected errors.
/// </summary>
public static class GenerationPipeline
{
    /// <summary>
    /// Generates complete C# source text from a validated state machine model.
    /// </summary>
    /// <param name="input">The validated state machine model.</param>
    /// <returns>A tuple of the generated source (or null on error) and any diagnostics.</returns>
    public static (string? GeneratedSource, ImmutableArray<Diagnostic> Diagnostics) Generate(
        ValidatedStateMachine input)
    {
        try
        {
            var bodyBuilder = new StringBuilder();

            // Emit HandleAsync method
            bodyBuilder.Append(HandleMethodEmitter.Emit(input));
            bodyBuilder.AppendLine();

            // Emit default persistence class
            bodyBuilder.Append(PersistenceEmitter.Emit(input));
            bodyBuilder.AppendLine();

            // Emit default lock class
            bodyBuilder.Append(LockEmitter.Emit(input));

            // Format with namespace, class declaration, etc.
            var source = SourceFormatter.Format(input, bodyBuilder.ToString());

            return (source, ImmutableArray<Diagnostic>.Empty);
        }
        catch (Exception ex)
        {
            var diagnostic = Diagnostic.Create(
                DiagnosticDescriptors.InternalGeneratorError,
                Location.None,
                ex.Message);

            return (null, ImmutableArray.Create(diagnostic));
        }
    }
}
