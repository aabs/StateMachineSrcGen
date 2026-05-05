// Feature: state-machine-source-generator, Property 25: Pipeline stage determinism (parsing stage)
// **Validates: Requirements 11.1**

using System.Collections.Immutable;
using System.Linq;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using StateMachineSrcGen;
using StateMachineSrcGen.Parsing;

namespace StateMachineSrcGen.Tests.Parsing;

/// <summary>
/// Property 25: Pipeline stage determinism (parsing stage)
/// For any input to the parsing stage, invoking the stage function multiple times
/// with the same input shall produce identical output each time.
/// </summary>
public class ParsingDeterminismProperties
{
    [Property]
    public bool Parsing_SameInput_ProducesIdenticalOutput(
        NonEmptyString classRaw, NonEmptyString stateRaw1,
        NonEmptyString stateRaw2, NonEmptyString triggerRaw)
    {
        var className = ToIdentifier(classRaw);
        var stateName1 = ToIdentifier(stateRaw1);
        var stateName2 = ToIdentifier(stateRaw2);
        var triggerName = ToIdentifier(triggerRaw);
        var methodName = "Handle" + triggerName;

        var source = ParsingTestHelper.GenerateValidStateMachineSource(
            className, stateName1, triggerName, methodName,
            fromState: stateName1, toState: stateName2, trigger: triggerName);

        // Parse the same source multiple times
        var compilation = ParsingTestHelper.CreateCompilation(source);
        var syntaxTree = compilation.SyntaxTrees.First();
        var semanticModel = compilation.GetSemanticModel(syntaxTree);
        var root = syntaxTree.GetRoot();

        var classDeclaration = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault();

        if (classDeclaration is null)
            return true;

        var (result1, diag1) = ParsingPipeline.Parse(classDeclaration, semanticModel);
        var (result2, diag2) = ParsingPipeline.Parse(classDeclaration, semanticModel);
        var (result3, diag3) = ParsingPipeline.Parse(classDeclaration, semanticModel);

        // All three invocations should produce identical results
        var resultsEqual = result1.Equals(result2) && result2.Equals(result3);
        var diagsEqual = DiagnosticsAreEqual(diag1, diag2) && DiagnosticsAreEqual(diag2, diag3);

        return resultsEqual && diagsEqual;
    }

    [Property]
    public bool Parsing_SameSourceRecompiled_ProducesIdenticalOutput(
        NonEmptyString classRaw, NonEmptyString stateRaw, NonEmptyString triggerRaw)
    {
        var className = ToIdentifier(classRaw);
        var stateName = ToIdentifier(stateRaw);
        var triggerName = ToIdentifier(triggerRaw);

        var source = $$"""
            using System;
            using System.Threading.Tasks;
            using StateMachineSrcGen;

            namespace TestNamespace;

            public record MyState(string CurrentState);
            public record MyEvent(string EventType) : IDispatchableEvent<string>
            {
                public string GetEventId() => EventType;
            }

            [State("{{stateName}}", IsInitial = true)]
            [Trigger("{{triggerName}}")]
            public static partial class {{className}} : IStateMachine<MyState, MyEvent>, IStatePersistence<MyState>
            {
                [Transition("{{stateName}}", "{{stateName}}", "{{triggerName}}")]
                public static MyState HandleEvent(MyState state, MyEvent @event)
                {
                    return state;
                }

                public Task<TransitionResult> HandleAsync(MyEvent @event) => throw new NotImplementedException();
                public Task<MyState> LoadAsync() => throw new NotImplementedException();
                public Task SaveAsync(MyState state) => throw new NotImplementedException();
            }
            """;

        // Create two separate compilations from the same source
        var (result1, diag1) = ParsingTestHelper.ParseSource(source);
        var (result2, diag2) = ParsingTestHelper.ParseSource(source);

        var resultsEqual = result1.Equals(result2);
        var diagsEqual = DiagnosticsAreEqual(diag1, diag2);

        return resultsEqual && diagsEqual;
    }

    [Property]
    public bool Parsing_InvalidSource_DeterministicDiagnostics(NonEmptyString classRaw)
    {
        var className = ToIdentifier(classRaw);

        // Source with missing modifiers — should produce deterministic diagnostics
        var source = $$"""
            using System;
            using System.Threading.Tasks;
            using StateMachineSrcGen;

            namespace TestNamespace;

            public record MyState(string CurrentState);
            public record MyEvent(string EventType) : IDispatchableEvent<string>
            {
                public string GetEventId() => EventType;
            }

            [State("Idle", IsInitial = true)]
            [Trigger("Start")]
            internal partial class {{className}} : IStateMachine<MyState, MyEvent>, IStatePersistence<MyState>
            {
                [Transition("Idle", "Running", "Start")]
                public static MyState HandleStart(MyState state, MyEvent @event)
                {
                    return state with { CurrentState = "Running" };
                }

                public Task<TransitionResult> HandleAsync(MyEvent @event) => throw new NotImplementedException();
                public Task<MyState> LoadAsync() => throw new NotImplementedException();
                public Task SaveAsync(MyState state) => throw new NotImplementedException();
            }
            """;

        var (result1, diag1) = ParsingTestHelper.ParseSource(source);
        var (result2, diag2) = ParsingTestHelper.ParseSource(source);
        var (result3, diag3) = ParsingTestHelper.ParseSource(source);

        var resultsEqual = result1.Equals(result2) && result2.Equals(result3);
        var diagsEqual = DiagnosticsAreEqual(diag1, diag2) && DiagnosticsAreEqual(diag2, diag3);

        return resultsEqual && diagsEqual;
    }

    // ─── Helpers ────────────────────────────────────────────────────────────────

    private static bool DiagnosticsAreEqual(ImmutableArray<Diagnostic> a, ImmutableArray<Diagnostic> b)
    {
        if (a.Length != b.Length)
            return false;

        for (int i = 0; i < a.Length; i++)
        {
            if (a[i].Id != b[i].Id ||
                a[i].Severity != b[i].Severity ||
                a[i].GetMessage() != b[i].GetMessage())
            {
                return false;
            }
        }

        return true;
    }

    private static string ToIdentifier(NonEmptyString raw)
    {
        var filtered = new string(raw.Get.Where(char.IsLetterOrDigit).ToArray());
        if (string.IsNullOrEmpty(filtered) || !char.IsLetter(filtered[0]))
            return "X" + filtered;
        return filtered;
    }
}
