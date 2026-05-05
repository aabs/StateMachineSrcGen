// Integration tests: End-to-end compilation
// Validates full pipeline with valid state machine definitions compiling in-memory
// **Validates: Requirements 11.4, 7.1**

using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using StateMachineSrcGen.Pipeline;
using Xunit;

namespace StateMachineSrcGen.Tests.Integration;

/// <summary>
/// End-to-end compilation integration tests that verify the full generator pipeline
/// produces valid, compilable assemblies from state machine definitions.
/// </summary>
public class EndToEndCompilationTests
{
    /// <summary>
    /// Tests that a valid state machine definition produces generated source through the full pipeline.
    /// </summary>
    [Fact]
    public void FullPipeline_ValidDefinition_ProducesGeneratedSource()
    {
        var source = CreateSimpleStateMachineSource("MyMachine", "MyApp", "Start", "start");

        var (generatedSources, diagnostics) = RunGenerator(source);

        // Generator should not produce SMSG errors
        var smsgErrors = diagnostics.Where(d => d.Id.StartsWith("SMSG") && d.Severity == DiagnosticSeverity.Error);
        Assert.Empty(smsgErrors);

        // Should produce at least one generated source
        Assert.NotEmpty(generatedSources);
        Assert.Contains(generatedSources, s => s.Contains("HandleAsync"));
    }

    /// <summary>
    /// Tests that generated code produces a valid .NET assembly when compiled with proper user code.
    /// </summary>
    [Fact]
    public void FullPipeline_GeneratedCode_ProducesValidAssembly()
    {
        var source = CreateSimpleStateMachineSource("OrderMachine", "TestApp", "Confirm", "confirm");

        var (generatedSources, _) = RunGenerator(source);
        Assert.NotEmpty(generatedSources);

        var generatedSource = generatedSources.First();

        // Compile the generated source with proper user code
        var userCode = CreateUserCode("OrderMachine", "TestApp", new[] { ("HandleConfirm", false, false) });
        var assembly = CompileAndLoad(generatedSource, userCode);

        Assert.NotNull(assembly);
        Assert.NotNull(assembly!.GetType("TestApp.OrderMachine"));
    }

    /// <summary>
    /// Tests that multiple state machines in the same compilation all generate correctly.
    /// </summary>
    [Fact]
    public void FullPipeline_MultipleStateMachines_AllGenerateSuccessfully()
    {
        var source = @"
using System;
using System.Threading.Tasks;
using StateMachineSrcGen;

namespace MultiApp;

public record EventA(string Id) : IDispatchableEvent<string>
{
    public string GetEventId() => Id;
}

public record EventB(string Code) : IDispatchableEvent<string>
{
    public string GetEventId() => Code;
}

[State(""Off"", IsInitial = true)]
[State(""On"")]
[Trigger(""Toggle"")]
public static partial class LightMachine : IStateMachine<string, EventA>, IStatePersistence<string>
{
    [Transition(""Off"", ""On"", ""Toggle"", EventId = ""toggle"")]
    public static string TurnOn(string state, EventA @event) => ""On"";

    public Task<TransitionResult> HandleAsync(EventA @event) => throw new NotImplementedException();
    public Task<string> LoadAsync() => throw new NotImplementedException();
    public Task SaveAsync(string state) => throw new NotImplementedException();
}

[State(""Locked"", IsInitial = true)]
[State(""Unlocked"")]
[Trigger(""Unlock"")]
public static partial class DoorMachine : IStateMachine<string, EventB>, IStatePersistence<string>
{
    [Transition(""Locked"", ""Unlocked"", ""Unlock"", EventId = ""unlock"")]
    public static string HandleUnlock(string state, EventB @event) => ""Unlocked"";

    public Task<TransitionResult> HandleAsync(EventB @event) => throw new NotImplementedException();
    public Task<string> LoadAsync() => throw new NotImplementedException();
    public Task SaveAsync(string state) => throw new NotImplementedException();
}
";

        var (generatedSources, diagnostics) = RunGenerator(source);

        var smsgErrors = diagnostics.Where(d => d.Id.StartsWith("SMSG") && d.Severity == DiagnosticSeverity.Error);
        Assert.Empty(smsgErrors);

        // Should produce generated sources for both machines
        Assert.True(generatedSources.Count >= 2, $"Expected at least 2 generated sources, got {generatedSources.Count}");
        Assert.Contains(generatedSources, s => s.Contains("LightMachine"));
        Assert.Contains(generatedSources, s => s.Contains("DoorMachine"));
    }

    /// <summary>
    /// Tests that a state machine with guards and side effects generates correctly.
    /// </summary>
    [Fact]
    public void FullPipeline_WithGuardsAndSideEffects_GeneratesSuccessfully()
    {
        var source = @"
using System;
using System.Threading.Tasks;
using StateMachineSrcGen;

namespace GuardApp;

public record GEvent(string Cmd) : IDispatchableEvent<string>
{
    public string GetEventId() => Cmd;
}

[State(""Draft"", IsInitial = true)]
[State(""Published"")]
[Trigger(""Publish"")]
public static partial class DocMachine : IStateMachine<string, GEvent>, IStatePersistence<string>
{
    [Guard(""Draft"", ""Published"", ""Publish"")]
    public static bool CanPublish(string state, GEvent @event) => state == ""Draft"";

    [Transition(""Draft"", ""Published"", ""Publish"", EventId = ""publish"")]
    public static string HandlePublish(string state, GEvent @event) => ""Published"";

    [SideEffect(""Draft"", ""Published"", ""Publish"")]
    public static void OnPublished(string state, GEvent @event) { }

    public Task<TransitionResult> HandleAsync(GEvent @event) => throw new NotImplementedException();
    public Task<string> LoadAsync() => throw new NotImplementedException();
    public Task SaveAsync(string state) => throw new NotImplementedException();
}
";

        var (generatedSources, diagnostics) = RunGenerator(source);

        var smsgErrors = diagnostics.Where(d => d.Id.StartsWith("SMSG") && d.Severity == DiagnosticSeverity.Error);
        Assert.Empty(smsgErrors);

        Assert.NotEmpty(generatedSources);
        var generated = generatedSources.First();

        // Verify guard and side effect are referenced in generated code
        Assert.Contains("CanPublish", generated);
        Assert.Contains("OnPublished", generated);
        Assert.Contains("HandlePublish", generated);
    }

    /// <summary>
    /// Tests that invalid definitions produce diagnostics but don't crash the compilation.
    /// </summary>
    [Fact]
    public void FullPipeline_InvalidDefinition_ProducesDiagnosticsWithoutCrash()
    {
        var source = @"
using System;
using System.Threading.Tasks;
using StateMachineSrcGen;

namespace BadApp;

public record BadEvent(string Y) : IDispatchableEvent<string>
{
    public string GetEventId() => Y;
}

[State(""A"", IsInitial = true)]
[Trigger(""Go"")]
public static partial class BadMachine : IStateMachine<string, BadEvent>, IStatePersistence<string>
{
    [Transition(""A"", ""NonExistent"", ""Go"", EventId = ""go"")]
    public static string Handle(string state, BadEvent @event) => state;

    public Task<TransitionResult> HandleAsync(BadEvent @event) => throw new NotImplementedException();
    public Task<string> LoadAsync() => throw new NotImplementedException();
    public Task SaveAsync(string state) => throw new NotImplementedException();
}
";

        // Should not throw
        var (generatedSources, diagnostics) = RunGenerator(source);

        // Should have at least one SMSG diagnostic about undefined state
        Assert.Contains(diagnostics, d => d.Id.StartsWith("SMSG"));
    }

    /// <summary>
    /// Tests that generated code can be executed at runtime (HandleAsync returns a result).
    /// </summary>
    [Fact]
    public async Task FullPipeline_GeneratedCode_CanBeExecutedAtRuntime()
    {
        var source = CreateSimpleStateMachineSource("RuntimeMachine", "RuntimeApp", "Go", "go");

        var (generatedSources, _) = RunGenerator(source);
        Assert.NotEmpty(generatedSources);

        var generatedSource = generatedSources.First();

        // Replace default persistence with one that returns "Idle" as initial state
        generatedSource = generatedSource.Replace(
            "private static StateMachineSrcGen.IStatePersistence<string> _persistence = new InMemoryPersistence();",
            "private static StateMachineSrcGen.IStatePersistence<string> _persistence = new TestPersistence();");

        var userCode = CreateUserCode("RuntimeMachine", "RuntimeApp", new[] { ("HandleGo", false, false) });

        var assembly = CompileAndLoad(generatedSource, userCode);
        Assert.NotNull(assembly);

        var machineType = assembly!.GetType("RuntimeApp.RuntimeMachine");
        Assert.NotNull(machineType);

        var handleMethod = machineType!.GetMethod("HandleAsync");
        Assert.NotNull(handleMethod);

        // Create event and invoke
        var eventType = assembly.GetType("RuntimeApp.TestEvent");
        Assert.NotNull(eventType);
        var evt = Activator.CreateInstance(eventType!);
        eventType!.GetProperty("EventId")!.SetValue(evt, "go");

        var task = (Task)handleMethod!.Invoke(null, new[] { evt })!;
        await task;
        var resultProp = task.GetType().GetProperty("Result");
        var result = (int)resultProp!.GetValue(task)!;

        // TransitionResult.Success == 0
        Assert.Equal(0, result);
    }

    // ─── Helpers ────────────────────────────────────────────────────────────────

    private static string CreateSimpleStateMachineSource(string className, string ns, string trigger, string eventId)
    {
        return $@"
using System;
using System.Threading.Tasks;
using StateMachineSrcGen;

namespace {ns};

public record TestEvent(string Id) : IDispatchableEvent<string>
{{
    public string GetEventId() => Id;
}}

[State(""Idle"", IsInitial = true)]
[State(""Active"")]
[Trigger(""{trigger}"")]
public static partial class {className} : IStateMachine<string, TestEvent>, IStatePersistence<string>
{{
    [Transition(""Idle"", ""Active"", ""{trigger}"", EventId = ""{eventId}"")]
    public static string Handle{trigger}(string state, TestEvent @event) => ""Active"";

    public Task<TransitionResult> HandleAsync(TestEvent @event) => throw new NotImplementedException();
    public Task<string> LoadAsync() => throw new NotImplementedException();
    public Task SaveAsync(string state) => throw new NotImplementedException();
}}
";
    }

    private static (ImmutableList<string> GeneratedSources, ImmutableArray<Diagnostic> Diagnostics) RunGenerator(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var references = GetMetadataReferences();

        var compilation = CSharpCompilation.Create(
            "IntegrationTestAssembly",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        var generator = new StateMachineGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var generatorDiagnostics);

        var runResult = driver.GetRunResult();
        var generatedSources = runResult.GeneratedTrees
            .Select(t => t.GetText().ToString())
            .ToImmutableList();

        return (generatedSources, generatorDiagnostics);
    }

    private static string CreateUserCode(string className, string ns,
        (string MethodName, bool IsGuard, bool IsSideEffect)[] handlers)
    {
        var handlerMethods = string.Join("\n", handlers.Select(h =>
        {
            if (h.IsGuard)
                return $"        public static bool {h.MethodName}(string state, TestEvent @event) => true;";
            if (h.IsSideEffect)
                return $"        public static void {h.MethodName}(string state, TestEvent @event) {{ }}";
            return $"        public static string {h.MethodName}(string state, TestEvent @event) => \"Active\";";
        }));

        return $@"
#nullable enable
using System;
using System.Threading.Tasks;

namespace StateMachineSrcGen
{{
    public interface IStatePersistence<TState>
    {{
        Task<TState> LoadAsync();
        Task SaveAsync(TState state);
    }}

    public interface IStateLock<TState>
    {{
        Task<bool> AcquireAsync();
        Task ReleaseAsync();
    }}

    public interface IDispatchableEvent<TEventId> where TEventId : IEquatable<TEventId>
    {{
        TEventId GetEventId();
    }}

    public enum TransitionResult
    {{
        Success,
        NotHandled,
        LockFailed
    }}
}}

namespace {ns}
{{
    public class TestEvent : StateMachineSrcGen.IDispatchableEvent<string>
    {{
        public string EventId {{ get; set; }} = """";
        public string GetEventId() => EventId;
    }}

    public class TestPersistence : StateMachineSrcGen.IStatePersistence<string>
    {{
        private string _state = ""Idle"";
        public Task<string> LoadAsync() => Task.FromResult(_state);
        public Task SaveAsync(string state) {{ _state = state; return Task.CompletedTask; }}
    }}

    public static partial class {className}
    {{
{handlerMethods}
    }}
}}
";
    }

    private static Assembly? CompileAndLoad(string generatedSource, string userCode)
    {
        var syntaxTrees = new[]
        {
            CSharpSyntaxTree.ParseText(generatedSource),
            CSharpSyntaxTree.ParseText(userCode)
        };

        var references = new System.Collections.Generic.List<MetadataReference>();
        var trustedAssemblies = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))?.Split(Path.PathSeparator) ?? Array.Empty<string>();

        foreach (var assembly in trustedAssemblies)
        {
            if (assembly.EndsWith("System.Runtime.dll", StringComparison.OrdinalIgnoreCase) ||
                assembly.EndsWith("System.Threading.Tasks.dll", StringComparison.OrdinalIgnoreCase) ||
                assembly.EndsWith("System.Private.CoreLib.dll", StringComparison.OrdinalIgnoreCase) ||
                assembly.EndsWith("netstandard.dll", StringComparison.OrdinalIgnoreCase) ||
                assembly.EndsWith("mscorlib.dll", StringComparison.OrdinalIgnoreCase))
            {
                references.Add(MetadataReference.CreateFromFile(assembly));
            }
        }

        var compilation = CSharpCompilation.Create(
            "RuntimeTestAssembly",
            syntaxTrees,
            references,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        using var ms = new MemoryStream();
        var emitResult = compilation.Emit(ms);

        if (!emitResult.Success) return null;

        ms.Seek(0, SeekOrigin.Begin);
        return Assembly.Load(ms.ToArray());
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
