using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using StateMachineSrcGen.Analysis;
using StateMachineSrcGen.Generation;
using StateMachineSrcGen.Parsing;

namespace StateMachineSrcGen.Pipeline;

/// <summary>
/// Roslyn Incremental Source Generator that transforms attribute-decorated state machine
/// class declarations into fully-functional implementations at compile time.
/// Wires Parsing → Analysis → Generation stages with incremental caching.
/// </summary>
[Generator]
public class StateMachineGenerator : IIncrementalGenerator
{
    /// <summary>
    /// Initializes the incremental generator pipeline.
    /// </summary>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Stage 1: Filter syntax nodes — find class declarations with state machine attributes
        var classDeclarations = context.SyntaxProvider.CreateSyntaxProvider(
            predicate: static (node, _) => node is ClassDeclarationSyntax cds && SyntaxExtractor.HasStateMachineAttributes(cds),
            transform: static (ctx, _) => (ClassDeclaration: (ClassDeclarationSyntax)ctx.Node, SemanticModel: ctx.SemanticModel));

        // Stage 2: Parse → Analyze → Generate (pure function pipeline)
        var pipelineResults = classDeclarations.Select(static (input, _) =>
        {
            // Parsing stage
            var (parsed, parseDiagnostics) = ParsingPipeline.Parse(input.ClassDeclaration, input.SemanticModel);

            if (parsed == null)
            {
                return new PipelineResult(null, null, parseDiagnostics);
            }

            // Analysis stage
            var (validated, analysisDiagnostics) = AnalysisPipeline.Analyze(parsed.Value);

            var allDiagnostics = parseDiagnostics.AddRange(analysisDiagnostics);

            if (validated == null)
            {
                return new PipelineResult(null, null, allDiagnostics);
            }

            // Generation stage
            var (generatedSource, genDiagnostics) = GenerationPipeline.Generate(validated.Value);

            allDiagnostics = allDiagnostics.AddRange(genDiagnostics);

            var hint = $"{validated.Value.ClassName}.g.cs";
            return new PipelineResult(hint, generatedSource, allDiagnostics);
        });

        // Stage 3: Register source output and diagnostic reporting
        context.RegisterSourceOutput(pipelineResults, static (spc, result) =>
        {
            // Report all diagnostics
            foreach (var diagnostic in result.Diagnostics)
            {
                spc.ReportDiagnostic(diagnostic);
            }

            // Add generated source if available
            if (result.HintName != null && result.GeneratedSource != null)
            {
                spc.AddSource(result.HintName, result.GeneratedSource);
            }
        });
    }

    /// <summary>
    /// Holds the result of the full pipeline execution for a single class declaration.
    /// Uses value equality for incremental caching.
    /// </summary>
    private sealed record PipelineResult(
        string? HintName,
        string? GeneratedSource,
        ImmutableArray<Diagnostic> Diagnostics);
}
