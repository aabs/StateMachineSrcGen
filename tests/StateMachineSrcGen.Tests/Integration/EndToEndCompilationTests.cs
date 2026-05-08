// Feature: generic-state-machine-api
// Property 18: Generated code compiles without errors (end-to-end)
// Integration tests verifying the full source generator pipeline produces valid C#
// **Validates: Requirements 7.1, 7.2, 7.3, 7.4, 7.5, 7.6**

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using StateMachineSrcGen.Pipeline;
using Xunit;

namespace StateMachineSrcGen.Tests.Integration;

/// <summary>
/// End-to-end compilation integration tests that run the full Roslyn source generator pipeline
/// with complete C# source code using the new generic enum-based API, then verify the generated
/// output compiles without errors.
/// </summary>
public class EndToEndCompilationTests
{
    // ─── Test: Simple state machine (2 states, 1 transition) ─────────────────────

    /// <summary>
    /// A simple state machine with 2 states and 1 transition compiles without errors
    /// through the full generator pipeline.
    /// </summary>
    [Fact]
    public void SimpleStateMachine_TwoStates_OneTransition_CompilesWithoutErrors()
    {
        var source = @"
using System;
using System.Threading.Tasks;
using StateMachineSrcGen;

namespace TestApp;

public enum OrderStateId
{
    Pending = 0,
    Confirmed = 1
}

public enum OrderEventId
{
    Confirm = 0
}

public class OrderState : IStateMachineState<OrderStateId>
{
    public OrderStateId GetStateId() => OrderStateId.Pending;
}

public class OrderEvent : IDispatchableEvent<OrderEventId>
{
    public OrderEventId GetEventId() => OrderEventId.Confirm;
}

[InitialState((int)OrderStateId.Pending)]
public static partial class OrderMachine
{
    [Transition((int)OrderStateId.Pending, (int)OrderStateId.Confirmed, (int)OrderEventId.Confirm)]
    public static OrderState HandleConfirm(OrderState state, OrderEvent @event) => state;
}
";

        var (generatedSources, diagnostics) = RunGenerator(source);

        AssertNoSmsgErrors(diagnostics);
        Assert.NotEmpty(generatedSources);

        var generatedSource = generatedSources.First();
        AssertGeneratedCodeCompiles(generatedSource, "TestApp", "OrderMachine",
            "OrderStateId", "OrderEventId", "OrderState", "OrderEvent",
            new[] { ("Pending", 0), ("Confirmed", 1) },
            new[] { ("Confirm", 0) },
            new[] { ("HandleConfirm", false, false) },
            Array.Empty<(string, bool)>(),
            null);
    }

    // ─── Test: State machine with guards ─────────────────────────────────────────

    /// <summary>
    /// A state machine with a guard method compiles without errors through the full pipeline.
    /// </summary>
    [Fact]
    public void StateMachine_WithGuard_CompilesWithoutErrors()
    {
        var source = @"
using System;
using System.Threading.Tasks;
using StateMachineSrcGen;

namespace TestApp;

public enum DocStateId
{
    Draft = 0,
    Published = 1
}

public enum DocEventId
{
    Publish = 0
}

public class DocState : IStateMachineState<DocStateId>
{
    public DocStateId GetStateId() => DocStateId.Draft;
}

public class DocEvent : IDispatchableEvent<DocEventId>
{
    public DocEventId GetEventId() => DocEventId.Publish;
}

[InitialState((int)DocStateId.Draft)]
public static partial class DocMachine
{
    [Guard((int)DocStateId.Draft, (int)DocStateId.Published, (int)DocEventId.Publish)]
    public static bool CanPublish(DocState state, DocEvent @event) => true;

    [Transition((int)DocStateId.Draft, (int)DocStateId.Published, (int)DocEventId.Publish)]
    public static DocState HandlePublish(DocState state, DocEvent @event) => state;
}
";

        var (generatedSources, diagnostics) = RunGenerator(source);

        AssertNoSmsgErrors(diagnostics);
        Assert.NotEmpty(generatedSources);

        var generatedSource = generatedSources.First();
        AssertGeneratedCodeCompiles(generatedSource, "TestApp", "DocMachine",
            "DocStateId", "DocEventId", "DocState", "DocEvent",
            new[] { ("Draft", 0), ("Published", 1) },
            new[] { ("Publish", 0) },
            new[] { ("HandlePublish", false, false), ("CanPublish", true, false) },
            Array.Empty<(string, bool)>(),
            null);

        // Verify guard is referenced in generated code
        Assert.Contains("CanPublish", generatedSource);
    }

    // ─── Test: State machine with side-effects ───────────────────────────────────

    /// <summary>
    /// A state machine with a side-effect method compiles without errors through the full pipeline.
    /// </summary>
    [Fact]
    public void StateMachine_WithSideEffect_CompilesWithoutErrors()
    {
        var source = @"
using System;
using System.Threading.Tasks;
using StateMachineSrcGen;

namespace TestApp;

public enum TaskStateId
{
    Open = 0,
    InProgress = 1
}

public enum TaskEventId
{
    Start = 0
}

public class TaskState : IStateMachineState<TaskStateId>
{
    public TaskStateId GetStateId() => TaskStateId.Open;
}

public class TaskEvent : IDispatchableEvent<TaskEventId>
{
    public TaskEventId GetEventId() => TaskEventId.Start;
}

[InitialState((int)TaskStateId.Open)]
public static partial class TaskMachine
{
    [Transition((int)TaskStateId.Open, (int)TaskStateId.InProgress, (int)TaskEventId.Start)]
    public static TaskState HandleStart(TaskState state, TaskEvent @event) => state;

    [SideEffect((int)TaskStateId.Open, (int)TaskStateId.InProgress, (int)TaskEventId.Start)]
    public static void OnStarted(TaskState state, TaskEvent @event) { }
}
";

        var (generatedSources, diagnostics) = RunGenerator(source);

        AssertNoSmsgErrors(diagnostics);
        Assert.NotEmpty(generatedSources);

        var generatedSource = generatedSources.First();
        AssertGeneratedCodeCompiles(generatedSource, "TestApp", "TaskMachine",
            "TaskStateId", "TaskEventId", "TaskState", "TaskEvent",
            new[] { ("Open", 0), ("InProgress", 1) },
            new[] { ("Start", 0) },
            new[] { ("HandleStart", false, false), ("OnStarted", false, true) },
            Array.Empty<(string, bool)>(),
            null);

        // Verify side-effect is referenced in generated code
        Assert.Contains("OnStarted", generatedSource);
    }

    // ─── Test: State machine with entry callbacks ────────────────────────────────

    /// <summary>
    /// A state machine with targeted and catch-all entry callbacks compiles without errors.
    /// </summary>
    [Fact]
    public void StateMachine_WithEntryCallbacks_CompilesWithoutErrors()
    {
        var source = @"
using System;
using System.Threading.Tasks;
using StateMachineSrcGen;

namespace TestApp;

public enum FlowStateId
{
    Idle = 0,
    Active = 1,
    Done = 2
}

public enum FlowEventId
{
    Activate = 0,
    Complete = 1
}

public class FlowState : IStateMachineState<FlowStateId>
{
    public FlowStateId GetStateId() => FlowStateId.Idle;
}

public class FlowEvent : IDispatchableEvent<FlowEventId>
{
    public FlowEventId GetEventId() => FlowEventId.Activate;
}

[InitialState((int)FlowStateId.Idle)]
public static partial class FlowMachine
{
    [Transition((int)FlowStateId.Idle, (int)FlowStateId.Active, (int)FlowEventId.Activate)]
    public static FlowState HandleActivate(FlowState state, FlowEvent @event) => state;

    [Transition((int)FlowStateId.Active, (int)FlowStateId.Done, (int)FlowEventId.Complete)]
    public static FlowState HandleComplete(FlowState state, FlowEvent @event) => state;

    [OnEnter((int)FlowStateId.Active)]
    public static FlowState OnEnterActive(FlowState state, FlowEvent @event) => state;

    [OnEnter]
    public static void OnEnterAny(FlowState state, FlowEvent @event) { }
}
";

        var (generatedSources, diagnostics) = RunGenerator(source);

        AssertNoSmsgErrors(diagnostics);
        Assert.NotEmpty(generatedSources);

        var generatedSource = generatedSources.First();
        AssertGeneratedCodeCompiles(generatedSource, "TestApp", "FlowMachine",
            "FlowStateId", "FlowEventId", "FlowState", "FlowEvent",
            new[] { ("Idle", 0), ("Active", 1), ("Done", 2) },
            new[] { ("Activate", 0), ("Complete", 1) },
            new[] { ("HandleActivate", false, false), ("HandleComplete", false, false) },
            new[] { ("OnEnterActive", true), ("OnEnterAny", false) },
            null);

        // Verify entry callbacks are referenced in generated code
        Assert.Contains("OnEnterActive", generatedSource);
        Assert.Contains("OnEnterAny", generatedSource);
    }

    // ─── Test: State machine with terminal states and cleanup handler ────────────

    /// <summary>
    /// A state machine with terminal states and a cleanup handler compiles without errors.
    /// </summary>
    [Fact]
    public void StateMachine_WithTerminalStateAndCleanup_CompilesWithoutErrors()
    {
        var source = @"
using System;
using System.Threading.Tasks;
using StateMachineSrcGen;

namespace TestApp;

public enum JobStateId
{
    Queued = 0,
    Running = 1,
    Completed = 2
}

public enum JobEventId
{
    Run = 0,
    Finish = 1
}

public class JobState : IStateMachineState<JobStateId>
{
    public JobStateId GetStateId() => JobStateId.Queued;
}

public class JobEvent : IDispatchableEvent<JobEventId>
{
    public JobEventId GetEventId() => JobEventId.Run;
}

[InitialState((int)JobStateId.Queued)]
[TerminalState((int)JobStateId.Completed)]
public static partial class JobMachine
{
    [Transition((int)JobStateId.Queued, (int)JobStateId.Running, (int)JobEventId.Run)]
    public static JobState HandleRun(JobState state, JobEvent @event) => state;

    [Transition((int)JobStateId.Running, (int)JobStateId.Completed, (int)JobEventId.Finish)]
    public static JobState HandleFinish(JobState state, JobEvent @event) => state;

    [OnTerminal]
    public static Task CleanupOnComplete(JobState state) => Task.CompletedTask;
}
";

        var (generatedSources, diagnostics) = RunGenerator(source);

        AssertNoSmsgErrors(diagnostics);
        Assert.NotEmpty(generatedSources);

        var generatedSource = generatedSources.First();
        AssertGeneratedCodeCompiles(generatedSource, "TestApp", "JobMachine",
            "JobStateId", "JobEventId", "JobState", "JobEvent",
            new[] { ("Queued", 0), ("Running", 1), ("Completed", 2) },
            new[] { ("Run", 0), ("Finish", 1) },
            new[] { ("HandleRun", false, false), ("HandleFinish", false, false) },
            Array.Empty<(string, bool)>(),
            "CleanupOnComplete");

        // Verify cleanup handler is referenced in generated code
        Assert.Contains("CleanupOnComplete", generatedSource);
    }

    // ─── Test: State machine with all features combined ──────────────────────────

    /// <summary>
    /// A state machine combining guards, side-effects, entry callbacks, terminal states,
    /// and cleanup handler compiles without errors through the full pipeline.
    /// </summary>
    [Fact]
    public void StateMachine_AllFeaturesCombined_CompilesWithoutErrors()
    {
        var source = @"
using System;
using System.Threading.Tasks;
using StateMachineSrcGen;

namespace TestApp;

public enum OrderStateId
{
    Pending = 0,
    Confirmed = 1,
    Shipped = 2,
    Cancelled = 3
}

public enum OrderEventId
{
    Confirm = 0,
    Ship = 1,
    Cancel = 2
}

public class OrderState : IStateMachineState<OrderStateId>
{
    public OrderStateId GetStateId() => OrderStateId.Pending;
}

public class OrderEvent : IDispatchableEvent<OrderEventId>
{
    public OrderEventId GetEventId() => OrderEventId.Confirm;
}

[InitialState((int)OrderStateId.Pending)]
[TerminalState((int)OrderStateId.Cancelled)]
public static partial class OrderMachine
{
    [Guard((int)OrderStateId.Pending, (int)OrderStateId.Confirmed, (int)OrderEventId.Confirm)]
    public static bool CanConfirm(OrderState state, OrderEvent @event) => true;

    [Transition((int)OrderStateId.Pending, (int)OrderStateId.Confirmed, (int)OrderEventId.Confirm)]
    public static OrderState HandleConfirm(OrderState state, OrderEvent @event) => state;

    [SideEffect((int)OrderStateId.Pending, (int)OrderStateId.Confirmed, (int)OrderEventId.Confirm)]
    public static void AfterConfirm(OrderState state, OrderEvent @event) { }

    [Transition((int)OrderStateId.Confirmed, (int)OrderStateId.Shipped, (int)OrderEventId.Ship)]
    public static OrderState HandleShip(OrderState state, OrderEvent @event) => state;

    [Transition((int)OrderStateId.Pending, (int)OrderStateId.Cancelled, (int)OrderEventId.Cancel)]
    public static OrderState HandleCancel(OrderState state, OrderEvent @event) => state;

    [OnEnter((int)OrderStateId.Confirmed)]
    public static OrderState OnEnterConfirmed(OrderState state, OrderEvent @event) => state;

    [OnEnter]
    public static void OnEnterAny(OrderState state, OrderEvent @event) { }

    [OnTerminal]
    public static Task OnOrderCancelled(OrderState state) => Task.CompletedTask;
}
";

        var (generatedSources, diagnostics) = RunGenerator(source);

        AssertNoSmsgErrors(diagnostics);
        Assert.NotEmpty(generatedSources);

        var generatedSource = generatedSources.First();
        AssertGeneratedCodeCompiles(generatedSource, "TestApp", "OrderMachine",
            "OrderStateId", "OrderEventId", "OrderState", "OrderEvent",
            new[] { ("Pending", 0), ("Confirmed", 1), ("Shipped", 2), ("Cancelled", 3) },
            new[] { ("Confirm", 0), ("Ship", 1), ("Cancel", 2) },
            new[]
            {
                ("HandleConfirm", false, false),
                ("CanConfirm", true, false),
                ("AfterConfirm", false, true),
                ("HandleShip", false, false),
                ("HandleCancel", false, false)
            },
            new[] { ("OnEnterConfirmed", true), ("OnEnterAny", false) },
            "OnOrderCancelled");

        // Verify all features are referenced in generated code
        Assert.Contains("CanConfirm", generatedSource);
        Assert.Contains("HandleConfirm", generatedSource);
        Assert.Contains("AfterConfirm", generatedSource);
        Assert.Contains("HandleShip", generatedSource);
        Assert.Contains("HandleCancel", generatedSource);
        Assert.Contains("OnEnterConfirmed", generatedSource);
        Assert.Contains("OnEnterAny", generatedSource);
        Assert.Contains("OnOrderCancelled", generatedSource);
    }

    // ─── Test: Generated code uses enum dispatch ─────────────────────────────────

    /// <summary>
    /// Verifies the generated code uses enum-based dispatch (switch on enum values)
    /// rather than string comparisons.
    /// </summary>
    [Fact]
    public void GeneratedCode_UsesEnumBasedDispatch()
    {
        var source = @"
using System;
using System.Threading.Tasks;
using StateMachineSrcGen;

namespace TestApp;

public enum LightStateId
{
    Off = 0,
    On = 1
}

public enum LightEventId
{
    Toggle = 0
}

public class LightState : IStateMachineState<LightStateId>
{
    public LightStateId GetStateId() => LightStateId.Off;
}

public class LightEvent : IDispatchableEvent<LightEventId>
{
    public LightEventId GetEventId() => LightEventId.Toggle;
}

[InitialState((int)LightStateId.Off)]
public static partial class LightMachine
{
    [Transition((int)LightStateId.Off, (int)LightStateId.On, (int)LightEventId.Toggle)]
    public static LightState HandleToggle(LightState state, LightEvent @event) => state;
}
";

        var (generatedSources, diagnostics) = RunGenerator(source);

        AssertNoSmsgErrors(diagnostics);
        Assert.NotEmpty(generatedSources);

        var generated = generatedSources.First();

        // Should use enum references in dispatch
        Assert.Contains("LightEventId.Toggle", generated);
        Assert.Contains("LightStateId.Off", generated);

        // Should NOT use string comparisons for dispatch
        Assert.DoesNotContain("\"Off\"", generated);
        Assert.DoesNotContain("\"Toggle\"", generated);
    }

    // ─── Test: Invalid definition produces diagnostics without crash ──────────────

    /// <summary>
    /// An invalid definition (missing initial state) produces diagnostics but does not crash.
    /// </summary>
    [Fact]
    public void InvalidDefinition_MissingInitialState_ProducesDiagnosticsWithoutCrash()
    {
        var source = @"
using System;
using System.Threading.Tasks;
using StateMachineSrcGen;

namespace TestApp;

public enum BadStateId
{
    A = 0,
    B = 1
}

public enum BadEventId
{
    Go = 0
}

public class BadState : IStateMachineState<BadStateId>
{
    public BadStateId GetStateId() => BadStateId.A;
}

public class BadEvent : IDispatchableEvent<BadEventId>
{
    public BadEventId GetEventId() => BadEventId.Go;
}

public static partial class BadMachine
{
    [Transition((int)BadStateId.A, (int)BadStateId.B, (int)BadEventId.Go)]
    public static BadState HandleGo(BadState state, BadEvent @event) => state;
}
";

        // Should not throw
        var (generatedSources, diagnostics) = RunGenerator(source);

        // Should have diagnostics about missing initial state (SMSG005)
        Assert.Contains(diagnostics, d => d.Id.StartsWith("SMSG"));
    }

    // ─── Helpers ────────────────────────────────────────────────────────────────

    private static (ImmutableList<string> GeneratedSources, ImmutableArray<Diagnostic> Diagnostics) RunGenerator(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var references = GetGeneratorReferences();

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

    private static void AssertNoSmsgErrors(ImmutableArray<Diagnostic> diagnostics)
    {
        var smsgErrors = diagnostics
            .Where(d => d.Id.StartsWith("SMSG") && d.Severity == DiagnosticSeverity.Error)
            .ToList();

        Assert.True(smsgErrors.Count == 0,
            $"Expected no SMSG errors but found: {string.Join(", ", smsgErrors.Select(d => $"{d.Id}: {d.GetMessage()}"))}");
    }

    /// <summary>
    /// Verifies the generated source compiles without errors by providing stub types
    /// that match what the generated code expects (enums, state/event types, handler methods).
    /// </summary>
    private static void AssertGeneratedCodeCompiles(
        string generatedSource,
        string ns,
        string className,
        string stateIdEnumName,
        string eventIdEnumName,
        string stateTypeName,
        string eventTypeName,
        (string Name, int Value)[] states,
        (string Name, int Value)[] events,
        (string MethodName, bool IsGuard, bool IsSideEffect)[] handlers,
        (string MethodName, bool ReturnsTState)[] entryCallbacks,
        string? cleanupHandlerName)
    {
        var supportCode = BuildSupportCode(ns, className, stateIdEnumName, eventIdEnumName,
            stateTypeName, eventTypeName, states, events, handlers, entryCallbacks, cleanupHandlerName);

        var syntaxTrees = new[]
        {
            CSharpSyntaxTree.ParseText(generatedSource),
            CSharpSyntaxTree.ParseText(supportCode)
        };

        var references = GetCompilationReferences();

        var compilation = CSharpCompilation.Create(
            "CompilationTestAssembly_" + Guid.NewGuid().ToString("N"),
            syntaxTrees,
            references,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        var errors = compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Where(d => !IsAllowedError(d))
            .ToList();

        Assert.True(errors.Count == 0,
            $"Generated code has compilation errors:\n{string.Join("\n", errors.Select(e => $"  {e.Id}: {e.GetMessage()} at {e.Location}"))}");
    }

    private static bool IsAllowedError(Diagnostic d)
    {
        // CS1998: async method lacks await - acceptable for generated stubs
        return d.Id == "CS1998";
    }

    /// <summary>
    /// Builds support code with stub types for the generated source to compile against.
    /// Uses the same pattern as GenerationPropertyTests to avoid IEquatable constraint issues.
    /// </summary>
    private static string BuildSupportCode(
        string ns,
        string className,
        string stateIdEnumName,
        string eventIdEnumName,
        string stateTypeName,
        string eventTypeName,
        (string Name, int Value)[] states,
        (string Name, int Value)[] events,
        (string MethodName, bool IsGuard, bool IsSideEffect)[] handlers,
        (string MethodName, bool ReturnsTState)[] entryCallbacks,
        string? cleanupHandlerName)
    {
        var stateMembers = string.Join(",\n        ", states.Select(s => $"{s.Name} = {s.Value}"));
        var eventMembers = string.Join(",\n        ", events.Select(e => $"{e.Name} = {e.Value}"));

        var handlerMethods = string.Join("\n", handlers.Select(h =>
        {
            if (h.IsGuard)
                return $"        public static bool {h.MethodName}({stateTypeName} state, {eventTypeName} @event) => true;";
            if (h.IsSideEffect)
                return $"        public static void {h.MethodName}({stateTypeName} state, {eventTypeName} @event) {{ }}";
            return $"        public static {stateTypeName} {h.MethodName}({stateTypeName} state, {eventTypeName} @event) => state;";
        }));

        var entryMethods = string.Join("\n", entryCallbacks.Select(ec =>
        {
            if (ec.ReturnsTState)
                return $"        public static {stateTypeName} {ec.MethodName}({stateTypeName} state, {eventTypeName} @event) => state;";
            else
                return $"        public static void {ec.MethodName}({stateTypeName} state, {eventTypeName} @event) {{ }}";
        }));

        var cleanupMethod = cleanupHandlerName != null
            ? $"        public static System.Threading.Tasks.Task {cleanupHandlerName}({stateTypeName} state) => System.Threading.Tasks.Task.CompletedTask;"
            : "";

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

    public interface IDispatchableEvent<TEventId> where TEventId : struct, Enum
    {{
        TEventId GetEventId();
    }}

    public interface IStateMachineState<TStateId> where TStateId : struct, Enum
    {{
        TStateId GetStateId();
    }}

    public readonly struct TransitionResult<TState>
    {{
        public TState? State {{ get; }}
        private TransitionResult(TState? state) {{ State = state; }}
        public static TransitionResult<TState> Succeeded(TState newState) => new(newState);
        public static TransitionResult<TState> GuardRejected => new(default);
        public static TransitionResult<TState> NoTransition => new(default);
    }}
}}

namespace {ns}
{{
    public enum {stateIdEnumName}
    {{
        {stateMembers}
    }}

    public enum {eventIdEnumName}
    {{
        {eventMembers}
    }}

    public class {stateTypeName} : StateMachineSrcGen.IStateMachineState<{stateIdEnumName}>
    {{
        public {stateIdEnumName} GetStateId() => default;
    }}

    public class {eventTypeName} : StateMachineSrcGen.IDispatchableEvent<{eventIdEnumName}>
    {{
        public {eventIdEnumName} GetEventId() => default;
    }}

    public static partial class {className}
    {{
{handlerMethods}
{entryMethods}
{cleanupMethod}
    }}
}}
";
    }

    /// <summary>
    /// Gets metadata references needed for running the source generator (includes attributes assembly).
    /// </summary>
    private static ImmutableArray<MetadataReference> GetGeneratorReferences()
    {
        var references = new List<MetadataReference>();

        // Add the attributes assembly for the generator to resolve attribute types
        references.Add(MetadataReference.CreateFromFile(typeof(TransitionAttribute).Assembly.Location));

        var trustedAssemblies = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))?.Split(Path.PathSeparator)
            ?? Array.Empty<string>();

        foreach (var assembly in trustedAssemblies)
        {
            var fileName = Path.GetFileName(assembly);
            if (fileName.Equals("System.Runtime.dll", StringComparison.OrdinalIgnoreCase) ||
                fileName.Equals("System.Threading.Tasks.dll", StringComparison.OrdinalIgnoreCase) ||
                fileName.Equals("System.Private.CoreLib.dll", StringComparison.OrdinalIgnoreCase) ||
                fileName.Equals("netstandard.dll", StringComparison.OrdinalIgnoreCase) ||
                fileName.Equals("mscorlib.dll", StringComparison.OrdinalIgnoreCase) ||
                fileName.Equals("System.Collections.dll", StringComparison.OrdinalIgnoreCase))
            {
                references.Add(MetadataReference.CreateFromFile(assembly));
            }
        }

        return references.ToImmutableArray();
    }

    /// <summary>
    /// Gets metadata references needed for compiling the generated source with stub types.
    /// Does NOT include the attributes assembly (stubs are provided instead).
    /// </summary>
    private static MetadataReference[] GetCompilationReferences()
    {
        var trustedAssemblies = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))?.Split(Path.PathSeparator)
            ?? Array.Empty<string>();

        return trustedAssemblies
            .Where(a =>
            {
                var fileName = Path.GetFileName(a);
                return fileName.Equals("System.Runtime.dll", StringComparison.OrdinalIgnoreCase) ||
                       fileName.Equals("System.Threading.Tasks.dll", StringComparison.OrdinalIgnoreCase) ||
                       fileName.Equals("System.Private.CoreLib.dll", StringComparison.OrdinalIgnoreCase) ||
                       fileName.Equals("netstandard.dll", StringComparison.OrdinalIgnoreCase) ||
                       fileName.Equals("mscorlib.dll", StringComparison.OrdinalIgnoreCase);
            })
            .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a))
            .ToArray();
    }
}
