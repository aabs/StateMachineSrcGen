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
/// interface implementations, and class-level attributes ([State], [Trigger]).
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
        public bool ImplementsIStateMachine { get; init; }
        public bool ImplementsIStatePersistence { get; init; }
        public bool ImplementsIDispatchableEvent { get; init; }
        public string? EventIdTypeName { get; init; }
        public ImmutableArray<ParsedState> States { get; init; }
        public ImmutableArray<ParsedTrigger> Triggers { get; init; }
        public Location Location { get; init; }
        public bool IsValid { get; init; }
    }

    /// <summary>
    /// Parses the class declaration to extract modifiers, interfaces, states, and triggers.
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

        // Detect IStateMachine<TState, TEvent>
        var (implementsIStateMachine, stateTypeName, eventTypeName) = DetectIStateMachine(classSymbol);

        if (!implementsIStateMachine)
        {
            diagnostics.Add(Diagnostic.Create(
                DiagnosticDescriptors.MissingIStateMachineImplementation,
                location));
        }

        // Detect IStatePersistence<TState>
        var implementsIStatePersistence = DetectIStatePersistence(classSymbol);

        if (!implementsIStatePersistence)
        {
            diagnostics.Add(Diagnostic.Create(
                DiagnosticDescriptors.MissingIStatePersistenceImplementation,
                location));
        }

        // Detect IDispatchableEvent<TEventId> on the event type
        var (implementsIDispatchableEvent, eventIdTypeName) = DetectIDispatchableEvent(classSymbol);

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
            StateTypeName = stateTypeName ?? "Unknown",
            EventTypeName = eventTypeName ?? "Unknown",
            ImplementsIStateMachine = implementsIStateMachine,
            ImplementsIStatePersistence = implementsIStatePersistence,
            ImplementsIDispatchableEvent = implementsIDispatchableEvent,
            EventIdTypeName = eventIdTypeName,
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

    private static (bool Implements, string? StateTypeName, string? EventTypeName) DetectIStateMachine(
        INamedTypeSymbol? classSymbol)
    {
        if (classSymbol is null)
            return (false, null, null);

        foreach (var iface in classSymbol.AllInterfaces)
        {
            if (iface.Name == "IStateMachine" &&
                iface.ContainingNamespace?.ToString() == "StateMachineSrcGen" &&
                iface.TypeArguments.Length == 2)
            {
                var stateType = iface.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
                var eventType = iface.TypeArguments[1].ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
                return (true, stateType, eventType);
            }
        }

        return (false, null, null);
    }

    private static bool DetectIStatePersistence(INamedTypeSymbol? classSymbol)
    {
        if (classSymbol is null)
            return false;

        foreach (var iface in classSymbol.AllInterfaces)
        {
            if (iface.Name == "IStatePersistence" &&
                iface.ContainingNamespace?.ToString() == "StateMachineSrcGen" &&
                iface.TypeArguments.Length == 1)
            {
                return true;
            }
        }

        return false;
    }

    private static (bool Implements, string? EventIdTypeName) DetectIDispatchableEvent(
        INamedTypeSymbol? classSymbol)
    {
        if (classSymbol is null)
            return (false, null);

        // Find the event type symbol from IStateMachine's second type argument
        INamedTypeSymbol? eventTypeSymbol = null;
        foreach (var iface in classSymbol.AllInterfaces)
        {
            if (iface.Name == "IStateMachine" &&
                iface.ContainingNamespace?.ToString() == "StateMachineSrcGen" &&
                iface.TypeArguments.Length == 2)
            {
                eventTypeSymbol = iface.TypeArguments[1] as INamedTypeSymbol;
                break;
            }
        }

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
