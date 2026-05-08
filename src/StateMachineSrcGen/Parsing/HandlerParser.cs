using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using StateMachineSrcGen.Diagnostics;

namespace StateMachineSrcGen.Parsing;

/// <summary>
/// Extracts handler methods decorated with [Transition], [Guard], [SideEffect],
/// [OnEnter], or [OnTerminal] attributes.
/// Resolves integer attribute arguments back to enum member names using the
/// state/event ID enum type symbols from DeclarationParser.
/// </summary>
internal static class HandlerParser
{
    /// <summary>
    /// Result of parsing handler methods from a class declaration.
    /// </summary>
    internal readonly struct HandlerResult
    {
        public ImmutableArray<ParsedHandler> Handlers { get; init; }
        public ImmutableArray<ParsedEntryCallback> EntryCallbacks { get; init; }
        public ParsedCleanupHandler? CleanupHandler { get; init; }
    }

    /// <summary>
    /// Parses all handler methods from a class declaration.
    /// Returns parsed handlers, entry callbacks, cleanup handler, and any diagnostics.
    /// </summary>
    public static (HandlerResult Result, ImmutableArray<Diagnostic> Diagnostics) Parse(
        ClassDeclarationSyntax classDeclaration,
        SemanticModel semanticModel,
        string stateTypeName,
        string eventTypeName,
        INamedTypeSymbol? stateIdEnumSymbol,
        INamedTypeSymbol? eventIdEnumSymbol)
    {
        var handlers = ImmutableArray.CreateBuilder<ParsedHandler>();
        var entryCallbacks = ImmutableArray.CreateBuilder<ParsedEntryCallback>();
        ParsedCleanupHandler? cleanupHandler = null;
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();

        foreach (var member in classDeclaration.Members)
        {
            if (member is not MethodDeclarationSyntax method)
                continue;

            foreach (var attributeList in method.AttributeLists)
            {
                foreach (var attribute in attributeList.Attributes)
                {
                    // Check for [Transition], [Guard], [SideEffect]
                    var (kind, isHandlerAttribute) = ClassifyHandlerAttribute(attribute, semanticModel);
                    if (isHandlerAttribute)
                    {
                        // Extract attribute parameters with int-to-enum resolution
                        var (from, to, trigger, eventId) = ExtractHandlerAttributeParameters(
                            attribute, semanticModel, kind, stateIdEnumSymbol, eventIdEnumSymbol, diagnostics);

                        // Extract method signature
                        var signature = ExtractSignature(method, semanticModel);

                        // Validate signature based on handler kind
                        var signatureDiag = ValidateSignature(method, signature, kind, stateTypeName, eventTypeName);
                        if (signatureDiag != null)
                        {
                            diagnostics.Add(signatureDiag);
                        }

                        handlers.Add(new ParsedHandler
                        {
                            MethodName = method.Identifier.Text,
                            FromState = from ?? string.Empty,
                            ToState = to ?? string.Empty,
                            Trigger = trigger ?? string.Empty,
                            EventId = eventId,
                            Kind = kind,
                            Signature = signature,
                            Location = Location.None
                        });

                        goto NextMethod;
                    }

                    // Check for [OnEnter]
                    if (IsOnEnterAttribute(attribute, semanticModel))
                    {
                        var signature = ExtractSignature(method, semanticModel);
                        var (targetStateName, isCatchAll) = ExtractOnEnterParameters(
                            attribute, semanticModel, stateIdEnumSymbol);

                        entryCallbacks.Add(new ParsedEntryCallback
                        {
                            MethodName = method.Identifier.Text,
                            TargetStateName = targetStateName,
                            IsCatchAll = isCatchAll,
                            Signature = signature,
                            Location = Location.None
                        });

                        goto NextMethod;
                    }

                    // Check for [OnTerminal]
                    if (IsOnTerminalAttribute(attribute, semanticModel))
                    {
                        var signature = ExtractSignature(method, semanticModel);

                        cleanupHandler = new ParsedCleanupHandler
                        {
                            MethodName = method.Identifier.Text,
                            Signature = signature,
                            Location = Location.None
                        };

                        goto NextMethod;
                    }
                }
            }

            NextMethod:;
        }

        var result = new HandlerResult
        {
            Handlers = handlers.ToImmutable(),
            EntryCallbacks = entryCallbacks.ToImmutable(),
            CleanupHandler = cleanupHandler
        };

        return (result, diagnostics.ToImmutable());
    }

    /// <summary>
    /// Legacy overload for backward compatibility with existing callers.
    /// Delegates to the new overload with null enum symbols (falls back to string-based resolution).
    /// </summary>
    public static (ImmutableArray<ParsedHandler> Handlers, ImmutableArray<Diagnostic> Diagnostics) Parse(
        ClassDeclarationSyntax classDeclaration,
        SemanticModel semanticModel,
        string stateTypeName,
        string eventTypeName)
    {
        var (result, diagnostics) = Parse(
            classDeclaration, semanticModel, stateTypeName, eventTypeName, null, null);
        return (result.Handlers, diagnostics);
    }

    // ─── Attribute Classification ───────────────────────────────────────────────

    private static (HandlerKind Kind, bool IsHandlerAttribute) ClassifyHandlerAttribute(
        AttributeSyntax attribute,
        SemanticModel semanticModel)
    {
        // Try semantic resolution first (most accurate)
        var symbolInfo = semanticModel.GetSymbolInfo(attribute);
        var attrSymbol = symbolInfo.Symbol ?? symbolInfo.CandidateSymbols.FirstOrDefault();

        if (attrSymbol is not null)
        {
            var containingType = attrSymbol.ContainingType;
            if (containingType?.ContainingNamespace?.ToString() != "StateMachineSrcGen")
                return (default, false);

            return containingType.Name switch
            {
                "TransitionAttribute" => (HandlerKind.Transition, true),
                "GuardAttribute" => (HandlerKind.Guard, true),
                "SideEffectAttribute" => (HandlerKind.SideEffect, true),
                _ => (default, false)
            };
        }

        // Fallback: syntax-based name matching when semantic resolution fails
        var name = attribute.Name switch
        {
            SimpleNameSyntax simple => simple.Identifier.Text,
            QualifiedNameSyntax qualified => qualified.Right.Identifier.Text,
            _ => string.Empty
        };

        return name switch
        {
            "Transition" or "TransitionAttribute" => (HandlerKind.Transition, true),
            "Guard" or "GuardAttribute" => (HandlerKind.Guard, true),
            "SideEffect" or "SideEffectAttribute" => (HandlerKind.SideEffect, true),
            _ => (default, false)
        };
    }

    private static bool IsOnEnterAttribute(AttributeSyntax attribute, SemanticModel semanticModel)
    {
        return IsAttributeNamed(attribute, semanticModel, "OnEnter", "OnEnterAttribute");
    }

    private static bool IsOnTerminalAttribute(AttributeSyntax attribute, SemanticModel semanticModel)
    {
        return IsAttributeNamed(attribute, semanticModel, "OnTerminal", "OnTerminalAttribute");
    }

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

    // ─── Attribute Parameter Extraction ─────────────────────────────────────────

    /// <summary>
    /// Extracts handler attribute parameters, resolving int values to enum member names
    /// when enum type symbols are available. Emits SMSG018 diagnostics when int values
    /// don't resolve to valid enum members.
    /// </summary>
    private static (string? From, string? To, string? Trigger, string? EventId) ExtractHandlerAttributeParameters(
        AttributeSyntax attribute,
        SemanticModel semanticModel,
        HandlerKind kind,
        INamedTypeSymbol? stateIdEnumSymbol,
        INamedTypeSymbol? eventIdEnumSymbol,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        var args = attribute.ArgumentList?.Arguments;
        if (args is null || args.Value.Count < 3)
            return (null, null, null, null);

        string? from = null;
        string? to = null;
        string? trigger = null;
        string? eventId = null;
        var location = attribute.GetLocation();

        // First three positional arguments: from, to, trigger
        // Try int-to-enum resolution first, then fall back to string constants
        from = ResolveAttributeArgument(args.Value[0].Expression, semanticModel, stateIdEnumSymbol);
        to = ResolveAttributeArgument(args.Value[1].Expression, semanticModel, stateIdEnumSymbol);
        trigger = ResolveAttributeArgument(args.Value[2].Expression, semanticModel, eventIdEnumSymbol);

        // SMSG018: Emit diagnostic when int values don't resolve to enum members
        if (from == null && stateIdEnumSymbol != null)
        {
            var constVal = semanticModel.GetConstantValue(args.Value[0].Expression);
            if (constVal.HasValue && constVal.Value is int intVal)
            {
                var validMembers = string.Join(", ",
                    stateIdEnumSymbol.GetMembers().OfType<IFieldSymbol>()
                        .Where(f => f.HasConstantValue)
                        .Select(f => f.Name));
                diagnostics.Add(Diagnostic.Create(
                    DiagnosticDescriptors.InvalidEnumValue,
                    location,
                    intVal.ToString(),
                    stateIdEnumSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                    validMembers));
            }
        }

        if (to == null && stateIdEnumSymbol != null)
        {
            var constVal = semanticModel.GetConstantValue(args.Value[1].Expression);
            if (constVal.HasValue && constVal.Value is int intVal)
            {
                var validMembers = string.Join(", ",
                    stateIdEnumSymbol.GetMembers().OfType<IFieldSymbol>()
                        .Where(f => f.HasConstantValue)
                        .Select(f => f.Name));
                diagnostics.Add(Diagnostic.Create(
                    DiagnosticDescriptors.InvalidEnumValue,
                    location,
                    intVal.ToString(),
                    stateIdEnumSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                    validMembers));
            }
        }

        if (trigger == null && eventIdEnumSymbol != null)
        {
            var constVal = semanticModel.GetConstantValue(args.Value[2].Expression);
            if (constVal.HasValue && constVal.Value is int intVal)
            {
                var validMembers = string.Join(", ",
                    eventIdEnumSymbol.GetMembers().OfType<IFieldSymbol>()
                        .Where(f => f.HasConstantValue)
                        .Select(f => f.Name));
                diagnostics.Add(Diagnostic.Create(
                    DiagnosticDescriptors.InvalidEnumValue,
                    location,
                    intVal.ToString(),
                    eventIdEnumSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                    validMembers));
            }
        }

        // Check for EventId named argument (only on TransitionAttribute)
        if (kind == HandlerKind.Transition)
        {
            foreach (var arg in args.Value)
            {
                if (arg.NameEquals?.Name.Identifier.Text == "EventId")
                {
                    var eventIdValue = semanticModel.GetConstantValue(arg.Expression);
                    if (eventIdValue.HasValue && eventIdValue.Value != null)
                        eventId = eventIdValue.Value.ToString();
                }
            }
        }

        return (from, to, trigger, eventId);
    }

    /// <summary>
    /// Resolves an attribute argument expression to a string value.
    /// If the expression evaluates to an int and an enum type symbol is available,
    /// resolves the int back to the enum member name.
    /// Falls back to string constant resolution.
    /// </summary>
    private static string? ResolveAttributeArgument(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        INamedTypeSymbol? enumTypeSymbol)
    {
        var constantValue = semanticModel.GetConstantValue(expression);
        if (!constantValue.HasValue)
            return null;

        // If the constant is an int and we have an enum type, resolve to member name
        if (constantValue.Value is int intValue && enumTypeSymbol != null)
        {
            return ResolveEnumMemberName(enumTypeSymbol, intValue);
        }

        // If the constant is a string, use it directly (legacy path)
        if (constantValue.Value is string strValue)
        {
            return strValue;
        }

        return constantValue.Value?.ToString();
    }

    /// <summary>
    /// Resolves an integer value to an enum member name using the enum type symbol.
    /// </summary>
    private static string? ResolveEnumMemberName(INamedTypeSymbol enumType, int value)
    {
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

    /// <summary>
    /// Extracts [OnEnter] attribute parameters.
    /// Returns the target state name (resolved from int) and whether it's a catch-all.
    /// </summary>
    private static (string? TargetStateName, bool IsCatchAll) ExtractOnEnterParameters(
        AttributeSyntax attribute,
        SemanticModel semanticModel,
        INamedTypeSymbol? stateIdEnumSymbol)
    {
        var args = attribute.ArgumentList?.Arguments;

        // Parameterless [OnEnter] → catch-all
        if (args is null || args.Value.Count == 0)
        {
            return (null, true);
        }

        // [OnEnter(intValue)] → targeted
        var constantValue = semanticModel.GetConstantValue(args.Value[0].Expression);
        if (constantValue.HasValue && constantValue.Value is int intValue && stateIdEnumSymbol != null)
        {
            var stateName = ResolveEnumMemberName(stateIdEnumSymbol, intValue);
            return (stateName, false);
        }

        return (null, true);
    }

    // ─── Signature Extraction ───────────────────────────────────────────────────

    private static MethodSignature ExtractSignature(
        MethodDeclarationSyntax method,
        SemanticModel semanticModel)
    {
        var isPublic = method.Modifiers.Any(SyntaxKind.PublicKeyword);
        var isStatic = method.Modifiers.Any(SyntaxKind.StaticKeyword);

        var returnTypeInfo = semanticModel.GetTypeInfo(method.ReturnType);
        var returnType = returnTypeInfo.Type?.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat) ?? method.ReturnType.ToString();

        var parameters = ImmutableArray.CreateBuilder<ParameterInfo>();
        foreach (var param in method.ParameterList.Parameters)
        {
            var paramTypeInfo = semanticModel.GetTypeInfo(param.Type!);
            var paramTypeName = paramTypeInfo.Type?.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat) ?? param.Type?.ToString() ?? "Unknown";

            parameters.Add(new ParameterInfo
            {
                Name = param.Identifier.Text,
                TypeName = paramTypeName
            });
        }

        return new MethodSignature
        {
            IsPublic = isPublic,
            IsStatic = isStatic,
            ReturnType = returnType,
            Parameters = new EquatableArray<ParameterInfo>(parameters.ToImmutable())
        };
    }

    // ─── Signature Validation ───────────────────────────────────────────────────

    private static Diagnostic? ValidateSignature(
        MethodDeclarationSyntax method,
        MethodSignature signature,
        HandlerKind kind,
        string stateTypeName,
        string eventTypeName)
    {
        var location = method.Identifier.GetLocation();
        var methodName = method.Identifier.Text;

        switch (kind)
        {
            case HandlerKind.Transition:
                // Must be public static TState MethodName(TState state, TEvent @event)
                if (!signature.IsPublic || !signature.IsStatic)
                {
                    var actual = $"{(signature.IsPublic ? "public" : "non-public")} {(signature.IsStatic ? "static" : "instance")}";
                    return Diagnostic.Create(
                        DiagnosticDescriptors.InvalidHandlerSignature,
                        location,
                        methodName,
                        $"{stateTypeName} ({stateTypeName}, {eventTypeName})",
                        actual);
                }

                if (signature.ReturnType != stateTypeName)
                {
                    return Diagnostic.Create(
                        DiagnosticDescriptors.InvalidHandlerSignature,
                        location,
                        methodName,
                        $"{stateTypeName} ({stateTypeName}, {eventTypeName})",
                        $"return type '{signature.ReturnType}'");
                }

                // Check parameters
                var transParams = signature.Parameters.ToList();
                if (transParams.Count != 2 ||
                    transParams[0].TypeName != stateTypeName ||
                    transParams[1].TypeName != eventTypeName)
                {
                    var actualParams = string.Join(", ", transParams.Select(p => p.TypeName));
                    return Diagnostic.Create(
                        DiagnosticDescriptors.InvalidHandlerSignature,
                        location,
                        methodName,
                        $"{stateTypeName} ({stateTypeName}, {eventTypeName})",
                        $"parameters ({actualParams})");
                }
                break;

            case HandlerKind.Guard:
                // Must be public static bool MethodName(TState state, TEvent @event)
                if (!signature.IsPublic || !signature.IsStatic)
                {
                    var actual = $"{(signature.IsPublic ? "public" : "non-public")} {(signature.IsStatic ? "static" : "instance")}";
                    return Diagnostic.Create(
                        DiagnosticDescriptors.InvalidHandlerSignature,
                        location,
                        methodName,
                        $"bool ({stateTypeName}, {eventTypeName})",
                        actual);
                }

                if (signature.ReturnType != "bool")
                {
                    return Diagnostic.Create(
                        DiagnosticDescriptors.InvalidHandlerSignature,
                        location,
                        methodName,
                        $"bool ({stateTypeName}, {eventTypeName})",
                        $"return type '{signature.ReturnType}'");
                }

                var guardParams = signature.Parameters.ToList();
                if (guardParams.Count != 2 ||
                    guardParams[0].TypeName != stateTypeName ||
                    guardParams[1].TypeName != eventTypeName)
                {
                    var actualParams = string.Join(", ", guardParams.Select(p => p.TypeName));
                    return Diagnostic.Create(
                        DiagnosticDescriptors.InvalidHandlerSignature,
                        location,
                        methodName,
                        $"bool ({stateTypeName}, {eventTypeName})",
                        $"parameters ({actualParams})");
                }
                break;

            case HandlerKind.SideEffect:
                // Must be public static void MethodName(TState state, TEvent @event)
                if (!signature.IsPublic || !signature.IsStatic)
                {
                    var actual = $"{(signature.IsPublic ? "public" : "non-public")} {(signature.IsStatic ? "static" : "instance")}";
                    return Diagnostic.Create(
                        DiagnosticDescriptors.InvalidHandlerSignature,
                        location,
                        methodName,
                        $"void ({stateTypeName}, {eventTypeName})",
                        actual);
                }

                if (signature.ReturnType != "void")
                {
                    return Diagnostic.Create(
                        DiagnosticDescriptors.InvalidHandlerSignature,
                        location,
                        methodName,
                        $"void ({stateTypeName}, {eventTypeName})",
                        $"return type '{signature.ReturnType}'");
                }

                var sideEffectParams = signature.Parameters.ToList();
                if (sideEffectParams.Count != 2 ||
                    sideEffectParams[0].TypeName != stateTypeName ||
                    sideEffectParams[1].TypeName != eventTypeName)
                {
                    var actualParams = string.Join(", ", sideEffectParams.Select(p => p.TypeName));
                    return Diagnostic.Create(
                        DiagnosticDescriptors.InvalidHandlerSignature,
                        location,
                        methodName,
                        $"void ({stateTypeName}, {eventTypeName})",
                        $"parameters ({actualParams})");
                }
                break;
        }

        return null;
    }
}
