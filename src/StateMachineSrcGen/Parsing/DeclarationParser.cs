using System;
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
/// inferred state/event types from handler signatures, and class-level attributes
/// ([InitialState], [TerminalState]). Resolves enum types from cast expressions
/// in attributes and enumerates their members to build state/event sets.
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
        public ImmutableArray<ParsedEvent> Events { get; init; }
        public ImmutableArray<ParsedTrigger> Triggers { get; init; }
        public string? InitialStateName { get; init; }
        public ImmutableArray<string> TerminalStateNames { get; init; }
        public Location Location { get; init; }
        public bool IsValid { get; init; }

        /// <summary>The resolved state ID enum type symbol (for int→enum resolution in HandlerParser).</summary>
        public INamedTypeSymbol? StateIdEnumSymbol { get; init; }

        /// <summary>The resolved event ID enum type symbol (for int→enum resolution in HandlerParser).</summary>
        public INamedTypeSymbol? EventIdEnumSymbol { get; init; }
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

        // Try to resolve enum types from attribute cast expressions if interface detection found them
        INamedTypeSymbol? stateIdEnumSymbol = null;
        INamedTypeSymbol? eventIdEnumSymbol = null;

        if (stateIdTypeName != null)
        {
            stateIdEnumSymbol = ResolveEnumTypeFromAttributes(classDeclaration, semanticModel, "InitialState", "Transition", isStateEnum: true);
        }

        if (eventIdTypeName != null)
        {
            eventIdEnumSymbol = ResolveEnumTypeFromAttributes(classDeclaration, semanticModel, "InitialState", "Transition", isStateEnum: false);
        }

        // If we couldn't resolve from interface detection, try resolving directly from cast expressions
        if (stateIdEnumSymbol == null)
        {
            stateIdEnumSymbol = ResolveEnumTypeFromAttributes(classDeclaration, semanticModel, "InitialState", "Transition", isStateEnum: true);
            if (stateIdEnumSymbol != null)
            {
                stateIdTypeName = stateIdEnumSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
                implementsIStateMachineState = true;
            }
        }

        if (eventIdEnumSymbol == null)
        {
            eventIdEnumSymbol = ResolveEnumTypeFromAttributes(classDeclaration, semanticModel, "InitialState", "Transition", isStateEnum: false);
            if (eventIdEnumSymbol != null)
            {
                eventIdTypeName = eventIdEnumSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
                implementsIDispatchableEvent = true;
            }
        }

        // Enumerate enum members to build state/event sets
        var states = EnumerateEnumMembers(stateIdEnumSymbol);
        var events = EnumerateEnumEvents(eventIdEnumSymbol);

        // SMSG026: Validate [Flags] absence on state/event ID enums
        if (stateIdEnumSymbol != null && HasFlagsAttribute(stateIdEnumSymbol))
        {
            diagnostics.Add(Diagnostic.Create(
                DiagnosticDescriptors.FlagsEnumNotSupported,
                location,
                stateIdEnumSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
        }

        if (eventIdEnumSymbol != null && HasFlagsAttribute(eventIdEnumSymbol))
        {
            diagnostics.Add(Diagnostic.Create(
                DiagnosticDescriptors.FlagsEnumNotSupported,
                location,
                eventIdEnumSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
        }

        // SMSG018: Validate [InitialState] value resolves to a valid enum member
        var initialStateIntValue = ExtractInitialStateIntValue(classDeclaration, semanticModel);
        if (initialStateIntValue.HasValue && stateIdEnumSymbol != null)
        {
            var resolvedName = ResolveEnumMemberName(stateIdEnumSymbol, initialStateIntValue.Value);
            if (resolvedName == null)
            {
                var validMembers = string.Join(", ",
                    stateIdEnumSymbol.GetMembers().OfType<IFieldSymbol>()
                        .Where(f => f.HasConstantValue)
                        .Select(f => f.Name));
                diagnostics.Add(Diagnostic.Create(
                    DiagnosticDescriptors.InvalidEnumValue,
                    location,
                    initialStateIntValue.Value.ToString(),
                    stateIdEnumSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                    validMembers));
            }
        }

        // Parse [InitialState] and [TerminalState] class-level attributes
        var initialStateName = ExtractInitialState(classDeclaration, semanticModel, stateIdEnumSymbol);
        var terminalStateNames = ExtractTerminalStates(classDeclaration, semanticModel, stateIdEnumSymbol);

        // Mark the initial state in the states array
        if (initialStateName != null && states.Length > 0)
        {
            var builder = ImmutableArray.CreateBuilder<ParsedState>(states.Length);
            foreach (var state in states)
            {
                builder.Add(new ParsedState
                {
                    Name = state.Name,
                    IsInitial = state.Name == initialStateName,
                    Location = state.Location
                });
            }
            states = builder.ToImmutable();
        }

        // Fall back to old-style [State] attributes if no enum-based states found
        if (states.Length == 0)
        {
            states = ExtractLegacyStates(classDeclaration, semanticModel);
        }

        var triggers = ExtractTriggers(classDeclaration, semanticModel);

        // Use Location.None in the result model for deterministic equality across compilations.
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
            Events = events,
            Triggers = triggers,
            InitialStateName = initialStateName,
            TerminalStateNames = terminalStateNames,
            Location = Location.None,
            IsValid = missingModifiers.Count == 0,
            StateIdEnumSymbol = stateIdEnumSymbol,
            EventIdEnumSymbol = eventIdEnumSymbol
        };

        return (result, diagnostics.ToImmutable());
    }

    // ─── Enum Type Resolution ───────────────────────────────────────────────────

    /// <summary>
    /// Checks if an enum type has the [Flags] attribute.
    /// </summary>
    private static bool HasFlagsAttribute(INamedTypeSymbol enumType)
    {
        return enumType.GetAttributes().Any(a =>
            a.AttributeClass?.Name == "FlagsAttribute" &&
            a.AttributeClass.ContainingNamespace?.ToString() == "System");
    }

    /// <summary>
    /// Extracts the raw integer value from [InitialState] attribute for validation.
    /// Returns null if no [InitialState] attribute is found or value cannot be resolved.
    /// </summary>
    private static int? ExtractInitialStateIntValue(
        ClassDeclarationSyntax classDeclaration,
        SemanticModel semanticModel)
    {
        foreach (var attributeList in classDeclaration.AttributeLists)
        {
            foreach (var attribute in attributeList.Attributes)
            {
                if (!IsAttributeNamed(attribute, semanticModel, "InitialState", "InitialStateAttribute"))
                    continue;

                var args = attribute.ArgumentList?.Arguments;
                if (args == null || args.Value.Count == 0)
                    continue;

                var constantValue = semanticModel.GetConstantValue(args.Value[0].Expression);
                if (constantValue.HasValue && constantValue.Value is int intValue)
                {
                    return intValue;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Resolves an enum type from cast expressions in [InitialState] or [Transition] attributes.
    /// For state enums: looks at [InitialState((int)StateId.X)] or [Transition] From/To args.
    /// For event enums: looks at [Transition] Trigger arg (3rd positional argument).
    /// </summary>
    private static INamedTypeSymbol? ResolveEnumTypeFromAttributes(
        ClassDeclarationSyntax classDeclaration,
        SemanticModel semanticModel,
        string initialStateAttrName,
        string transitionAttrName,
        bool isStateEnum)
    {
        // First try [InitialState] for state enum
        if (isStateEnum)
        {
            foreach (var attributeList in classDeclaration.AttributeLists)
            {
                foreach (var attribute in attributeList.Attributes)
                {
                    if (!IsAttributeNamed(attribute, semanticModel, "InitialState", "InitialStateAttribute"))
                        continue;

                    var args = attribute.ArgumentList?.Arguments;
                    if (args == null || args.Value.Count == 0)
                        continue;

                    var enumType = ResolveEnumTypeFromCastExpression(args.Value[0].Expression, semanticModel);
                    if (enumType != null)
                        return enumType;
                }
            }
        }

        // Try [Transition] attributes on methods
        foreach (var member in classDeclaration.Members)
        {
            if (member is not MethodDeclarationSyntax method)
                continue;

            foreach (var attributeList in method.AttributeLists)
            {
                foreach (var attribute in attributeList.Attributes)
                {
                    if (!IsAttributeNamed(attribute, semanticModel, "Transition", "TransitionAttribute"))
                        continue;

                    var args = attribute.ArgumentList?.Arguments;
                    if (args == null || args.Value.Count < 3)
                        continue;

                    if (isStateEnum)
                    {
                        // From (arg 0) or To (arg 1) for state enum
                        var enumType = ResolveEnumTypeFromCastExpression(args.Value[0].Expression, semanticModel);
                        if (enumType != null)
                            return enumType;

                        enumType = ResolveEnumTypeFromCastExpression(args.Value[1].Expression, semanticModel);
                        if (enumType != null)
                            return enumType;
                    }
                    else
                    {
                        // Trigger (arg 2) for event enum
                        var enumType = ResolveEnumTypeFromCastExpression(args.Value[2].Expression, semanticModel);
                        if (enumType != null)
                            return enumType;
                    }
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Resolves the enum type from a cast expression like (int)EnumType.Member.
    /// </summary>
    private static INamedTypeSymbol? ResolveEnumTypeFromCastExpression(
        ExpressionSyntax expression,
        SemanticModel semanticModel)
    {
        // Pattern: (int)EnumType.Member
        if (expression is CastExpressionSyntax castExpr)
        {
            var innerExpr = castExpr.Expression;

            // The inner expression should be EnumType.Member (a MemberAccessExpression)
            if (innerExpr is MemberAccessExpressionSyntax memberAccess)
            {
                var typeInfo = semanticModel.GetTypeInfo(memberAccess.Expression);
                if (typeInfo.Type is INamedTypeSymbol namedType && namedType.TypeKind == TypeKind.Enum)
                {
                    return namedType;
                }

                // Try getting the symbol of the member access
                var symbolInfo = semanticModel.GetSymbolInfo(memberAccess);
                var symbol = symbolInfo.Symbol ?? symbolInfo.CandidateSymbols.FirstOrDefault();
                if (symbol is IFieldSymbol fieldSymbol && fieldSymbol.ContainingType?.TypeKind == TypeKind.Enum)
                {
                    return fieldSymbol.ContainingType;
                }
            }

            // Also try resolving the inner expression directly
            var innerTypeInfo = semanticModel.GetTypeInfo(innerExpr);
            if (innerTypeInfo.Type is INamedTypeSymbol innerNamedType && innerNamedType.TypeKind == TypeKind.Enum)
            {
                return innerNamedType;
            }
        }

        return null;
    }

    // ─── Enum Member Enumeration ────────────────────────────────────────────────

    /// <summary>
    /// Enumerates all members of an enum type and returns them as ParsedState instances.
    /// </summary>
    private static ImmutableArray<ParsedState> EnumerateEnumMembers(INamedTypeSymbol? enumType)
    {
        if (enumType == null || enumType.TypeKind != TypeKind.Enum)
            return ImmutableArray<ParsedState>.Empty;

        var builder = ImmutableArray.CreateBuilder<ParsedState>();

        foreach (var member in enumType.GetMembers())
        {
            if (member is IFieldSymbol field && field.HasConstantValue)
            {
                builder.Add(new ParsedState
                {
                    Name = field.Name,
                    IsInitial = false,
                    Location = Location.None
                });
            }
        }

        return builder.ToImmutable();
    }

    /// <summary>
    /// Enumerates all members of an enum type and returns them as ParsedEvent instances.
    /// </summary>
    private static ImmutableArray<ParsedEvent> EnumerateEnumEvents(INamedTypeSymbol? enumType)
    {
        if (enumType == null || enumType.TypeKind != TypeKind.Enum)
            return ImmutableArray<ParsedEvent>.Empty;

        var builder = ImmutableArray.CreateBuilder<ParsedEvent>();

        foreach (var member in enumType.GetMembers())
        {
            if (member is IFieldSymbol field && field.HasConstantValue)
            {
                var intValue = Convert.ToInt32(field.ConstantValue);
                builder.Add(new ParsedEvent
                {
                    Name = field.Name,
                    IntValue = intValue,
                    Location = Location.None
                });
            }
        }

        return builder.ToImmutable();
    }

    // ─── InitialState / TerminalState Extraction ────────────────────────────────

    /// <summary>
    /// Extracts the initial state name from [InitialState] class-level attribute.
    /// Resolves the int value to an enum member name.
    /// </summary>
    private static string? ExtractInitialState(
        ClassDeclarationSyntax classDeclaration,
        SemanticModel semanticModel,
        INamedTypeSymbol? stateIdEnumSymbol)
    {
        foreach (var attributeList in classDeclaration.AttributeLists)
        {
            foreach (var attribute in attributeList.Attributes)
            {
                if (!IsAttributeNamed(attribute, semanticModel, "InitialState", "InitialStateAttribute"))
                    continue;

                var args = attribute.ArgumentList?.Arguments;
                if (args == null || args.Value.Count == 0)
                    continue;

                // Get the constant int value
                var constantValue = semanticModel.GetConstantValue(args.Value[0].Expression);
                if (constantValue.HasValue && constantValue.Value is int intValue)
                {
                    return ResolveEnumMemberName(stateIdEnumSymbol, intValue);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Extracts terminal state names from [TerminalState] class-level attributes.
    /// Resolves int values to enum member names.
    /// </summary>
    private static ImmutableArray<string> ExtractTerminalStates(
        ClassDeclarationSyntax classDeclaration,
        SemanticModel semanticModel,
        INamedTypeSymbol? stateIdEnumSymbol)
    {
        var builder = ImmutableArray.CreateBuilder<string>();

        foreach (var attributeList in classDeclaration.AttributeLists)
        {
            foreach (var attribute in attributeList.Attributes)
            {
                if (!IsAttributeNamed(attribute, semanticModel, "TerminalState", "TerminalStateAttribute"))
                    continue;

                var args = attribute.ArgumentList?.Arguments;
                if (args == null || args.Value.Count == 0)
                    continue;

                var constantValue = semanticModel.GetConstantValue(args.Value[0].Expression);
                if (constantValue.HasValue && constantValue.Value is int intValue)
                {
                    var name = ResolveEnumMemberName(stateIdEnumSymbol, intValue);
                    if (name != null)
                        builder.Add(name);
                }
            }
        }

        return builder.ToImmutable();
    }

    /// <summary>
    /// Resolves an integer value to an enum member name.
    /// </summary>
    private static string? ResolveEnumMemberName(INamedTypeSymbol? enumType, int value)
    {
        if (enumType == null)
            return null;

        foreach (var member in enumType.GetMembers())
        {
            if (member is IFieldSymbol field && field.HasConstantValue)
            {
                if (Convert.ToInt32(field.ConstantValue) == value)
                    return field.Name;
            }
        }

        return null;
    }

    // ─── Attribute Name Matching ────────────────────────────────────────────────

    /// <summary>
    /// Checks if an attribute matches a given name (with or without "Attribute" suffix).
    /// Uses semantic resolution first, then falls back to syntax-based matching.
    /// </summary>
    private static bool IsAttributeNamed(
        AttributeSyntax attribute,
        SemanticModel semanticModel,
        string shortName,
        string fullName)
    {
        // Try semantic resolution first
        var symbolInfo = semanticModel.GetSymbolInfo(attribute);
        var attrSymbol = symbolInfo.Symbol ?? symbolInfo.CandidateSymbols.FirstOrDefault();

        if (attrSymbol is not null)
        {
            var containingType = attrSymbol.ContainingType;
            return containingType?.Name == fullName &&
                   containingType.ContainingNamespace?.ToString() == "StateMachineSrcGen";
        }

        // Fallback: syntax-based name matching
        var name = attribute.Name switch
        {
            SimpleNameSyntax simple => simple.Identifier.Text,
            QualifiedNameSyntax qualified => qualified.Right.Identifier.Text,
            _ => string.Empty
        };

        return name == shortName || name == fullName;
    }

    // ─── Existing Methods (preserved) ───────────────────────────────────────────

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

                    // Found a [Transition] handler - extract parameter types
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

                    // Handler found but has fewer than 2 parameters - use Unknown
                    return ("Unknown", "Unknown");
                }
            }
        }

        // No transition handler found
        return ("Unknown", "Unknown");
    }

    /// <summary>
    /// Determines whether an attribute is a [Transition] attribute.
    /// </summary>
    private static bool IsTransitionAttribute(AttributeSyntax attribute, SemanticModel semanticModel)
    {
        return IsAttributeNamed(attribute, semanticModel, "Transition", "TransitionAttribute");
    }

    /// <summary>
    /// Detects whether the inferred event type implements IDispatchableEvent&lt;TEventId&gt;.
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
    /// </summary>
    private static (bool Implements, string? StateIdTypeName) DetectIStateMachineState(
        string stateTypeName,
        SemanticModel semanticModel,
        ClassDeclarationSyntax classDeclaration)
    {
        if (stateTypeName == "Unknown")
            return (false, null);

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

    /// <summary>
    /// Extracts legacy [State] attributes from the class declaration.
    /// Used as fallback when no enum-based states are found.
    /// </summary>
    private static ImmutableArray<ParsedState> ExtractLegacyStates(
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

                var args = attribute.ArgumentList?.Arguments;
                if (args is null || args.Value.Count == 0)
                    continue;

                var nameArg = args.Value[0];
                var nameValue = semanticModel.GetConstantValue(nameArg.Expression);
                if (!nameValue.HasValue || nameValue.Value is not string name)
                    continue;

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
