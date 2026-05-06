// Feature: state-machine-source-generator, Property 23: Pipeline stages never throw (parsing stage)
// **Validates: Requirements 9.1, 9.2, 9.3, 9.4**

using System;
using System.Collections.Immutable;
using System.Linq;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using StateMachineSrcGen;
using StateMachineSrcGen.Parsing;
using Xunit;

namespace StateMachineSrcGen.Tests.Parsing;

/// <summary>
/// Property 23: Pipeline stages never throw (parsing stage)
/// For any input (including malformed, null-containing, or edge-case inputs),
/// the parsing stage shall not throw an unhandled exception. It shall return
/// either a valid result or a collection of diagnostics.
/// </summary>
public class ParsingReliabilityProperties
{
    [Fact]
    public void Parsing_EmptySource_NeverThrows()
    {
        var sources = new[] { "", " ", "\n", "\t", "   \n\n  " };

        foreach (var source in sources)
        {
            var exception = Record.Exception(() =>
            {
                var compilation = ParsingTestHelper.CreateCompilation(source);
                var syntaxTree = compilation.SyntaxTrees.First();
                var semanticModel = compilation.GetSemanticModel(syntaxTree);
                var root = syntaxTree.GetRoot();

                var classDeclaration = root.DescendantNodes()
                    .OfType<ClassDeclarationSyntax>()
                    .FirstOrDefault();

                if (classDeclaration is not null)
                {
                    ParsingPipeline.Parse(classDeclaration, semanticModel);
                }
            });

            Assert.Null(exception);
        }
    }

    [Property]
    public bool Parsing_ClassWithNoAttributes_NeverThrows(NonEmptyString classRaw)
    {
        var className = ToIdentifier(classRaw);

        var source = $$"""
            namespace TestNamespace;

            public class {{className}}
            {
                public void DoSomething() { }
            }
            """;

        try
        {
            var (result, diagnostics) = ParsingTestHelper.ParseSource(source);
            return true;
        }
        catch
        {
            return false;
        }
    }

    [Property]
    public bool Parsing_MalformedAttributeParameters_NeverThrows(NonEmptyString classRaw)
    {
        var className = ToIdentifier(classRaw);

        // Attributes with empty string parameters
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

            [State("", IsInitial = true)]
            [Trigger("")]
            public static partial class {{className}}
            {
                [Transition("", "", "")]
                public static MyState HandleStart(MyState state, MyEvent @event)
                {
                    return state with { CurrentState = "" };
                }
            }
            """;

        try
        {
            var (result, diagnostics) = ParsingTestHelper.ParseSource(source);
            return true;
        }
        catch
        {
            return false;
        }
    }

    [Property]
    public bool Parsing_MissingUsingStatements_NeverThrows(NonEmptyString classRaw)
    {
        var className = ToIdentifier(classRaw);

        // Source without using StateMachineSrcGen — attributes won't resolve
        var source = $$"""
            namespace TestNamespace;

            public static partial class {{className}}
            {
                public static string HandleStart(string state, object @event)
                {
                    return state;
                }
            }
            """;

        try
        {
            var (result, diagnostics) = ParsingTestHelper.ParseSource(source);
            return true;
        }
        catch
        {
            return false;
        }
    }

    [Property]
    public bool Parsing_ClassWithOnlyStateAttributes_NeverThrows(
        NonEmptyString classRaw, NonEmptyString stateRaw)
    {
        var className = ToIdentifier(classRaw);
        var stateName = ToIdentifier(stateRaw);

        // Class with state attributes but no handlers
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
            public static partial class {{className}}
            {
            }
            """;

        try
        {
            var (result, diagnostics) = ParsingTestHelper.ParseSource(source);
            return true;
        }
        catch
        {
            return false;
        }
    }

    [Property]
    public bool Parsing_MultipleHandlersOnSameTransition_NeverThrows(NonEmptyString classRaw)
    {
        var className = ToIdentifier(classRaw);

        // Edge case: duplicate transition handlers
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
            [State("Running")]
            [Trigger("Start")]
            public static partial class {{className}}
            {
                [Transition("Idle", "Running", "Start")]
                public static MyState HandleStart(MyState state, MyEvent @event)
                {
                    return state with { CurrentState = "Running" };
                }

                [Transition("Idle", "Running", "Start")]
                public static MyState HandleStartDuplicate(MyState state, MyEvent @event)
                {
                    return state with { CurrentState = "Running" };
                }
            }
            """;

        try
        {
            var (result, diagnostics) = ParsingTestHelper.ParseSource(source);
            return true;
        }
        catch
        {
            return false;
        }
    }

    [Property]
    public bool Parsing_ArbitraryValidSource_NeverThrows(
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

        try
        {
            var (result, diagnostics) = ParsingTestHelper.ParseSource(source);
            return true;
        }
        catch
        {
            return false;
        }
    }

    [Property]
    public bool Parsing_ClassWithNoMethods_NeverThrows(NonEmptyString classRaw)
    {
        var className = ToIdentifier(classRaw);

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
            public static partial class {{className}}
            {
            }
            """;

        try
        {
            var (result, diagnostics) = ParsingTestHelper.ParseSource(source);
            return true;
        }
        catch
        {
            return false;
        }
    }

    // ─── Helper: Convert NonEmptyString to valid C# identifier ──────────────────

    private static string ToIdentifier(NonEmptyString raw)
    {
        var filtered = new string(raw.Get.Where(char.IsLetterOrDigit).ToArray());
        if (string.IsNullOrEmpty(filtered) || !char.IsLetter(filtered[0]))
            return "X" + filtered;
        return filtered;
    }
}
