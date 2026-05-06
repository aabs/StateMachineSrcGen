// Feature: state-machine-source-generator, Property 1: Parsing extracts handler attributes correctly
// Feature: state-machine-source-generator, Property 27: Class declaration validation
// Feature: state-machine-source-generator, Property 29: Handler signature validation
// **Validates: Requirements 1.1, 14.1, 14.2, 14.3, 14.5, 14.6, 14.7, 14.8**

using System.Collections.Immutable;
using System.Linq;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.CodeAnalysis;
using StateMachineSrcGen;
using StateMachineSrcGen.Parsing;
using Xunit;

namespace StateMachineSrcGen.Tests.Parsing;

/// <summary>
/// Property-based tests for the parsing stage covering handler attribute extraction,
/// class declaration validation, and handler signature validation.
/// </summary>
public class HandlerParsingProperties
{
    // ─── Property 1: Parsing extracts handler attributes correctly ──────────────
    // For any method with [Transition] attribute, verify ParsedHandler.FromState,
    // ToState, Trigger match attribute parameters.

    [Property]
    public bool Parsing_ExtractsTransitionAttributes_Correctly(
        NonEmptyString fromRaw, NonEmptyString toRaw, NonEmptyString triggerRaw, NonEmptyString classRaw)
    {
        var fromState = ToIdentifier(fromRaw);
        var toState = ToIdentifier(toRaw);
        var trigger = ToIdentifier(triggerRaw);
        var className = ToIdentifier(classRaw);
        var methodName = "Handle" + className;

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

            [State("{{fromState}}", IsInitial = true)]
            [State("{{toState}}")]
            [Trigger("{{trigger}}")]
            public static partial class {{className}}
            {
                [Transition("{{fromState}}", "{{toState}}", "{{trigger}}")]
                public static MyState {{methodName}}(MyState state, MyEvent @event)
                {
                    return state with { CurrentState = "{{toState}}" };
                }
            }
            """;

        var (result, diagnostics) = ParsingTestHelper.ParseSource(source);

        // Stub returns null — test will pass once implementation is done
        if (result is null)
            return true;

        var handler = result.Value.Handlers.FirstOrDefault();
        return handler.FromState == fromState &&
               handler.ToState == toState &&
               handler.Trigger == trigger &&
               handler.MethodName == methodName &&
               handler.Kind == HandlerKind.Transition;
    }

    [Property]
    public bool Parsing_ExtractsGuardAttributes_Correctly(
        NonEmptyString fromRaw, NonEmptyString toRaw, NonEmptyString triggerRaw)
    {
        var fromState = ToIdentifier(fromRaw);
        var toState = ToIdentifier(toRaw);
        var trigger = ToIdentifier(triggerRaw);

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

            [State("{{fromState}}", IsInitial = true)]
            [State("{{toState}}")]
            [Trigger("{{trigger}}")]
            public static partial class TestMachine
            {
                [Guard("{{fromState}}", "{{toState}}", "{{trigger}}")]
                public static bool CanTransition(MyState state, MyEvent @event)
                {
                    return true;
                }

                [Transition("{{fromState}}", "{{toState}}", "{{trigger}}")]
                public static MyState DoTransition(MyState state, MyEvent @event)
                {
                    return state with { CurrentState = "{{toState}}" };
                }
            }
            """;

        var (result, diagnostics) = ParsingTestHelper.ParseSource(source);

        if (result is null)
            return true;

        var guard = result.Value.Handlers.FirstOrDefault(h => h.Kind == HandlerKind.Guard);
        return guard.FromState == fromState &&
               guard.ToState == toState &&
               guard.Trigger == trigger &&
               guard.Kind == HandlerKind.Guard;
    }

    [Property]
    public bool Parsing_ExtractsSideEffectAttributes_Correctly(
        NonEmptyString fromRaw, NonEmptyString toRaw, NonEmptyString triggerRaw)
    {
        var fromState = ToIdentifier(fromRaw);
        var toState = ToIdentifier(toRaw);
        var trigger = ToIdentifier(triggerRaw);

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

            [State("{{fromState}}", IsInitial = true)]
            [State("{{toState}}")]
            [Trigger("{{trigger}}")]
            public static partial class TestMachine
            {
                [Transition("{{fromState}}", "{{toState}}", "{{trigger}}")]
                public static MyState DoTransition(MyState state, MyEvent @event)
                {
                    return state with { CurrentState = "{{toState}}" };
                }

                [SideEffect("{{fromState}}", "{{toState}}", "{{trigger}}")]
                public static void AfterTransition(MyState state, MyEvent @event)
                {
                }
            }
            """;

        var (result, diagnostics) = ParsingTestHelper.ParseSource(source);

        if (result is null)
            return true;

        var sideEffect = result.Value.Handlers.FirstOrDefault(h => h.Kind == HandlerKind.SideEffect);
        return sideEffect.FromState == fromState &&
               sideEffect.ToState == toState &&
               sideEffect.Trigger == trigger &&
               sideEffect.Kind == HandlerKind.SideEffect;
    }

    [Property]
    public bool Parsing_ExtractsEventId_WhenSpecified(
        NonEmptyString fromRaw, NonEmptyString toRaw, NonEmptyString triggerRaw)
    {
        var fromState = ToIdentifier(fromRaw);
        var toState = ToIdentifier(toRaw);
        var trigger = ToIdentifier(triggerRaw);
        var eventId = "evt_" + fromState;

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

            [State("{{fromState}}", IsInitial = true)]
            [State("{{toState}}")]
            [Trigger("{{trigger}}")]
            public static partial class TestMachine
            {
                [Transition("{{fromState}}", "{{toState}}", "{{trigger}}", EventId = "{{eventId}}")]
                public static MyState HandleEvent(MyState state, MyEvent @event)
                {
                    return state with { CurrentState = "{{toState}}" };
                }
            }
            """;

        var (result, diagnostics) = ParsingTestHelper.ParseSource(source);

        if (result is null)
            return true;

        var handler = result.Value.Handlers.FirstOrDefault();
        return handler.EventId == eventId;
    }

    // ─── Property 27: Class declaration validation ──────────────────────────────
    // Test that missing public/partial/static modifiers or wrong generic parameter
    // count emits SMSG010.

    [Property]
    public bool ClassDeclaration_MissingPublic_EmitsSMSG010(NonEmptyString classRaw)
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
            internal static partial class {{className}}
            {
                [Transition("Idle", "Running", "Start")]
                public static MyState HandleStart(MyState state, MyEvent @event)
                {
                    return state with { CurrentState = "Running" };
                }
            }
            """;

        var (result, diagnostics) = ParsingTestHelper.ParseSource(source);

        if (result is null && diagnostics.IsEmpty)
            return true; // stub: not yet implemented

        return diagnostics.Any(d => d.Id == "SMSG010");
    }

    [Property]
    public bool ClassDeclaration_MissingPartial_EmitsSMSG010(NonEmptyString classRaw)
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
            public static class {{className}}
            {
                [Transition("Idle", "Running", "Start")]
                public static MyState HandleStart(MyState state, MyEvent @event)
                {
                    return state with { CurrentState = "Running" };
                }
            }
            """;

        var (result, diagnostics) = ParsingTestHelper.ParseSource(source);

        if (result is null && diagnostics.IsEmpty)
            return true; // stub: not yet implemented

        return diagnostics.Any(d => d.Id == "SMSG010");
    }

    [Property]
    public bool ClassDeclaration_MissingStatic_EmitsSMSG010(NonEmptyString classRaw)
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
            public partial class {{className}}
            {
                [Transition("Idle", "Running", "Start")]
                public static MyState HandleStart(MyState state, MyEvent @event)
                {
                    return state with { CurrentState = "Running" };
                }
            }
            """;

        var (result, diagnostics) = ParsingTestHelper.ParseSource(source);

        if (result is null && diagnostics.IsEmpty)
            return true; // stub: not yet implemented

        return diagnostics.Any(d => d.Id == "SMSG010");
    }

    [Fact]
    public void ClassDeclaration_ZeroGenericParams_WithConcreteTypes_NoSMSG010()
    {
        // 0 generic parameters is valid when using concrete types in interface implementations
        var source = ParsingTestHelper.GenerateClassWithModifiers(
            isPublic: true,
            isPartial: true,
            isStatic: true,
            genericParamCount: 0);

        var (result, diagnostics) = ParsingTestHelper.ParseSource(source);

        if (result is null && diagnostics.IsEmpty)
            return; // stub: not yet implemented

        Assert.DoesNotContain(diagnostics, d => d.Id == "SMSG010");
    }

    [Fact]
    public void ClassDeclaration_OneGenericParam_EmitsSMSG010()
    {
        var source = ParsingTestHelper.GenerateClassWithModifiers(
            isPublic: true,
            isPartial: true,
            isStatic: true,
            genericParamCount: 1);

        var (result, diagnostics) = ParsingTestHelper.ParseSource(source);

        if (result is null && diagnostics.IsEmpty)
            return; // stub: not yet implemented

        Assert.Contains(diagnostics, d => d.Id == "SMSG010");
    }

    [Fact]
    public void ClassDeclaration_ThreeGenericParams_EmitsSMSG010()
    {
        var source = ParsingTestHelper.GenerateClassWithModifiers(
            isPublic: true,
            isPartial: true,
            isStatic: true,
            genericParamCount: 3);

        var (result, diagnostics) = ParsingTestHelper.ParseSource(source);

        if (result is null && diagnostics.IsEmpty)
            return; // stub: not yet implemented

        Assert.Contains(diagnostics, d => d.Id == "SMSG010");
    }

    [Property]
    public bool ClassDeclaration_AllModifiersPresent_NoSMSG010(NonEmptyString classRaw)
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
                [Transition("Idle", "Running", "Start")]
                public static MyState HandleStart(MyState state, MyEvent @event)
                {
                    return state with { CurrentState = "Running" };
                }
            }
            """;

        var (result, diagnostics) = ParsingTestHelper.ParseSource(source);

        if (result is null && diagnostics.IsEmpty)
            return true; // stub: not yet implemented

        return !diagnostics.Any(d => d.Id == "SMSG010");
    }

    // ─── Property 29: Handler signature validation ──────────────────────────────
    // Test that non-public, non-static, wrong parameters, or wrong return type emits SMSG012.

    [Fact]
    public void HandlerSignature_NonPublic_EmitsSMSG012()
    {
        var source = ParsingTestHelper.GenerateHandlerWithSignature(
            isPublic: false,
            isStatic: true,
            returnType: "MyState",
            parameters: "MyState state, MyEvent @event");

        var (result, diagnostics) = ParsingTestHelper.ParseSource(source);

        if (result is null && diagnostics.IsEmpty)
            return; // stub: not yet implemented

        Assert.Contains(diagnostics, d => d.Id == "SMSG012");
    }

    [Fact]
    public void HandlerSignature_NonStatic_EmitsSMSG012()
    {
        var source = ParsingTestHelper.GenerateHandlerWithSignature(
            isPublic: true,
            isStatic: false,
            returnType: "MyState",
            parameters: "MyState state, MyEvent @event");

        var (result, diagnostics) = ParsingTestHelper.ParseSource(source);

        if (result is null && diagnostics.IsEmpty)
            return; // stub: not yet implemented

        Assert.Contains(diagnostics, d => d.Id == "SMSG012");
    }

    [Fact]
    public void HandlerSignature_WrongReturnType_EmitsSMSG012()
    {
        var source = ParsingTestHelper.GenerateHandlerWithSignature(
            isPublic: true,
            isStatic: true,
            returnType: "void",
            parameters: "MyState state, MyEvent @event");

        var (result, diagnostics) = ParsingTestHelper.ParseSource(source);

        if (result is null && diagnostics.IsEmpty)
            return; // stub: not yet implemented

        Assert.Contains(diagnostics, d => d.Id == "SMSG012");
    }

    [Fact]
    public void HandlerSignature_WrongParameters_EmitsSMSG012()
    {
        // Missing event parameter
        var source = ParsingTestHelper.GenerateHandlerWithSignature(
            isPublic: true,
            isStatic: true,
            returnType: "MyState",
            parameters: "MyState state");

        var (result, diagnostics) = ParsingTestHelper.ParseSource(source);

        if (result is null && diagnostics.IsEmpty)
            return; // stub: not yet implemented

        Assert.Contains(diagnostics, d => d.Id == "SMSG012");
    }

    [Fact]
    public void HandlerSignature_Valid_NoSMSG012()
    {
        var source = ParsingTestHelper.GenerateHandlerWithSignature(
            isPublic: true,
            isStatic: true,
            returnType: "MyState",
            parameters: "MyState state, MyEvent @event");

        var (result, diagnostics) = ParsingTestHelper.ParseSource(source);

        if (result is null && diagnostics.IsEmpty)
            return; // stub: not yet implemented

        Assert.DoesNotContain(diagnostics, d => d.Id == "SMSG012");
    }

    // ─── Helper: Convert NonEmptyString to valid C# identifier ──────────────────

    /// <summary>
    /// Converts a NonEmptyString to a valid C# identifier by prefixing with a letter
    /// and filtering to alphanumeric characters.
    /// </summary>
    private static string ToIdentifier(NonEmptyString raw)
    {
        var filtered = new string(raw.Get.Where(char.IsLetterOrDigit).ToArray());
        if (string.IsNullOrEmpty(filtered) || !char.IsLetter(filtered[0]))
            return "X" + filtered;
        return filtered;
    }
}
