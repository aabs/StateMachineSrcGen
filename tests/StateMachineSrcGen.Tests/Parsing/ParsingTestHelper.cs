using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using StateMachineSrcGen.Parsing;

namespace StateMachineSrcGen.Tests.Parsing;

/// <summary>
/// Helper class for parsing property tests. Creates in-memory Roslyn compilations
/// from source code strings and invokes the parsing pipeline.
/// </summary>
internal static class ParsingTestHelper
{
    /// <summary>
    /// The standard set of metadata references needed for compilation,
    /// including the Attributes assembly.
    /// </summary>
    private static readonly Lazy<ImmutableArray<MetadataReference>> s_references = new(() =>
    {
        var refs = new MetadataReference[]
        {
            // Core runtime references
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Attribute).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Threading.Tasks.Task).Assembly.Location),
            // Attributes assembly
            MetadataReference.CreateFromFile(typeof(TransitionAttribute).Assembly.Location),
        };

        // Add netstandard and System.Runtime references
        var runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        var additionalRefs = new[]
        {
            Path.Combine(runtimeDir, "netstandard.dll"),
            Path.Combine(runtimeDir, "System.Runtime.dll"),
            Path.Combine(runtimeDir, "System.Collections.dll"),
        };

        var allRefs = refs.AsEnumerable();
        foreach (var path in additionalRefs)
        {
            if (File.Exists(path))
            {
                allRefs = allRefs.Append(MetadataReference.CreateFromFile(path));
            }
        }

        return allRefs.ToImmutableArray();
    });

    /// <summary>
    /// Gets the standard metadata references for compilation.
    /// </summary>
    public static ImmutableArray<MetadataReference> References => s_references.Value;

    /// <summary>
    /// Creates a CSharpCompilation from the given source code string.
    /// </summary>
    public static CSharpCompilation CreateCompilation(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        return CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: new[] { syntaxTree },
            references: References,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithNullableContextOptions(NullableContextOptions.Enable));
    }

    /// <summary>
    /// Parses the first class declaration found in the source code using the ParsingPipeline.
    /// Returns the parsing result and any diagnostics.
    /// </summary>
    public static (ParsedStateMachine? Result, ImmutableArray<Diagnostic> Diagnostics) ParseSource(string source)
    {
        var compilation = CreateCompilation(source);
        var syntaxTree = compilation.SyntaxTrees.First();
        var semanticModel = compilation.GetSemanticModel(syntaxTree);
        var root = syntaxTree.GetRoot();

        var classDeclaration = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault();

        if (classDeclaration is null)
        {
            return (null, ImmutableArray<Diagnostic>.Empty);
        }

        return ParsingPipeline.Parse(classDeclaration, semanticModel);
    }

    /// <summary>
    /// Generates a valid state machine class source with the given parameters.
    /// </summary>
    public static string GenerateValidStateMachineSource(
        string className,
        string stateName,
        string triggerName,
        string handlerMethodName,
        string fromState,
        string toState,
        string trigger)
    {
        return $$"""
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
            [State("{{toState}}")]
            [Trigger("{{triggerName}}")]
            public static partial class {{className}} : IStateMachine<MyState, MyEvent>, IStatePersistence<MyState>
            {
                [Transition("{{fromState}}", "{{toState}}", "{{trigger}}")]
                public static MyState {{handlerMethodName}}(MyState state, MyEvent @event)
                {
                    return state with { CurrentState = "{{toState}}" };
                }

                public Task<TransitionResult> HandleAsync(MyEvent @event) => throw new NotImplementedException();
                public Task<MyState> LoadAsync() => throw new NotImplementedException();
                public Task SaveAsync(MyState state) => throw new NotImplementedException();
            }
            """;
    }

    /// <summary>
    /// Generates a minimal class source with configurable modifiers.
    /// </summary>
    public static string GenerateClassWithModifiers(
        bool isPublic,
        bool isPartial,
        bool isStatic,
        int genericParamCount,
        bool implementsIStateMachine = true,
        bool implementsIStatePersistence = true,
        bool implementsIDispatchableEvent = true)
    {
        var accessModifier = isPublic ? "public" : "internal";
        var partialModifier = isPartial ? "partial" : "";
        var staticModifier = isStatic ? "static" : "";

        var genericParams = genericParamCount switch
        {
            0 => "",
            1 => "<MyState>",
            2 => "<MyState, MyEvent>",
            3 => "<MyState, MyEvent, Extra>",
            _ => "<" + string.Join(", ", Enumerable.Range(1, genericParamCount).Select(i => $"T{i}")) + ">"
        };

        var interfaces = new System.Collections.Generic.List<string>();
        if (implementsIStateMachine) interfaces.Add("IStateMachine<MyState, MyEvent>");
        if (implementsIStatePersistence) interfaces.Add("IStatePersistence<MyState>");

        var interfaceList = interfaces.Count > 0 ? " : " + string.Join(", ", interfaces) : "";

        var eventTypeDecl = implementsIDispatchableEvent
            ? "public record MyEvent(string EventType) : IDispatchableEvent<string>\n{\n    public string GetEventId() => EventType;\n}"
            : "public record MyEvent(string EventType);";

        return $$"""
            using System;
            using System.Threading.Tasks;
            using StateMachineSrcGen;

            namespace TestNamespace;

            public record MyState(string CurrentState);
            {{eventTypeDecl}}

            [State("Idle", IsInitial = true)]
            [Trigger("Start")]
            {{accessModifier}} {{staticModifier}} {{partialModifier}} class TestMachine{{genericParams}}{{interfaceList}}
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
    }

    /// <summary>
    /// Generates a handler method source with configurable signature properties.
    /// </summary>
    public static string GenerateHandlerWithSignature(
        bool isPublic,
        bool isStatic,
        string returnType,
        string parameters)
    {
        var accessModifier = isPublic ? "public" : "private";
        var staticModifier = isStatic ? "static" : "";

        return $$"""
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
            public static partial class TestMachine : IStateMachine<MyState, MyEvent>, IStatePersistence<MyState>
            {
                [Transition("Idle", "Running", "Start")]
                {{accessModifier}} {{staticModifier}} {{returnType}} HandleStart({{parameters}})
                {
                    return default!;
                }

                public Task<TransitionResult> HandleAsync(MyEvent @event) => throw new NotImplementedException();
                public Task<MyState> LoadAsync() => throw new NotImplementedException();
                public Task SaveAsync(MyState state) => throw new NotImplementedException();
            }
            """;
    }
}
