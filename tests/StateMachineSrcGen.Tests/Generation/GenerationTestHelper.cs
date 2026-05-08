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
    /// Creates a minimal valid ValidatedStateMachine with one transition.
    /// </summary>
    public static ValidatedStateMachine CreateValidStateMachine(
        string className = "TestMachine",
        string ns = "TestNamespace",
        string stateType = "string",
        string eventType = "TestEvent",
        string eventIdType = "string")
    {
        var idle = new ValidatedState { Name = "Idle", IsInitial = true, IsTerminal = false };
        var running = new ValidatedState { Name = "Running", IsInitial = false, IsTerminal = true };

        return new ValidatedStateMachine
        {
            Namespace = ns,
            ClassName = className,
            StateTypeName = stateType,
            EventTypeName = eventType,
            EventIdTypeName = eventIdType,
            ImplementsIStateMachineState = false,
            StateIdTypeName = null,
            States = new EquatableArray<ValidatedState>(ImmutableArray.Create(idle, running)),
            InitialState = idle,
            Transitions = new EquatableArray<ValidatedTransition>(ImmutableArray.Create(
                new ValidatedTransition
                {
                    FromState = "Idle",
                    ToState = "Running",
                    Trigger = "Start",
                    EventId = "Start",
                    HandlerMethodName = "HandleStart",
                    GuardMethodName = null,
                    SideEffectMethodName = null,
                    DeclarationOrder = 0
                }))
        };
    }

    /// <summary>
    /// Creates a ValidatedStateMachine with multiple transitions, guards, and side effects.
    /// </summary>
    public static ValidatedStateMachine CreateComplexStateMachine(
        string className = "ComplexMachine",
        string ns = "TestNamespace")
    {
        var idle = new ValidatedState { Name = "Idle", IsInitial = true, IsTerminal = false };
        var running = new ValidatedState { Name = "Running", IsInitial = false, IsTerminal = false };
        var stopped = new ValidatedState { Name = "Stopped", IsInitial = false, IsTerminal = true };

        return new ValidatedStateMachine
        {
            Namespace = ns,
            ClassName = className,
            StateTypeName = "string",
            EventTypeName = "TestEvent",
            EventIdTypeName = "string",
            ImplementsIStateMachineState = false,
            StateIdTypeName = null,
            States = new EquatableArray<ValidatedState>(ImmutableArray.Create(idle, running, stopped)),
            InitialState = idle,
            Transitions = new EquatableArray<ValidatedTransition>(ImmutableArray.Create(
                new ValidatedTransition
                {
                    FromState = "Idle",
                    ToState = "Running",
                    Trigger = "Start",
                    EventId = "Start",
                    HandlerMethodName = "HandleStart",
                    GuardMethodName = "CanStart",
                    SideEffectMethodName = "OnStarted",
                    DeclarationOrder = 0
                },
                new ValidatedTransition
                {
                    FromState = "Running",
                    ToState = "Stopped",
                    Trigger = "Stop",
                    EventId = "Stop",
                    HandlerMethodName = "HandleStop",
                    GuardMethodName = null,
                    SideEffectMethodName = null,
                    DeclarationOrder = 1
                }))
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
        string eventIdType,
        ValidatedState[] states,
        ValidatedTransition[] transitions)
    {
        var initial = states.FirstOrDefault(s => s.IsInitial);
        if (!states.Any(s => s.IsInitial))
        {
            initial = states.Length > 0 ? states[0] : new ValidatedState { Name = "Default", IsInitial = true, IsTerminal = true };
        }

        return new ValidatedStateMachine
        {
            Namespace = ns,
            ClassName = className,
            StateTypeName = stateType,
            EventTypeName = eventType,
            EventIdTypeName = eventIdType,
            ImplementsIStateMachineState = false,
            StateIdTypeName = null,
            States = new EquatableArray<ValidatedState>(states.ToImmutableArray()),
            InitialState = initial,
            Transitions = new EquatableArray<ValidatedTransition>(transitions.ToImmutableArray())
        };
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

        // Add user code that provides the types referenced by generated code
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

    public interface IDispatchableEvent<TEventId> where TEventId : IEquatable<TEventId>
    {
        TEventId GetEventId();
    }

    public enum TransitionResult
    {
        Success,
        NotHandled,
        LockFailed
    }
}

namespace TestNamespace
{
    public class TestEvent : StateMachineSrcGen.IDispatchableEvent<string>
    {
        public string EventId { get; set; } = """";
        public string GetEventId() => EventId;
    }

    public static partial class TestMachine
    {
        public static string HandleStart(string state, TestEvent @event) => ""Running"";
    }

    public static partial class ComplexMachine
    {
        public static bool CanStart(string state, TestEvent @event) => true;
        public static string HandleStart(string state, TestEvent @event) => ""Running"";
        public static void OnStarted(string state, TestEvent @event) { }
        public static string HandleStop(string state, TestEvent @event) => ""Stopped"";
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

        // Add core runtime references
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
