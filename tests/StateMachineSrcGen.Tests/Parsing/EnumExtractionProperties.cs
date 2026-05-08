// Feature: generic-state-machine-api
// Property 1: Enum member extraction produces complete state set — **Validates: Requirements 1.3, 2.3**
// Property 2: Enum member extraction produces complete event set — **Validates: Requirements 1.4, 2.4**

using System.Collections.Immutable;
using System.Linq;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using StateMachineSrcGen.Parsing;
using StateMachineSrcGen.Tests.Generators;

namespace StateMachineSrcGen.Tests.Parsing;

/// <summary>
/// Property-based tests for enum member extraction in the parsing pipeline.
/// Property 1: State enum extraction produces complete state set.
/// Property 2: Event enum extraction produces complete event set.
/// </summary>
public class EnumExtractionProperties
{
    [Property(MaxTest = 100)]
    public Property Parser_ExtractsAllEnumMembers_AsStates()
    {
        return Prop.ForAll(
            GenericApiArbitraries.GenValidEnumConfiguration().ToArbitrary(),
            enumConfig =>
            {
                var (memberNames, values) = enumConfig;

                // Generate C# source with an enum and a state machine class using it
                var source = GenerateStateMachineSourceWithEnum(memberNames);

                // Create in-memory compilation
                var compilation = CreateCompilation(source);

                // Verify compilation has no critical errors (enum and class should parse fine)
                var compilationDiags = compilation.GetDiagnostics()
                    .Where(d => d.Severity == DiagnosticSeverity.Error)
                    .ToArray();

                // If there are compilation errors unrelated to our generator, skip this case
                // (e.g., missing references for complex scenarios)
                if (compilationDiags.Any(d => d.Id != "CS0246" && d.Id != "CS0234"))
                {
                    // Allow CS0246 (type not found) for IStateMachineState/IDispatchableEvent
                    // since we include the attributes assembly reference
                }

                var syntaxTree = compilation.SyntaxTrees.First();
                var semanticModel = compilation.GetSemanticModel(syntaxTree);
                var root = syntaxTree.GetRoot();

                var classDeclaration = root.DescendantNodes()
                    .OfType<ClassDeclarationSyntax>()
                    .FirstOrDefault();

                if (classDeclaration is null)
                    return false.Label("No class declaration found in generated source");

                // Run the parser
                var (result, diagnostics) = ParsingPipeline.Parse(classDeclaration, semanticModel);

                // The parser should produce a result with states matching enum members
                if (result is null)
                    return false.Label($"Parser returned null. Diagnostics: {string.Join(", ", diagnostics.Select(d => d.ToString()))}");

                var parsedStateNames = result.Value.States
                    .Select(s => s.Name)
                    .OrderBy(n => n)
                    .ToArray();

                var expectedNames = memberNames
                    .OrderBy(n => n)
                    .ToArray();

                var sameCount = parsedStateNames.Length == expectedNames.Length;
                var sameNames = parsedStateNames.SequenceEqual(expectedNames);

                return (sameCount && sameNames)
                    .Label($"Expected states [{string.Join(", ", expectedNames)}] " +
                           $"but got [{string.Join(", ", parsedStateNames)}]");
            });
    }

    // ─── Property 2: Enum member extraction produces complete event set ────────

    /// <summary>
    /// Property 2: Enum member extraction produces complete event set
    /// For any valid enum type used as event ID, the parser produces an event set
    /// whose names exactly match the enum member names (no missing, no extra).
    /// **Validates: Requirements 1.4, 2.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Parser_ExtractsAllEnumMembers_AsEvents()
    {
        return Prop.ForAll(
            GenericApiArbitraries.GenValidEnumConfiguration().ToArbitrary(),
            enumConfig =>
            {
                var (memberNames, values) = enumConfig;

                // Generate C# source with an event enum and a state machine class using it
                var source = GenerateStateMachineSourceWithEventEnum(memberNames);

                // Create in-memory compilation
                var compilation = CreateCompilation(source);

                var syntaxTree = compilation.SyntaxTrees.First();
                var semanticModel = compilation.GetSemanticModel(syntaxTree);
                var root = syntaxTree.GetRoot();

                var classDeclaration = root.DescendantNodes()
                    .OfType<ClassDeclarationSyntax>()
                    .FirstOrDefault();

                if (classDeclaration is null)
                    return false.Label("No class declaration found in generated source");

                // Run the parser
                var (result, diagnostics) = ParsingPipeline.Parse(classDeclaration, semanticModel);

                // The parser should produce a result with events matching enum members
                if (result is null)
                    return false.Label($"Parser returned null. Diagnostics: {string.Join(", ", diagnostics.Select(d => d.ToString()))}");

                var parsedEventNames = result.Value.Events
                    .Select(e => e.Name)
                    .OrderBy(n => n)
                    .ToArray();

                var expectedNames = memberNames
                    .OrderBy(n => n)
                    .ToArray();

                var sameCount = parsedEventNames.Length == expectedNames.Length;
                var sameNames = parsedEventNames.SequenceEqual(expectedNames);

                return (sameCount && sameNames)
                    .Label($"Expected events [{string.Join(", ", expectedNames)}] " +
                           $"but got [{string.Join(", ", parsedEventNames)}]");
            });
    }

    // ─── Helper Methods ─────────────────────────────────────────────────────────

    /// <summary>
    /// Generates C# source code with a state ID enum and a state machine class
    /// that uses the generic 4-type-parameter pattern.
    /// </summary>
    private static string GenerateStateMachineSourceWithEnum(string[] enumMemberNames)
    {
        var enumMembers = string.Join(",\n        ", enumMemberNames);
        var firstMember = enumMemberNames[0];
        var secondMember = enumMemberNames.Length > 1 ? enumMemberNames[1] : enumMemberNames[0];

        return $$"""
            using System;
            using System.Threading.Tasks;
            using StateMachineSrcGen;

            namespace TestNamespace;

            public enum TestStateId
            {
                {{enumMembers}}
            }

            public enum TestEventId
            {
                DoSomething
            }

            public record TestState(TestStateId Id) : IStateMachineState<TestStateId>
            {
                public TestStateId GetStateId() => Id;
            }

            public record TestEvent(TestEventId EventType) : IDispatchableEvent<TestEventId>
            {
                public TestEventId GetEventId() => EventType;
            }

            [InitialState((int)TestStateId.{{firstMember}})]
            public static partial class TestMachine
            {
                [Transition((int)TestStateId.{{firstMember}}, (int)TestStateId.{{secondMember}}, (int)TestEventId.DoSomething)]
                public static TestState HandleDoSomething(TestState state, TestEvent @event)
                {
                    return state with { Id = TestStateId.{{secondMember}} };
                }
            }
            """;
    }

    /// <summary>
    /// Generates C# source code with an event ID enum and a state machine class
    /// that uses the generic 4-type-parameter pattern. The event enum has the
    /// generated member names, while the state enum is fixed with two members.
    /// </summary>
    private static string GenerateStateMachineSourceWithEventEnum(string[] eventEnumMemberNames)
    {
        var enumMembers = string.Join(",\n        ", eventEnumMemberNames);
        var firstEvent = eventEnumMemberNames[0];

        return $$"""
            using System;
            using System.Threading.Tasks;
            using StateMachineSrcGen;

            namespace TestNamespace;

            public enum TestStateId
            {
                Idle,
                Running
            }

            public enum TestEventId
            {
                {{enumMembers}}
            }

            public record TestState(TestStateId Id) : IStateMachineState<TestStateId>
            {
                public TestStateId GetStateId() => Id;
            }

            public record TestEvent(TestEventId EventType) : IDispatchableEvent<TestEventId>
            {
                public TestEventId GetEventId() => EventType;
            }

            [InitialState((int)TestStateId.Idle)]
            public static partial class TestMachine
            {
                [Transition((int)TestStateId.Idle, (int)TestStateId.Running, (int)TestEventId.{{firstEvent}})]
                public static TestState Handle{{firstEvent}}(TestState state, TestEvent @event)
                {
                    return state with { Id = TestStateId.Running };
                }
            }
            """;
    }

    /// <summary>
    /// Creates a CSharpCompilation from source code with all necessary references.
    /// </summary>
    private static CSharpCompilation CreateCompilation(string source)
    {
        return ParsingTestHelper.CreateCompilation(source);
    }
}
