using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using StateMachineSrcGen;

namespace StateMachineSrcGen.Tests.Generation;

/// <summary>
/// Helper class for generation property tests. Creates ValidatedStateMachine instances
/// and provides in-memory compilation utilities.
/// </summary>
internal static class GenerationTestHelper
{
    /// <summary>
    /// Creates a minimal valid ValidatedStateMachine with one transition using enum-based types.
    /// </summary>
    public static ValidatedStateMachine CreateValidStateMachine(
        string className = "TestMachine",
        string ns = "TestNamespace",
        string stateType = "TestState",
        string eventType = "TestEvent",
        string stateIdEnumType = "TestStateId",
        string eventIdEnumType = "TestEventId")
    {
        var idle = new ValidatedState { Name = "Idle", EnumValue = 0, IsInitial = true, IsTerminal = false };
        var running = new ValidatedState { Name = "Running", EnumValue = 1, IsInitial = false, IsTerminal = true };

        return new ValidatedStateMachine
        {
            Namespace = ns,
            ClassName = className,
            StateTypeName = stateType,
            EventTypeName = eventType,
            StateIdEnumTypeName = stateIdEnumType,
            EventIdEnumTypeName = eventIdEnumType,
            States = new EquatableArray<ValidatedState>(ImmutableArray.Create(idle, running)),
            InitialState = idle,
            Transitions = new EquatableArray<ValidatedTransition>(ImmutableArray.Create(
                new ValidatedTransition
                {
                    FromState = "Idle",
                    ToState = "Running",
                    Trigger = "Start",
                    FromStateEnumValue = 0,
                    ToStateEnumValue = 1,
                    TriggerEnumValue = 0,
                    EventId = "Start",
                    HandlerMethodName = "HandleStart",
                    GuardMethodName = null,
                    SideEffectMethodName = null,
                    IsTerminal = true,
                    DeclarationOrder = 0
                })),
            EntryCallbacks = new EquatableArray<ValidatedEntryCallback>(ImmutableArray<ValidatedEntryCallback>.Empty),
            CleanupHandlerMethodName = null
        };
    }

    /// <summary>
    /// Creates a ValidatedStateMachine with multiple transitions, guards, and side effects using enum-based types.
    /// </summary>
    public static ValidatedStateMachine CreateComplexStateMachine(
        string className = "ComplexMachine",
        string ns = "TestNamespace")
    {
        var idle = new ValidatedState { Name = "Idle", EnumValue = 0, IsInitial = true, IsTerminal = false };
        var running = new ValidatedState { Name = "Running", EnumValue = 1, IsInitial = false, IsTerminal = false };
        var stopped = new ValidatedState { Name = "Stopped", EnumValue = 2, IsInitial = false, IsTerminal = true };

        return new ValidatedStateMachine
        {
            Namespace = ns,
            ClassName = className,
            StateTypeName = "TestState",
            EventTypeName = "TestEvent",
            StateIdEnumTypeName = "TestStateId",
            EventIdEnumTypeName = "TestEventId",
            States = new EquatableArray<ValidatedState>(ImmutableArray.Create(idle, running, stopped)),
            InitialState = idle,
            Transitions = new EquatableArray<ValidatedTransition>(ImmutableArray.Create(
                new ValidatedTransition
                {
                    FromState = "Idle",
                    ToState = "Running",
                    Trigger = "Start",
                    FromStateEnumValue = 0,
                    ToStateEnumValue = 1,
                    TriggerEnumValue = 0,
                    EventId = "Start",
                    HandlerMethodName = "HandleStart",
                    GuardMethodName = "CanStart",
                    SideEffectMethodName = "OnStarted",
                    IsTerminal = false,
                    DeclarationOrder = 0
                },
                new ValidatedTransition
                {
                    FromState = "Running",
                    ToState = "Stopped",
                    Trigger = "Stop",
                    FromStateEnumValue = 1,
                    ToStateEnumValue = 2,
                    TriggerEnumValue = 1,
                    EventId = "Stop",
                    HandlerMethodName = "HandleStop",
                    GuardMethodName = null,
                    SideEffectMethodName = null,
                    IsTerminal = true,
                    DeclarationOrder = 1
                })),
            EntryCallbacks = new EquatableArray<ValidatedEntryCallback>(ImmutableArray<ValidatedEntryCallback>.Empty),
            CleanupHandlerMethodName = null
        };
    }

    /// <summary>
    /// Creates a ValidatedStateMachine with custom states and transitions.
    /// </summary>
    public static ValidatedStateMachine CreateStateMachine(
        string className,
        string ns,
        string stateType,
        string eventType,
        string stateIdEnumType,
        string eventIdEnumType,
        ValidatedState[] states,
        ValidatedTransition[] transitions)
    {
        var initial = states.FirstOrDefault(s => s.IsInitial);
        if (!states.Any(s => s.IsInitial))
        {
            initial = states.Length > 0 ? states[0] : new ValidatedState { Name = "Default", EnumValue = 0, IsInitial = true, IsTerminal = true };
        }

        return new ValidatedStateMachine
        {
            Namespace = ns,
            ClassName = className,
            StateTypeName = stateType,
            EventTypeName = eventType,
            StateIdEnumTypeName = stateIdEnumType,
            EventIdEnumTypeName = eventIdEnumType,
            States = new EquatableArray<ValidatedState>(states.ToImmutableArray()),
            InitialState = initial,
            Transitions = new EquatableArray<ValidatedTransition>(transitions.ToImmutableArray()),
            EntryCallbacks = new EquatableArray<ValidatedEntryCallback>(ImmutableArray<ValidatedEntryCallback>.Empty),
            CleanupHandlerMethodName = null
        };
    }

    /// <summary>
    /// Builds support code for enum-based state machines that provides all types needed for compilation.
    /// </summary>
    public static string BuildEnumSupportCode(ValidatedStateMachine input)
    {
        var handlerMethods = string.Join("\n", input.Transitions.Select(t =>
        {
            var methods = $"        public static {input.StateTypeName} {t.HandlerMethodName}({input.StateTypeName} state, {input.EventTypeName} @event) => state;";

            if (t.GuardMethodName != null)
                methods += $"\n        public static bool {t.GuardMethodName}({input.StateTypeName} state, {input.EventTypeName} @event) => true;";

            if (t.SideEffectMethodName != null)
                methods += $"\n        public static void {t.SideEffectMethodName}({input.StateTypeName} state, {input.EventTypeName} @event) {{ }}";

            return methods;
        }));

        var entryMethods = string.Join("\n", input.EntryCallbacks.Select(ec =>
        {
            if (ec.ReturnsTState)
                return $"        public static {input.StateTypeName} {ec.MethodName}({input.StateTypeName} state, {input.EventTypeName} @event) => state;";
            else
                return $"        public static void {ec.MethodName}({input.StateTypeName} state, {input.EventTypeName} @event) {{ }}";
        }));

        var cleanupMethod = input.CleanupHandlerMethodName != null
            ? $"        public static System.Threading.Tasks.Task {input.CleanupHandlerMethodName}({input.StateTypeName} state) => System.Threading.Tasks.Task.CompletedTask;"
            : "";

        var stateIdEnumMembers = string.Join(",\n        ",
            input.States.Select(s => $"{s.Name} = {s.EnumValue}"));

        var eventIdEnumMembers = string.Join(",\n        ",
            input.Transitions.Select(t => (t.Trigger, t.TriggerEnumValue))
                .Distinct()
                .Select(e => $"{e.Trigger} = {e.TriggerEnumValue}"));

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

    public interface IStateMachineState<TStateId> where TStateId : struct, Enum
    {{
        TStateId GetStateId();
    }}

    public interface IDispatchableEvent<TEventId> where TEventId : struct, Enum
    {{
        TEventId GetEventId();
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

namespace {input.Namespace}
{{
    public enum {input.StateIdEnumTypeName}
    {{
        {stateIdEnumMembers}
    }}

    public enum {input.EventIdEnumTypeName}
    {{
        {eventIdEnumMembers}
    }}

    public class {input.StateTypeName} : StateMachineSrcGen.IStateMachineState<{input.StateIdEnumTypeName}>
    {{
        public {input.StateIdEnumTypeName} GetStateId() => default;
    }}

    public class {input.EventTypeName} : StateMachineSrcGen.IDispatchableEvent<{input.EventIdEnumTypeName}>
    {{
        public {input.EventIdEnumTypeName} GetEventId() => default;
    }}

    public static partial class {input.ClassName}
    {{
{handlerMethods}
{entryMethods}
{cleanupMethod}
    }}
}}
";
    }

    /// <summary>
    /// Compiles generated source code in-memory and returns the compilation result.
    /// </summary>
    public static CSharpCompilation CompileGeneratedSource(string generatedSource, string? userCode = null)
    {
        var syntaxTrees = new List<SyntaxTree>
        {
            CSharpSyntaxTree.ParseText(generatedSource)
        };

        var supportCode = userCode ?? GetDefaultSupportCode();
        syntaxTrees.Add(CSharpSyntaxTree.ParseText(supportCode));

        var references = GetMetadataReferences();

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            syntaxTrees,
            references,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        return compilation;
    }

    /// <summary>
    /// Compiles and returns diagnostics (errors and warnings).
    /// </summary>
    public static ImmutableArray<Diagnostic> GetCompilationDiagnostics(string generatedSource, string? userCode = null)
    {
        var compilation = CompileGeneratedSource(generatedSource, userCode);
        return compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error || d.Severity == DiagnosticSeverity.Warning)
            .Where(d => !IsAllowedWarning(d))
            .ToImmutableArray();
    }

    /// <summary>
    /// Checks if a diagnostic is an allowed warning that we can ignore.
    /// </summary>
    private static bool IsAllowedWarning(Diagnostic d)
    {
        // CS1998: async method lacks await - we use async for interface compliance
        // CS0649: field never assigned - persistence/lock fields are assigned inline
        // CS8618: non-nullable field not initialized - handled by inline initialization
        return d.Id == "CS1998" || d.Id == "CS0649" || d.Id == "CS8618";
    }

    /// <summary>
    /// Gets the default support code that provides types referenced by generated code.
    /// </summary>
    private static string GetDefaultSupportCode()
    {
        return @"
#nullable enable
using System;
using System.Threading.Tasks;

namespace StateMachineSrcGen
{
    public interface IStatePersistence<TState>
    {
        Task<TState> LoadAsync();
        Task SaveAsync(TState state);
    }

    public interface IStateLock<TState>
    {
        Task<bool> AcquireAsync();
        Task ReleaseAsync();
    }

    public interface IStateMachineState<TStateId> where TStateId : struct, Enum
    {
        TStateId GetStateId();
    }

    public interface IDispatchableEvent<TEventId> where TEventId : struct, Enum
    {
        TEventId GetEventId();
    }

    public readonly struct TransitionResult<TState>
    {
        public TState? State { get; }
        private TransitionResult(TState? state) { State = state; }
        public static TransitionResult<TState> Succeeded(TState newState) => new(newState);
        public static TransitionResult<TState> GuardRejected => new(default);
        public static TransitionResult<TState> NoTransition => new(default);
    }
}

namespace TestNamespace
{
    public enum TestStateId
    {
        Idle = 0,
        Running = 1,
        Stopped = 2
    }

    public enum TestEventId
    {
        Start = 0,
        Stop = 1
    }

    public class TestState : StateMachineSrcGen.IStateMachineState<TestStateId>
    {
        public TestStateId GetStateId() => default;
    }

    public class TestEvent : StateMachineSrcGen.IDispatchableEvent<TestEventId>
    {
        public TestEventId GetEventId() => default;
    }

    public static partial class TestMachine
    {
        public static TestState HandleStart(TestState state, TestEvent @event) => state;
    }

    public static partial class ComplexMachine
    {
        public static bool CanStart(TestState state, TestEvent @event) => true;
        public static TestState HandleStart(TestState state, TestEvent @event) => state;
        public static void OnStarted(TestState state, TestEvent @event) { }
        public static TestState HandleStop(TestState state, TestEvent @event) => state;
    }
}
";
    }

    /// <summary>
    /// Gets metadata references needed for compilation.
    /// </summary>
    private static List<MetadataReference> GetMetadataReferences()
    {
        var references = new List<MetadataReference>();

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

        return references;
    }
}
