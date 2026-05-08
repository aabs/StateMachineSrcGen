// Feature: generic-state-machine-api, Property 18: Generated code compiles without errors
// Feature: state-machine-source-generator, Property 26: Full pipeline round-trip compilation
// **Validates: Requirements 7.1, 7.2, 7.3, 7.4, 7.5, 7.6**

using System;
using System.Collections.Immutable;
using System.Linq;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using StateMachineSrcGen;
using StateMachineSrcGen.Generation;

namespace StateMachineSrcGen.Tests.Generation;

/// <summary>
/// Property 18: Generated code compiles without errors
/// For any valid ValidatedStateMachine, generated C# compiles without errors
/// under nullable reference types context.
///
/// Property 26: Full pipeline round-trip compilation
/// Running full pipeline and compiling output in-memory produces a valid .NET assembly.
/// </summary>
public class CompilationProperties
{
    [Property]
    public bool GeneratedCode_CompilesWithoutErrors_ForSimpleMachine(
        NonEmptyString classRaw, NonEmptyString nsRaw)
    {
        var className = ToIdentifier(classRaw);
        var ns = ToIdentifier(nsRaw);

        var input = GenerationTestHelper.CreateValidStateMachine(className: className, ns: ns);
        var (source, diagnostics) = GenerationPipeline.Generate(input);

        if (source == null)
            return false;

        var userCode = GenerationTestHelper.BuildEnumSupportCode(input);
        var compilationDiags = GenerationTestHelper.GetCompilationDiagnostics(source, userCode);

        return !compilationDiags.Any(d => d.Severity == DiagnosticSeverity.Error);
    }

    [Property]
    public bool GeneratedCode_CompilesWithoutErrors_ForComplexMachine(
        NonEmptyString classRaw, NonEmptyString nsRaw)
    {
        var className = ToIdentifier(classRaw);
        var ns = ToIdentifier(nsRaw);

        var input = GenerationTestHelper.CreateComplexStateMachine(className: className, ns: ns);
        var (source, diagnostics) = GenerationPipeline.Generate(input);

        if (source == null)
            return false;

        var userCode = GenerationTestHelper.BuildEnumSupportCode(input);
        var compilationDiags = GenerationTestHelper.GetCompilationDiagnostics(source, userCode);

        return !compilationDiags.Any(d => d.Severity == DiagnosticSeverity.Error);
    }

    [Property]
    public bool FullPipeline_RoundTrip_ProducesValidAssembly(
        NonEmptyString classRaw, NonEmptyString nsRaw)
    {
        var className = ToIdentifier(classRaw);
        var ns = ToIdentifier(nsRaw);

        var input = GenerationTestHelper.CreateValidStateMachine(className: className, ns: ns);
        var (source, diagnostics) = GenerationPipeline.Generate(input);

        if (source == null)
            return false;

        var userCode = GenerationTestHelper.BuildEnumSupportCode(input);
        var compilation = GenerationTestHelper.CompileGeneratedSource(source, userCode);
        using var ms = new System.IO.MemoryStream();
        var emitResult = compilation.Emit(ms);

        return emitResult.Success;
    }

    [Property]
    public bool GeneratedCode_CompilesWithoutWarnings_UnderNullableContext(
        NonEmptyString classRaw, NonEmptyString nsRaw)
    {
        var className = ToIdentifier(classRaw);
        var ns = ToIdentifier(nsRaw);

        var input = GenerationTestHelper.CreateValidStateMachine(className: className, ns: ns);
        var (source, diagnostics) = GenerationPipeline.Generate(input);

        if (source == null)
            return false;

        var userCode = GenerationTestHelper.BuildEnumSupportCode(input);
        var compilationDiags = GenerationTestHelper.GetCompilationDiagnostics(source, userCode);

        // No errors or warnings (excluding allowed ones)
        return compilationDiags.Length == 0;
    }

    // ─── Helpers ────────────────────────────────────────────────────────────────

    private static string ToIdentifier(NonEmptyString raw)
    {
        var filtered = new string(raw.Get.Where(char.IsLetterOrDigit).ToArray());
        if (string.IsNullOrEmpty(filtered) || !char.IsLetter(filtered[0]))
            return "X" + filtered;
        return filtered;
    }
}
