// Feature: state-machine-source-generator, Property 17: Generated code compiles without warnings
// Feature: state-machine-source-generator, Property 26: Full pipeline round-trip compilation
// **Validates: Requirements 7.1, 11.4**

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
/// Property 17: Generated code compiles without warnings
/// For any valid ValidatedStateMachine, generated C# compiles without errors or warnings
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

        var userCode = GetUserCodeForMachine(className, ns, "TestEvent", "string",
            new[] { ("HandleStart", "string", false, false) });

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

        var userCode = GetUserCodeForMachine(className, ns, "TestEvent", "string",
            new[]
            {
                ("HandleStart", "string", false, false),
                ("CanStart", "bool", true, false),
                ("OnStarted", "void", false, true),
                ("HandleStop", "string", false, false)
            });

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

        var userCode = GetUserCodeForMachine(className, ns, "TestEvent", "string",
            new[] { ("HandleStart", "string", false, false) });

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

        var userCode = GetUserCodeForMachine(className, ns, "TestEvent", "string",
            new[] { ("HandleStart", "string", false, false) });

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

    /// <summary>
    /// Generates user code that provides the event type and handler methods needed by the generated code.
    /// </summary>
    private static string GetUserCodeForMachine(
        string className, string ns, string eventType, string stateType,
        (string MethodName, string ReturnType, bool IsGuard, bool IsSideEffect)[] handlers)
    {
        var handlerMethods = string.Join("\n", handlers.Select(h =>
        {
            if (h.IsGuard)
                return $"        public static bool {h.MethodName}(string state, {eventType} @event) => true;";
            if (h.IsSideEffect)
                return $"        public static void {h.MethodName}(string state, {eventType} @event) {{ }}";
            return $"        public static string {h.MethodName}(string state, {eventType} @event) => \"NewState\";";
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
    public class {eventType} : StateMachineSrcGen.IDispatchableEvent<string>
    {{
        public string EventId {{ get; set; }} = """";
        public string GetEventId() => EventId;
    }}

    public static partial class {className}
    {{
{handlerMethods}
    }}
}}
";
    }
}
