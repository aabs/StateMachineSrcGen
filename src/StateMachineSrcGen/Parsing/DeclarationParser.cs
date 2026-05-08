using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using StateMachineSrcGen.Diagnostics;

namespace StateMachineSrcGen.Parsing;

/// <summary>
/// Extracts class-level information: modifiers, generic type parameters,
/// inferred state/event types from handler signatures, and class-level attributes ([State], [Trigger]).
/// </summary>
internal static class DeclarationParser
{
    /// <summary>
    /// Result of parsing a class declaration.
    /// </summary>
    internal readonly struct DeclarationResult
    {
        public ClassModifiers Modifiers { get; init; }
        public string ClassName { get; init; }
        public string Namespace { get; init; }
        public string StateTypeName { get; init; }
        public string EventTypeName { get; init; }
        public bool ImplementsIDispatchableEvent { get; init; }
        public string? EventIdTypeName { get; init; }
        public bool ImplementsIStateMachineState { get; init; }
        public string? StateIdTypeName { get; init; }
        public ImmutableArray<ParsedState> States { get; init; }
        public ImmutableArray<ParsedTrigger> Triggers { get; init; }
        public Location Location { get; init; }
        public bool IsValid { get; init; }
    }

    /// <summary>
    /// Parses the class declaration to extract modifiers, inferred types, states, and triggers.
    /// Returns diagnostics if the declaration is invalid.
    /// </summary>
    public static (DeclarationResult Result, ImmutableArray<Diagnostic> Diagnostics) Parse(
        ClassDeclarationSyntax classDeclaration,
        SemanticModel semanticModel)
    {
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        var location = classDeclaration.Identifier.GetLocation();

        // Extract modifiers
        var modifiers = ExtractModifiers(classDeclaration);

        // Extract class name
        var className = classDeclaration.Identifier.Text;

        // Extract namespace
        var ns = ExtractNamespace(classDeclaration);

        // Get the declared symbol
        var classSymbol = semanticModel.GetDeclaredSymbol(classDeclaration) as INamedTypeSymbol;

        // Validate modifiers
        var missingModifiers = new List<string>();
        if ((modifiers & ClassModifiers.Public) == 0) missingModifiers.Add("public");
        if ((modifiers & ClassModifiers.Partial) == 0) missingModifiers.Add("partial");
        if ((modifiers & ClassModifiers.Static) == 0) missingModifiers.Add("static");

        // Validate generic type parameter count on the class itself.
        // The class should have either 0 generic params (using concrete types) or exactly 2.
        // Having 1 or 3+ generic params is invalid.
        var genericParamCount = classDeclaration.TypeParameterList?.Parameters.Count ?? 0;
        if (genericParamCount != 0 && genericParamCount != 2)
        {
            missingModifiers.Add($"exactly 2 generic type parameters (found {genericParamCount})");
        }

        if (missingModifiers.Count > 0)
        {
            diagnostics.Add(Diagnostic.Create(
                DiagnosticDescriptors.InvalidClassDeclaration,
                location,
                string.Join(", ", missingModifiers)));
        }

        // Infer TState and TEvent from the first [Transition] handler's parameters
        var (stateTypeName, eventTypeName) = InferTypesFromHandlers(classDeclaration, semanticModel);

        // Detect IDispatchableEvent<TEventId> on the inferred event type
        var (implementsIDispatchableEvent, eventIdTypeName) = DetectIDispatchableEvent(
            classSymbol, eventTypeName, semanticModel, classDeclaration);

        // Detect IStateMachineState<TStateId> on the inferred state type
        var (implementsIStateMachineState, stateIdTypeName) = DetectIStateMachineState(
            stateTypeName, semanticModel, classDeclaration);

        // Extract [State] and [Trigger] attributes
        var states = ExtractStates(classDeclaration, semanticModel);
        var triggers = ExtractTriggers(classDeclaration, semanticModel);

        // Use Location.None in the result model for deterministic equality across compilations.
        // Real locations are preserved in diagnostics.
        var result = new DeclarationResult
        {
            Modifiers = modifiers,
            ClassName = className,
            Namespace = ns,
            StateTypeName = stateTypeName,
            EventTypeName = eventTypeName,
            ImplementsIDispatchableEvent = implementsIDispatchableEvent,
            EventIdTypeName = eventIdTypeName,
            ImplementsIStateMachineState = implementsIStateMachineState,
            StateIdTypeName = stateIdTypeName,
            States = states,
            Triggers = triggers,
            Location = Location.None,
            IsValid = missingModifiers.Count == 0
        };

        return (result, diagnostics.ToImmutable());
    }

    private static ClassModifiers ExtractModifiers(ClassDeclarationSyntax classDeclaration)
    {
        var modifiers = ClassModifiers.None;

        foreach (var modifier in classDeclaration.Modifiers)
        {
            switch (modifier.Kind())
            {
                case SyntaxKind.PublicKeyword:
                    modifiers |= ClassModifiers.Public;
                    break;
                case SyntaxKind.PartialKeyword:
                    modifiers |= ClassModifiers.Partial;
                    break;
                case SyntaxKind.StaticKeyword:
                    modifiers |= ClassModifiers.Static;
                    break;
            }
        }

        return modifiers;
    }

    private static string ExtractNamespace(ClassDeclarationSyntax classDeclaration)
    {
        // Check for file-scoped namespace
        var root = classDeclaration.SyntaxTree.GetRoot();
        var fileScopedNs = root.DescendantNodes().OfType<FileScopedNamespaceDeclarationSyntax>().FirstOrDefault();
        if (fileScopedNs != null)
            return fileScopedNs.Name.ToString();

        // Check for block-scoped namespace
        var blockNs = classDeclaration.Ancestors().OfType<NamespaceDeclarationSyntax>().FirstOrDefault();
        if (blockNs != null)
            return blockNs.Name.ToString();

        return "global";
    }

    /// <summary>
    /// Infers TState and TEvent from the first [Transition] handler method's parameters.
    /// TState = first parameter type, TEvent = second parameter type.
    /// Returns ("Unknown", "Unknown") if no transition handler is found.
    /// Uses a two-pass approach: first tries semantic resolution, then falls back to syntax-based
    /// attribute name matching to handle cases where the attributes assembly isn't yet resolved.
    /// </summary>
    private static (string StateTypeName, string EventTypeName) InferTypesFromHandlers(
        ClassDeclarationSyntax classDeclaration,
        SemanticModel semanticModel)
    {
        foreach (var member in classDeclaration.Members)
        {
            if (member is not MethodDeclarationSyntax method)
                continue;

            foreach (var attributeList in method.AttributeLists)
            {
                foreach (var attribute in attributeList.Attributes)
                {
                    if (!IsTransitionAttribute(attribute, semanticModel))
                        continue;

                    // Found a [Transition] handler — extract parameter types
                    var parameters = method.ParameterList.Parameters;
                    if (parameters.Count >= 2)
                    {
                        var stateParamType = parameters[0].Type;
                        var eventParamType = parameters[1].Type;

                        if (stateParamType != null && eventParamType != null)
                        {
                            var stateTypeInfo = semanticModel.GetTypeInfo(stateParamType);
                            var eventTypeInfo = semanticModel.GetTypeInfo(eventParamType);

                            var stateTypeName = stateTypeInfo.Type?.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)
                                                ?? stateParamType.ToString();
                            var eventTypeName = eventTypeInfo.Type?.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)
                                                ?? eventParamType.ToString();

                            return (stateTypeName, eventTypeName);
                        }
                    }

                    // Handler found but has fewer than 2 parameters — use Unknown
                    return ("Unknown", "Unknown");
                }
            }
        }

        // No transition handler found
        return ("Unknown", "Unknown");
    }

    /// <summary>
    /// Determines whether an attribute is a [Transition] attribute.
    /// First attempts semantic resolution for accuracy, then falls back to syntax-based
    /// name matching when the semantic model cannot resolve the symbol (e.g., when the
    /// attributes assembly reference isn't yet available during compilation).
    /// </summary>
    private static bool IsTransitionAttribute(AttributeSyntax attribute, SemanticModel semanticModel)
    {
        // Try semantic resolution first (most accurate)
        var symbolInfo = semanticModel.GetSymbolInfo(attribute);
        var attrSymbol = symbolInfo.Symbol ?? symbolInfo.CandidateSymbols.FirstOrDefault();

        if (attrSymbol is not null)
        {
            var containingType = attrSymbol.ContainingType;
            return containingType?.Name == "TransitionAttribute" &&
                   containingType.ContainingNamespace?.ToString() == "StateMachineSrcGen";
        }

        // Fallback: syntax-based name matching when semantic resolution fails
        var name = attribute.Name switch
        {
            SimpleNameSyntax simple => simple.Identifier.Text,
            QualifiedNameSyntax qualified => qualified.Right.Identifier.Text,
            _ => string.Empty
        };

        return name == "Transition" || name == "TransitionAttribute";
    }

    /// <summary>
    /// Detects whether the inferred event type implements IDispatchableEvent&lt;TEventId&gt;.
    /// Looks up the event type symbol from the semantic model and checks its interfaces.
    /// </summary>
    private static (bool Implements, string? EventIdTypeName) DetectIDispatchableEvent(
        INamedTypeSymbol? classSymbol,
        string eventTypeName,
        SemanticModel semanticModel,
        ClassDeclarationSyntax classDeclaration)
    {
        if (eventTypeName == "Unknown")
            return (false, null);

        // Try to find the event type symbol by looking at the first transition handler's second parameter
        INamedTypeSymbol? eventTypeSymbol = null;

        foreach (var member in classDeclaration.Members)
        {
            if (member is not MethodDeclarationSyntax method)
                continue;

            foreach (var attributeList in method.AttributeLists)
            {
                foreach (var attribute in attributeList.Attributes)
                {
                    if (!IsTransitionAttribute(attribute, semanticModel))
                        continue;

                    // Found a [Transition] handler — get the event type symbol
                    var parameters = method.ParameterList.Parameters;
                    if (parameters.Count >= 2 && parameters[1].Type != null)
                    {
                        var eventTypeInfo = semanticModel.GetTypeInfo(parameters[1].Type!);
                        eventTypeSymbol = eventTypeInfo.Type as INamedTypeSymbol;
                    }

                    goto FoundHandler;
                }
            }
        }

        FoundHandler:

        if (eventTypeSymbol is null)
            return (false, null);

        // Check if the event type implements IDispatchableEvent<TEventId>
        foreach (var iface in eventTypeSymbol.AllInterfaces)
        {
            if (iface.Name == "IDispatchableEvent" &&
                iface.ContainingNamespace?.ToString() == "StateMachineSrcGen" &&
                iface.TypeArguments.Length == 1)
            {
                var eventIdType = iface.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
                return (true, eventIdType);
            }
        }

        return (false, null);
    }

    /// <summary>
    /// Detects whether the inferred state type implements IStateMachineState&lt;TStateId&gt;.
    /// Looks up the state type symbol from the first transition handler's first parameter.
    /// </summary>
    private static (bool Implements, string? StateIdTypeName) DetectIStateMachineState(
        string stateTypeName,
        SemanticModel semanticModel,
        ClassDeclarationSyntax classDeclaration)
    {
        if (stateTypeName == "Unknown")
            return (false, null);

        // For primitive types like string, no interface detection needed
        if (stateTypeName == "string" || stateTypeName == "String" || stateTypeName == "System.String")
            return (false, null);

        // Try to find the state type symbol by looking at the first transition handler's first parameter
        INamedTypeSymbol? stateTypeSymbol = null;

        foreach (var member in classDeclaration.Members)
        {
            if (member is not MethodDeclarationSyntax method)
                continue;

            foreach (var attributeList in method.AttributeLists)
            {
                foreach (var attribute in attributeList.Attributes)
                {
                    if (!IsTransitionAttribute(attribute, semanticModel))
                        continue;

                    // Found a [Transition] handler — get the state type symbol
                    var parameters = method.ParameterList.Parameters;
                    if (parameters.Count >= 1 && parameters[0].Type != null)
                    {
                        var stateTypeInfo = semanticModel.GetTypeInfo(parameters[0].Type!);
                        stateTypeSymbol = stateTypeInfo.Type as INamedTypeSymbol;
                    }

                    goto FoundStateHandler;
                }
            }
        }

        FoundStateHandler:

        if (stateTypeSymbol is null)
            return (false, null);

        // Check if the state type implements IStateMachineState<TStateId>
        foreach (var iface in stateTypeSymbol.AllInterfaces)
        {
            if (iface.Name == "IStateMachineState" &&
                iface.ContainingNamespace?.ToString() == "StateMachineSrcGen" &&
                iface.TypeArguments.Length == 1)
            {
                var stateIdType = iface.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
                return (true, stateIdType);
            }
        }

        return (false, null);
    }

    private static ImmutableArray<ParsedState> ExtractStates(
        ClassDeclarationSyntax classDeclaration,
        SemanticModel semanticModel)
    {
        var states = ImmutableArray.CreateBuilder<ParsedState>();

        foreach (var attributeList in classDeclaration.AttributeLists)
        {
            foreach (var attribute in attributeList.Attributes)
            {
                var symbolInfo = semanticModel.GetSymbolInfo(attribute);
                var attrSymbol = symbolInfo.Symbol ?? symbolInfo.CandidateSymbols.FirstOrDefault();

                if (attrSymbol is null)
                    continue;

                var containingType = attrSymbol.ContainingType;
                if (containingType?.Name != "StateAttribute" ||
                    containingType.ContainingNamespace?.ToString() != "StateMachineSrcGen")
                    continue;

                // Extract the Name parameter (first constructor argument)
                var args = attribute.ArgumentList?.Arguments;
                if (args is null || args.Value.Count == 0)
                    continue;

                var nameArg = args.Value[0];
                var nameValue = semanticModel.GetConstantValue(nameArg.Expression);
                if (!nameValue.HasValue || nameValue.Value is not string name)
                    continue;

                // Extract IsInitial named argument
                var isInitial = false;
                foreach (var arg in args.Value)
                {
                    if (arg.NameEquals?.Name.Identifier.Text == "IsInitial")
                    {
                        var isInitialValue = semanticModel.GetConstantValue(arg.Expression);
                        if (isInitialValue.HasValue && isInitialValue.Value is bool b)
                            isInitial = b;
                    }
                }

                states.Add(new ParsedState
                {
                    Name = name,
                    IsInitial = isInitial,
                    Location = Location.None
                });
            }
        }

        return states.ToImmutable();
    }

    private static ImmutableArray<ParsedTrigger> ExtractTriggers(
        ClassDeclarationSyntax classDeclaration,
        SemanticModel semanticModel)
    {
        var triggers = ImmutableArray.CreateBuilder<ParsedTrigger>();

        foreach (var attributeList in classDeclaration.AttributeLists)
        {
            foreach (var attribute in attributeList.Attributes)
            {
                var symbolInfo = semanticModel.GetSymbolInfo(attribute);
                var attrSymbol = symbolInfo.Symbol ?? symbolInfo.CandidateSymbols.FirstOrDefault();

                if (attrSymbol is null)
                    continue;

                var containingType = attrSymbol.ContainingType;
                if (containingType?.Name != "TriggerAttribute" ||
                    containingType.ContainingNamespace?.ToString() != "StateMachineSrcGen")
                    continue;

                // Extract the Name parameter (first constructor argument)
                var args = attribute.ArgumentList?.Arguments;
                if (args is null || args.Value.Count == 0)
                    continue;

                var nameArg = args.Value[0];
                var nameValue = semanticModel.GetConstantValue(nameArg.Expression);
                if (!nameValue.HasValue || nameValue.Value is not string name)
                    continue;

                triggers.Add(new ParsedTrigger
                {
                    Name = name,
                    Location = Location.None
                });
            }
        }

        return triggers.ToImmutable();
    }
}
