// Feature: generic-state-machine-api
// Property 10: Generated dispatch uses enum comparisons
// Property 11: Non-terminal orchestration ordering
// Property 12: Terminal orchestration ordering
// Property 15: Targeted entry callback returns TState
// Property 16: Catch-all entry callback is void and observational
// Property 17: Terminal transition without cleanup completes normally
// Property 18: Generated code compiles without errors
// **Validates: Requirements 7.1, 7.2, 7.3, 7.4, 7.5, 10.5, 10.6, 10.10, 13.3, 13.6, 13.7, 13.8, 13.9, 13.12**

using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using StateMachineSrcGen;
using StateMachineSrcGen.Generation;
using StateMachineSrcGen.Tests.Generators;

namespace StateMachineSrcGen.Tests.Generation;

/// <summary>
/// Property-based tests for the Generation stage of the generic state machine API.
/// These tests validate correctness properties of the generated code structure,
/// ordering, and compilation.
/// </summary>
public class GenerationPropertyTests
{
    // ─── Property 10: Generated dispatch uses enum comparisons ───────────────────
    // **Validates: Requirements 7.1, 7.2, 7.3, 7.4**

    /// <summary>
    /// Property 10: For any validated state machine with enum-based state and event IDs,
    /// the generated dispatch code SHALL contain enum member references and SHALL NOT
    /// contain string literal comparisons for state or event routing.
    /// </summary>
    [Property(Arbitrary = new[] { typeof(GenericApiArbitraryProvider) })]
    public bool GeneratedDispatch_UsesEnumComparisons_NoStringLiterals(ValidatedStateMachine input)
    {
        // Only test machines with non-string enum types (the generic API)
        if (input.StateIdEnumTypeName == "string" || input.EventIdEnumTypeName == "string")
            return true; // Skip string-based machines, not relevant to this property

        if (!input.Transitions.Any())
            return true; // No transitions means no dispatch code to check

        var (source, diagnostics) = GenerationPipeline.Generate(input);

        if (source == null)
            return false;

        // The generated code should reference enum type members for dispatch
        // e.g., OrderStateId.Pending, OrderEventId.Confirm
        var stateEnumType = input.StateIdEnumTypeName;
        var eventEnumType = input.EventIdEnumTypeName;

        // Check that the dispatch uses enum-style comparisons
        // The generated switch should use enum case labels like: case EventId.Confirm:
        // And state comparisons like: currentState.GetStateId() == StateId.Pending
        foreach (var transition in input.Transitions)
        {
            // The event dispatch should reference the trigger as an enum member
            var expectedEventRef = $"{eventEnumType}.{transition.Trigger}";
            var expectedStateRef = $"{stateEnumType}.{transition.FromState}";

            // Check for enum references in the source
            var hasEnumEventRef = source.Contains(expectedEventRef);
            var hasEnumStateRef = source.Contains(expectedStateRef);

            if (!hasEnumEventRef || !hasEnumStateRef)
                return false;
        }

        // Verify no string literal comparisons are used for routing
        // String literals like "StateName" or "EventName" should not appear in switch/if dispatch
        foreach (var transition in input.Transitions)
        {
            // Check that the dispatch doesn't use string comparisons for state/event routing
            var stringEventPattern = $"\"{transition.Trigger}\"";
            var stringStatePattern = $"\"{transition.FromState}\"";

            // These string literals should NOT appear in the dispatch logic
            // (they may appear in XML comments or other non-dispatch contexts, so we check
            // specifically in the switch/if patterns)
            if (source.Contains($"case {stringEventPattern}") ||
                source.Contains($"== {stringStatePattern}") ||
                source.Contains($".Equals({stringStatePattern})"))
                return false;
        }

        return true;
    }

    // ─── Property 11: Non-terminal orchestration ordering ────────────────────────
    // **Validates: Requirements 7.5, 13.6, 13.9**

    /// <summary>
    /// Property 11: For any validated state machine with a non-terminal transition that has
    /// a guard, entry callback, and side-effect, the generated code SHALL follow the ordering:
    /// lock-acquire → guard → transition handler → targeted entry callback → catch-all entry
    /// callback → persist → side-effect → lock-release.
    /// </summary>
    [Property(Arbitrary = new[] { typeof(FullOrchestrationArbitraryProvider) })]
    public bool NonTerminalOrchestration_FollowsCorrectOrdering(ValidatedStateMachine input)
    {
        var nonTerminalTransitions = input.Transitions.Where(t => !t.IsTerminal).ToList();
        if (!nonTerminalTransitions.Any())
            return true;

        var (source, diagnostics) = GenerationPipeline.Generate(input);

        if (source == null)
            return false;

        // For each non-terminal transition with guard + side-effect + entry callbacks,
        // verify the ordering in the generated source
        foreach (var transition in nonTerminalTransitions)
        {
            if (transition.GuardMethodName == null && transition.SideEffectMethodName == null)
                continue;

            // Find the relevant section of generated code for this transition
            var handlerCall = transition.HandlerMethodName;

            // Verify ordering: guard before handler
            if (transition.GuardMethodName != null)
            {
                var guardIdx = source.IndexOf(transition.GuardMethodName, StringComparison.Ordinal);
                var handlerIdx = source.IndexOf(handlerCall + "(", StringComparison.Ordinal);
                if (guardIdx < 0 || handlerIdx < 0 || guardIdx >= handlerIdx)
                    return false;
            }

            // Verify ordering: handler before entry callbacks
            var targetedEntry = input.EntryCallbacks
                .FirstOrDefault(ec => !ec.IsCatchAll && ec.TargetStateName == transition.ToState);
            var catchAllEntry = input.EntryCallbacks
                .FirstOrDefault(ec => ec.IsCatchAll);

            var handlerCallIdx = source.IndexOf(handlerCall + "(", StringComparison.Ordinal);

            if (targetedEntry.MethodName != null)
            {
                var targetedIdx = source.IndexOf(targetedEntry.MethodName + "(", StringComparison.Ordinal);
                if (targetedIdx < 0 || handlerCallIdx >= targetedIdx)
                    return false;

                // Verify targeted entry before catch-all entry
                if (catchAllEntry.MethodName != null)
                {
                    var catchAllIdx = source.IndexOf(catchAllEntry.MethodName + "(", StringComparison.Ordinal);
                    if (catchAllIdx < 0 || targetedIdx >= catchAllIdx)
                        return false;
                }
            }

            // Verify ordering: entry callbacks before persist (SaveAsync)
            var saveIdx = source.IndexOf("SaveAsync(", StringComparison.Ordinal);
            if (saveIdx < 0 || handlerCallIdx >= saveIdx)
                return false;

            if (targetedEntry.MethodName != null)
            {
                var targetedIdx = source.IndexOf(targetedEntry.MethodName + "(", StringComparison.Ordinal);
                if (targetedIdx >= saveIdx)
                    return false;
            }

            // Verify ordering: persist before side-effect
            if (transition.SideEffectMethodName != null)
            {
                var sideEffectIdx = source.IndexOf(transition.SideEffectMethodName + "(", StringComparison.Ordinal);
                if (sideEffectIdx < 0 || saveIdx >= sideEffectIdx)
                    return false;
            }
        }

        // Verify lock acquire is at the start and release is in finally
        var lockAcquireIdx = source.IndexOf("AcquireAsync()", StringComparison.Ordinal);
        var lockReleaseIdx = source.IndexOf("ReleaseAsync()", StringComparison.Ordinal);
        if (lockAcquireIdx < 0 || lockReleaseIdx < 0 || lockAcquireIdx >= lockReleaseIdx)
            return false;

        // Verify lock release is in a finally block
        if (!source.Contains("finally"))
            return false;

        return true;
    }

    // ─── Property 12: Terminal orchestration ordering ─────────────────────────────
    // **Validates: Requirements 10.5, 10.10**

    /// <summary>
    /// Property 12: For any validated state machine with a terminal-state transition that has
    /// a cleanup handler and entry callback, the generated code SHALL follow the ordering:
    /// lock-acquire → guard → transition handler → targeted entry callback → catch-all entry
    /// callback → persist → cleanup → lock-release, with no side-effect invocation.
    /// </summary>
    [Property(Arbitrary = new[] { typeof(TerminalTransitionArbitraryProvider) })]
    public bool TerminalOrchestration_FollowsCorrectOrdering_NoSideEffect(ValidatedStateMachine input)
    {
        var terminalTransitions = input.Transitions.Where(t => t.IsTerminal).ToList();
        if (!terminalTransitions.Any())
            return true;

        var (source, diagnostics) = GenerationPipeline.Generate(input);

        if (source == null)
            return false;

        foreach (var transition in terminalTransitions)
        {
            var handlerCall = transition.HandlerMethodName;
            var handlerCallIdx = source.IndexOf(handlerCall + "(", StringComparison.Ordinal);
            if (handlerCallIdx < 0)
                return false;

            // Verify ordering: handler before persist
            var saveIdx = source.IndexOf("SaveAsync(", handlerCallIdx, StringComparison.Ordinal);
            if (saveIdx < 0)
                return false;

            // Verify ordering: persist before cleanup (if cleanup exists)
            if (input.CleanupHandlerMethodName != null)
            {
                var cleanupIdx = source.IndexOf(input.CleanupHandlerMethodName, saveIdx, StringComparison.Ordinal);
                if (cleanupIdx < 0)
                    return false;
            }

            // Verify: no side-effect invocation for terminal transitions
            if (transition.SideEffectMethodName != null)
            {
                // The side-effect method should NOT be called for terminal transitions
                // Look for the side-effect call after the handler call in the terminal branch
                var terminalSection = source.Substring(handlerCallIdx);
                var nextReturnIdx = terminalSection.IndexOf("return ", StringComparison.Ordinal);
                if (nextReturnIdx > 0)
                {
                    var terminalBlock = terminalSection.Substring(0, nextReturnIdx);
                    if (terminalBlock.Contains(transition.SideEffectMethodName + "("))
                        return false;
                }
            }
        }

        // Verify lock acquire/release ordering
        var lockAcquireIdx = source.IndexOf("AcquireAsync()", StringComparison.Ordinal);
        var lockReleaseIdx = source.IndexOf("ReleaseAsync()", StringComparison.Ordinal);
        if (lockAcquireIdx < 0 || lockReleaseIdx < 0 || lockAcquireIdx >= lockReleaseIdx)
            return false;

        return true;
    }

    // ─── Property 15: Targeted entry callback returns TState ─────────────────────
    // **Validates: Requirements 13.7, 13.8**

    /// <summary>
    /// Property 15: For any targeted [OnEnter(stateId)] method, the generated code SHALL use
    /// the return value of the entry callback as the state passed to subsequent operations (persist).
    /// </summary>
    [Property(Arbitrary = new[] { typeof(FullOrchestrationArbitraryProvider) })]
    public bool TargetedEntryCallback_ReturnValueUsedAsState(ValidatedStateMachine input)
    {
        var targetedCallbacks = input.EntryCallbacks.Where(ec => !ec.IsCatchAll && ec.ReturnsTState).ToList();
        if (!targetedCallbacks.Any())
            return true;

        var (source, diagnostics) = GenerationPipeline.Generate(input);

        if (source == null)
            return false;

        foreach (var callback in targetedCallbacks)
        {
            // The generated code should assign the return value of the targeted entry callback
            // Pattern: newState = OnEnterStateName(newState, @event);
            // or: var newState = OnEnterStateName(...);
            var callPattern = callback.MethodName + "(";
            var callIdx = source.IndexOf(callPattern, StringComparison.Ordinal);
            if (callIdx < 0)
                return false;

            // The return value should be captured (assigned to a variable)
            // Look for assignment pattern before the call: "newState = " or "var newState = "
            var lineStart = source.LastIndexOf('\n', callIdx);
            if (lineStart < 0) lineStart = 0;
            var lineBeforeCall = source.Substring(lineStart, callIdx - lineStart);

            // Should contain an assignment (= sign before the method call on the same line)
            if (!lineBeforeCall.Contains("="))
                return false;
        }

        return true;
    }

    // ─── Property 16: Catch-all entry callback is void and observational ─────────
    // **Validates: Requirements 13.12, 13.3**

    /// <summary>
    /// Property 16: For any catch-all [OnEnter] method (parameterless attribute), the generated
    /// code SHALL invoke it after any targeted callback without using a return value.
    /// </summary>
    [Property(Arbitrary = new[] { typeof(FullOrchestrationArbitraryProvider) })]
    public bool CatchAllEntryCallback_IsVoidAndObservational(ValidatedStateMachine input)
    {
        var catchAllCallback = input.EntryCallbacks.FirstOrDefault(ec => ec.IsCatchAll);
        if (catchAllCallback.MethodName == null)
            return true;

        var (source, diagnostics) = GenerationPipeline.Generate(input);

        if (source == null)
            return false;

        // The catch-all entry callback should be invoked
        var callPattern = catchAllCallback.MethodName + "(";
        var callIdx = source.IndexOf(callPattern, StringComparison.Ordinal);
        if (callIdx < 0)
            return false;

        // The call should NOT have its return value assigned (it's void)
        var lineStart = source.LastIndexOf('\n', callIdx);
        if (lineStart < 0) lineStart = 0;
        var lineBeforeCall = source.Substring(lineStart, callIdx - lineStart).TrimStart();

        // Should NOT contain an assignment pattern (no "= " or "var " before the call)
        // The line should start directly with the method call (possibly with indentation)
        if (lineBeforeCall.Contains("= ") || lineBeforeCall.Contains("var "))
            return false;

        // Verify it's called after any targeted callback
        var targetedCallbacks = input.EntryCallbacks.Where(ec => !ec.IsCatchAll).ToList();
        foreach (var targeted in targetedCallbacks)
        {
            var targetedIdx = source.IndexOf(targeted.MethodName + "(", StringComparison.Ordinal);
            if (targetedIdx >= 0 && targetedIdx >= callIdx)
                return false; // Targeted should come BEFORE catch-all
        }

        return true;
    }

    // ─── Property 17: Terminal transition without cleanup completes normally ──────
    // **Validates: Requirements 10.6**

    /// <summary>
    /// Property 17: For any validated state machine with a terminal-state transition but no
    /// [OnTerminal] handler defined, the generated code SHALL complete the transition
    /// (handler → entry → persist) without invoking any cleanup logic and SHALL return Success.
    /// </summary>
    [Property(Arbitrary = new[] { typeof(TerminalNoCleanupArbitraryProvider) })]
    public bool TerminalTransitionWithoutCleanup_CompletesNormally(ValidatedStateMachine input)
    {
        // This property only applies when there's no cleanup handler
        if (input.CleanupHandlerMethodName != null)
            return true;

        var terminalTransitions = input.Transitions.Where(t => t.IsTerminal).ToList();
        if (!terminalTransitions.Any())
            return true;

        var (source, diagnostics) = GenerationPipeline.Generate(input);

        if (source == null)
            return false;

        foreach (var transition in terminalTransitions)
        {
            // The handler should be called
            var handlerCallIdx = source.IndexOf(transition.HandlerMethodName + "(", StringComparison.Ordinal);
            if (handlerCallIdx < 0)
                return false;

            // Persist should be called after handler
            var saveIdx = source.IndexOf("SaveAsync(", handlerCallIdx, StringComparison.Ordinal);
            if (saveIdx < 0)
                return false;
        }

        // Should NOT contain any cleanup handler invocation
        // Common cleanup patterns: OnTerminalCleanup, HandleTerminal, etc.
        if (source.Contains("OnTerminalCleanup(") ||
            source.Contains("HandleTerminal(") ||
            source.Contains("CleanupOnComplete(") ||
            source.Contains("OnMachineTerminated("))
            return false;

        // Should return a success result
        if (!source.Contains("Success") && !source.Contains("Succeeded"))
            return false;

        return true;
    }

    // ─── Property 18: Generated code compiles without errors ─────────────────────
    // **Validates: Requirements 7.1, 7.2, 7.3, 7.4, 7.5, 7.6**

    /// <summary>
    /// Property 18: For any valid ValidatedStateMachine, generation produces compilable C# source.
    /// Uses in-memory Roslyn compilation with necessary type stubs to verify zero compilation errors.
    /// </summary>
    [Property(Arbitrary = new[] { typeof(GenericApiArbitraryProvider) })]
    public bool GeneratedCode_CompilesWithoutErrors_ForAnyValidMachine(ValidatedStateMachine input)
    {
        var (source, diagnostics) = GenerationPipeline.Generate(input);

        if (source == null)
            return false;

        // Build support code with the types referenced by the generated code
        var supportCode = BuildSupportCode(input);

        var compilationDiags = CompileAndGetErrors(source, supportCode);

        return !compilationDiags.Any(d => d.Severity == DiagnosticSeverity.Error);
    }

    // ─── Compilation Helpers ─────────────────────────────────────────────────────

    /// <summary>
    /// Builds support code (type stubs) for the generated source to compile against.
    /// </summary>
    private static string BuildSupportCode(ValidatedStateMachine input)
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
    /// Compiles generated source with support code and returns error diagnostics.
    /// </summary>
    private static ImmutableArray<Diagnostic> CompileAndGetErrors(string generatedSource, string supportCode)
    {
        var syntaxTrees = new[]
        {
            CSharpSyntaxTree.ParseText(generatedSource),
            CSharpSyntaxTree.ParseText(supportCode)
        };

        var references = GetMetadataReferences();

        var compilation = CSharpCompilation.Create(
            "TestAssembly_" + Guid.NewGuid().ToString("N"),
            syntaxTrees,
            references,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        return compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Where(d => !IsAllowedError(d))
            .ToImmutableArray();
    }

    private static bool IsAllowedError(Diagnostic d)
    {
        // CS1998: async method lacks await - acceptable for generated stubs
        return d.Id == "CS1998";
    }

    private static MetadataReference[] GetMetadataReferences()
    {
        var trustedAssemblies = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))?.Split(Path.PathSeparator)
            ?? Array.Empty<string>();

        return trustedAssemblies
            .Where(a =>
                a.EndsWith("System.Runtime.dll", StringComparison.OrdinalIgnoreCase) ||
                a.EndsWith("System.Threading.Tasks.dll", StringComparison.OrdinalIgnoreCase) ||
                a.EndsWith("System.Private.CoreLib.dll", StringComparison.OrdinalIgnoreCase) ||
                a.EndsWith("netstandard.dll", StringComparison.OrdinalIgnoreCase) ||
                a.EndsWith("mscorlib.dll", StringComparison.OrdinalIgnoreCase))
            .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a))
            .ToArray();
    }
}

// ─── Arbitrary Providers for FsCheck ─────────────────────────────────────────────

/// <summary>
/// Provides ValidGenericValidatedStateMachine arbitrary for properties that test
/// general generation behavior with enum-based state machines.
/// </summary>
public class GenericApiArbitraryProvider
{
    public static Arbitrary<ValidatedStateMachine> ValidatedStateMachine()
        => GenericApiArbitraries.ValidGenericValidatedStateMachine();
}

/// <summary>
/// Provides ValidatedStateMachineWithFullOrchestration arbitrary for properties that test
/// non-terminal orchestration with guards, side effects, and entry callbacks.
/// </summary>
public class FullOrchestrationArbitraryProvider
{
    public static Arbitrary<ValidatedStateMachine> ValidatedStateMachine()
        => GenericApiArbitraries.ValidatedStateMachineWithFullOrchestration();
}

/// <summary>
/// Provides ValidatedStateMachineWithTerminalTransitions arbitrary for properties that test
/// terminal orchestration ordering.
/// </summary>
public class TerminalTransitionArbitraryProvider
{
    public static Arbitrary<ValidatedStateMachine> ValidatedStateMachine()
        => GenericApiArbitraries.ValidatedStateMachineWithTerminalTransitions();
}

/// <summary>
/// Provides ValidatedStateMachine with terminal transitions but NO cleanup handler
/// for testing Property 17.
/// </summary>
public class TerminalNoCleanupArbitraryProvider
{
    public static Arbitrary<ValidatedStateMachine> ValidatedStateMachine()
        => TerminalNoCleanupGen().ToArbitrary();

    private static Gen<ValidatedStateMachine> TerminalNoCleanupGen()
    {
        return from className in StateMachineArbitraries.GenValidIdentifier()
               from ns in StateMachineArbitraries.GenValidNamespace()
               from stateEnumType in Gen.Elements("OrderStateId", "TaskStateId")
               from eventEnumType in Gen.Elements("OrderEventId", "TaskEventId")
               from stateType in Gen.Elements("OrderState", "TaskState")
               from eventType in Gen.Elements("OrderEvent", "TaskEvent")
               from state1Name in GenericApiArbitraries.GenEnumMemberName()
               from state2Name in GenericApiArbitraries.GenEnumMemberName()
                   .Where(n => n != state1Name)
               from state3Name in GenericApiArbitraries.GenEnumMemberName()
                   .Where(n => n != state1Name && n != state2Name)
               from triggerName in GenericApiArbitraries.GenEnumMemberName()
               from terminalTriggerName in GenericApiArbitraries.GenEnumMemberName()
                   .Where(n => n != triggerName)
               let initial = new ValidatedState
               {
                   Name = state1Name,
                   EnumValue = 0,
                   IsInitial = true,
                   IsTerminal = false
               }
               let middle = new ValidatedState
               {
                   Name = state2Name,
                   EnumValue = 1,
                   IsInitial = false,
                   IsTerminal = false
               }
               let terminal = new ValidatedState
               {
                   Name = state3Name,
                   EnumValue = 2,
                   IsInitial = false,
                   IsTerminal = true
               }
               select new ValidatedStateMachine
               {
                   Namespace = ns,
                   ClassName = className,
                   StateIdEnumTypeName = stateEnumType,
                   EventIdEnumTypeName = eventEnumType,
                   StateTypeName = stateType,
                   EventTypeName = eventType,
                   States = new EquatableArray<ValidatedState>(ImmutableArray.Create(initial, middle, terminal)),
                   InitialState = initial,
                   Transitions = new EquatableArray<ValidatedTransition>(ImmutableArray.Create(
                       new ValidatedTransition
                       {
                           FromState = state1Name,
                           ToState = state2Name,
                           Trigger = triggerName,
                           FromStateEnumValue = 0,
                           ToStateEnumValue = 1,
                           TriggerEnumValue = 0,
                           EventId = triggerName,
                           HandlerMethodName = $"Handle{triggerName}",
                           GuardMethodName = null,
                           SideEffectMethodName = null,
                           IsTerminal = false,
                           DeclarationOrder = 0
                       },
                       new ValidatedTransition
                       {
                           FromState = state2Name,
                           ToState = state3Name,
                           Trigger = terminalTriggerName,
                           FromStateEnumValue = 1,
                           ToStateEnumValue = 2,
                           TriggerEnumValue = 1,
                           EventId = terminalTriggerName,
                           HandlerMethodName = $"Handle{terminalTriggerName}",
                           GuardMethodName = null,
                           SideEffectMethodName = null,
                           IsTerminal = true,
                           DeclarationOrder = 1
                       })),
                   EntryCallbacks = new EquatableArray<ValidatedEntryCallback>(ImmutableArray<ValidatedEntryCallback>.Empty),
                   CleanupHandlerMethodName = null  // No cleanup handler
               };
    }
}
