// Feature: generic-state-machine-api
// Property 4: Integer-to-enum resolution round trip — **Validates: Requirements 8.2**

using System.Linq;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using StateMachineSrcGen.Parsing;
using StateMachineSrcGen.Tests.Generators;

namespace StateMachineSrcGen.Tests.Parsing;

/// <summary>
/// Property-based tests for integer-to-enum resolution round trip in the parsing pipeline.
/// Property 4: For any valid enum member, casting it to int and passing it through the
/// parser's enum resolution logic produces the original enum member name.
/// **Validates: Requirements 8.2**
/// </summary>
public class IntegerEnumResolutionProperties
{
    /// <summary>
    /// Property 4: Integer-to-enum resolution round trip
    /// For any valid enum member, casting to int and resolving back produces original member name.
    /// Tests end-to-end by creating a compilation with [Transition((int)StateId.X, ...)]
    /// and verifying the parser resolves the int back to the enum member name in the
    /// parsed handler's FromState/ToState/Trigger fields.
    /// **Validates: Requirements 8.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property IntCastEnumValues_ResolveBackToOriginalMemberNames()
    {
        return Prop.ForAll(
            GenericApiArbitraries.GenValidEnumConfiguration()
                .Where(config => config.Names.Length >= 2)
                .ToArbitrary(),
            enumConfig =>
            {
                var (memberNames, values) = enumConfig;

                // Pick first two state members and first event member for the transition
                var fromState = memberNames[0];
                var toState = memberNames[1];
                // Use a single event enum member
                var triggerName = "Trigger0";

                // Generate source with int-cast enum values in [Transition] attribute
                var source = GenerateSourceWithIntCastTransition(
                    memberNames, fromState, toState, triggerName);

                // Create compilation and parse
                var compilation = ParsingTestHelper.CreateCompilation(source);
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

                if (result is null)
                    return false.Label($"Parser returned null. Diagnostics: {string.Join(", ", diagnostics.Select(d => d.ToString()))}");

                // Find the transition handler
                var handler = result.Value.Handlers
                    .FirstOrDefault(h => h.Kind == HandlerKind.Transition);

                if (handler.MethodName is null)
                    return false.Label("No transition handler found in parsed result");

                // Verify the integer-to-enum resolution round trip:
                // The int values in the attribute should resolve back to the original enum member names
                var fromMatches = handler.FromState == fromState;
                var toMatches = handler.ToState == toState;
                var triggerMatches = handler.Trigger == triggerName;

                return (fromMatches && toMatches && triggerMatches)
                    .Label($"Expected FromState='{fromState}', ToState='{toState}', Trigger='{triggerName}' " +
                           $"but got FromState='{handler.FromState}', ToState='{handler.ToState}', Trigger='{handler.Trigger}'");
            });
    }

    // ─── Helper Methods ─────────────────────────────────────────────────────────

    /// <summary>
    /// Generates C# source code with a state ID enum and a state machine class
    /// that uses int-cast enum values in the [Transition] attribute.
    /// The attribute uses (int)TestStateId.X pattern to pass enum values as integers,
    /// and the parser must resolve them back to the original enum member names.
    /// </summary>
    private static string GenerateSourceWithIntCastTransition(
        string[] stateEnumMembers,
        string fromState,
        string toState,
        string triggerName)
    {
        var stateMembers = string.Join(",\n        ", stateEnumMembers);

        return $$"""
            using System;
            using System.Threading.Tasks;
            using StateMachineSrcGen;

            namespace TestNamespace;

            public enum TestStateId
            {
                {{stateMembers}}
            }

            public enum TestEventId
            {
                {{triggerName}}
            }

            public record TestState(TestStateId Id) : IStateMachineState<TestStateId>
            {
                public TestStateId GetStateId() => Id;
            }

            public record TestEvent(TestEventId EventType) : IDispatchableEvent<TestEventId>
            {
                public TestEventId GetEventId() => EventType;
            }

            [InitialState((int)TestStateId.{{fromState}})]
            public static partial class TestMachine
            {
                [Transition((int)TestStateId.{{fromState}}, (int)TestStateId.{{toState}}, (int)TestEventId.{{triggerName}})]
                public static TestState HandleTransition(TestState state, TestEvent @event)
                {
                    return state with { Id = TestStateId.{{toState}} };
                }
            }
            """;
    }
}
