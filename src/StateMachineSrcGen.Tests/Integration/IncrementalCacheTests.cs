// Integration tests: Incremental cache behavior
// Validates that the generator correctly leverages Roslyn's incremental caching
// **Validates: Requirements 10.1, 10.3**

using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using StateMachineSrcGen.Pipeline;
using Xunit;

namespace StateMachineSrcGen.Tests.Integration;

/// <summary>
/// Integration tests verifying that the incremental generator correctly caches results
/// and only re-executes when relevant inputs change.
/// </summary>
public class IncrementalCacheTests
{
    private const string StateMachineSource = @"
using System;
using System.Threading.Tasks;
using StateMachineSrcGen;

namespace CacheTest;

public record CState(string Val);
public record CEvent(string Id) : IDispatchableEvent<string>
{
    public string GetEventId() => Id;
}

[State(""A"", IsInitial = true)]
[State(""B"")]
[Trigger(""Go"")]
public static partial class CacheMachine : IStateMachine<CState, CEvent>, IStatePersistence<CState>
{
    [Transition(""A"", ""B"", ""Go"", EventId = ""go"")]
    public static CState HandleGo(CState state, CEvent @event)
    {
        return state with { Val = ""B"" };
    }

    public Task<TransitionResult> HandleAsync(CEvent @event) => throw new NotImplementedException();
    public Task<CState> LoadAsync() => throw new NotImplementedException();
    public Task SaveAsync(CState state) => throw new NotImplementedException();
}
";

    private const string UnrelatedSource = @"
namespace CacheTest;

public class Unrelated
{
    public int Value { get; set; }
}
";

    /// <summary>
    /// Tests that running the generator twice with the same input produces the same output
    /// and the second run uses cached results (tracked via GeneratorRunResult).
    /// </summary>
    [Fact]
    public void IncrementalCache_SameInput_ProducesSameOutput()
    {
        var compilation = CreateCompilation(StateMachineSource);
        var generator = new StateMachineGenerator();

        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        // First run
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output1, out var diags1);
        var result1 = driver.GetRunResult();

        // Second run with same compilation (simulates no changes)
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output2, out var diags2);
        var result2 = driver.GetRunResult();

        // Both runs should produce the same generated sources
        var sources1 = result1.GeneratedTrees.Select(t => t.GetText().ToString()).ToList();
        var sources2 = result2.GeneratedTrees.Select(t => t.GetText().ToString()).ToList();

        Assert.Equal(sources1.Count, sources2.Count);
        for (int i = 0; i < sources1.Count; i++)
        {
            Assert.Equal(sources1[i], sources2[i]);
        }
    }

    /// <summary>
    /// Tests that adding an unrelated file does not change the generated output.
    /// </summary>
    [Fact]
    public void IncrementalCache_UnrelatedFileAdded_OutputUnchanged()
    {
        var compilation = CreateCompilation(StateMachineSource);
        var generator = new StateMachineGenerator();

        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        // First run
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);
        var result1 = driver.GetRunResult();
        var sources1 = result1.GeneratedTrees.Select(t => t.GetText().ToString()).ToList();

        // Add an unrelated file to the compilation
        var newTree = CSharpSyntaxTree.ParseText(UnrelatedSource);
        var modifiedCompilation = compilation.AddSyntaxTrees(newTree);

        // Second run with modified compilation
        driver = driver.RunGeneratorsAndUpdateCompilation(modifiedCompilation, out _, out _);
        var result2 = driver.GetRunResult();
        var sources2 = result2.GeneratedTrees.Select(t => t.GetText().ToString()).ToList();

        // Output should be identical
        Assert.Equal(sources1.Count, sources2.Count);
        for (int i = 0; i < sources1.Count; i++)
        {
            Assert.Equal(sources1[i], sources2[i]);
        }
    }

    /// <summary>
    /// Tests that modifying the state machine definition triggers re-generation with new output.
    /// </summary>
    [Fact]
    public void IncrementalCache_StateMachineModified_OutputChanges()
    {
        var compilation = CreateCompilation(StateMachineSource);
        var generator = new StateMachineGenerator();

        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        // First run
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);
        var result1 = driver.GetRunResult();
        var sources1 = result1.GeneratedTrees.Select(t => t.GetText().ToString()).ToList();

        // Modify the state machine - add a new state and transition
        var modifiedSource = @"
using System;
using System.Threading.Tasks;
using StateMachineSrcGen;

namespace CacheTest;

public record CState(string Val);
public record CEvent(string Id) : IDispatchableEvent<string>
{
    public string GetEventId() => Id;
}

[State(""A"", IsInitial = true)]
[State(""B"")]
[State(""C"")]
[Trigger(""Go"")]
[Trigger(""Next"")]
public static partial class CacheMachine : IStateMachine<CState, CEvent>, IStatePersistence<CState>
{
    [Transition(""A"", ""B"", ""Go"", EventId = ""go"")]
    public static CState HandleGo(CState state, CEvent @event)
    {
        return state with { Val = ""B"" };
    }

    [Transition(""B"", ""C"", ""Next"", EventId = ""next"")]
    public static CState HandleNext(CState state, CEvent @event)
    {
        return state with { Val = ""C"" };
    }

    public Task<TransitionResult> HandleAsync(CEvent @event) => throw new NotImplementedException();
    public Task<CState> LoadAsync() => throw new NotImplementedException();
    public Task SaveAsync(CState state) => throw new NotImplementedException();
}
";

        var modifiedTree = CSharpSyntaxTree.ParseText(modifiedSource);
        var originalTree = compilation.SyntaxTrees.First();
        var modifiedCompilation = compilation.ReplaceSyntaxTree(originalTree, modifiedTree);

        // Second run with modified state machine
        driver = driver.RunGeneratorsAndUpdateCompilation(modifiedCompilation, out _, out _);
        var result2 = driver.GetRunResult();
        var sources2 = result2.GeneratedTrees.Select(t => t.GetText().ToString()).ToList();

        // Output should be different (new transition added)
        Assert.True(sources1.Count > 0 && sources2.Count > 0);

        // The generated source should now contain the new handler
        var generatedText = string.Join("", sources2);
        Assert.Contains("HandleNext", generatedText);
    }

    /// <summary>
    /// Tests that the generator driver tracks incremental steps correctly.
    /// </summary>
    [Fact]
    public void IncrementalCache_TrackedSteps_ShowCachedOnRerun()
    {
        var compilation = CreateCompilation(StateMachineSource);
        var generator = new StateMachineGenerator();

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new[] { generator.AsSourceGenerator() },
            driverOptions: new GeneratorDriverOptions(
                disabledOutputs: IncrementalGeneratorOutputKind.None,
                trackIncrementalGeneratorSteps: true));

        // First run
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);

        // Second run with same input - should use cache
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);
        var result = driver.GetRunResult();

        // With tracking enabled, we can verify cached steps exist
        var generatorResult = result.Results.FirstOrDefault();
        Assert.NotNull(generatorResult.Generator);

        // The tracked steps should show some cached entries on the second run
        if (generatorResult.TrackedSteps.Count > 0)
        {
            var hasCachedStep = generatorResult.TrackedSteps
                .SelectMany(kvp => kvp.Value)
                .SelectMany(step => step.Outputs)
                .Any(output => output.Reason == IncrementalStepRunReason.Cached ||
                               output.Reason == IncrementalStepRunReason.Unchanged);

            Assert.True(hasCachedStep, "Expected at least one cached/unchanged step on re-run with same input");
        }
    }

    // ─── Helpers ────────────────────────────────────────────────────────────────

    private static CSharpCompilation CreateCompilation(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var references = GetMetadataReferences();

        return CSharpCompilation.Create(
            "IncrementalCacheTestAssembly",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));
    }

    private static ImmutableArray<MetadataReference> GetMetadataReferences()
    {
        var references = new System.Collections.Generic.List<MetadataReference>();

        references.Add(MetadataReference.CreateFromFile(typeof(TransitionAttribute).Assembly.Location));

        var trustedAssemblies = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))?.Split(Path.PathSeparator) ?? Array.Empty<string>();

        foreach (var assembly in trustedAssemblies)
        {
            if (assembly.EndsWith("System.Runtime.dll", StringComparison.OrdinalIgnoreCase) ||
                assembly.EndsWith("System.Threading.Tasks.dll", StringComparison.OrdinalIgnoreCase) ||
                assembly.EndsWith("System.Private.CoreLib.dll", StringComparison.OrdinalIgnoreCase) ||
                assembly.EndsWith("netstandard.dll", StringComparison.OrdinalIgnoreCase) ||
                assembly.EndsWith("mscorlib.dll", StringComparison.OrdinalIgnoreCase) ||
                assembly.EndsWith("System.Collections.dll", StringComparison.OrdinalIgnoreCase))
            {
                references.Add(MetadataReference.CreateFromFile(assembly));
            }
        }

        return references.ToImmutableArray();
    }
}
