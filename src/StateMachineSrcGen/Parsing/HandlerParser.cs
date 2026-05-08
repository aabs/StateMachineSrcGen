using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using StateMachineSrcGen.Diagnostics;

namespace StateMachineSrcGen.Parsing;

/// <summary>
/// Extracts handler methods decorated with [Transition], [Guard], or [SideEffect] attributes.
/// Validates handler signatures and classifies handlers by kind.
/// </summary>
internal static class HandlerParser
{
    /// <summary>
    /// Parses all handler methods from a class declaration.
    /// Returns parsed handlers and any signature validation diagnostics.
    /// </summary>
    public static (ImmutableArray<ParsedHandler> Handlers, ImmutableArray<Diagnostic> Diagnostics) Parse(
        ClassDeclarationSyntax classDeclaration,
        SemanticModel semanticModel,
        string stateTypeName,
        string eventTypeName)
    {
        var handlers = ImmutableArray.CreateBuilder<ParsedHandler>();
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();

        foreach (var member in classDeclaration.Members)
        {
            if (member is not MethodDeclarationSyntax method)
                continue;

            foreach (var attributeList in method.AttributeLists)
            {
                foreach (var attribute in attributeList.Attributes)
                {
                    var (kind, isHandlerAttribute) = ClassifyAttribute(attribute, semanticModel);
                    if (!isHandlerAttribute)
                        continue;

                    // Extract attribute parameters
                    var (from, to, trigger, eventId) = ExtractAttributeParameters(attribute, semanticModel, kind);

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

                    // Only process the first handler attribute per method
                    goto NextMethod;
                }
            }

            NextMethod:;
        }

        return (handlers.ToImmutable(), diagnostics.ToImmutable());
    }

    private static (HandlerKind Kind, bool IsHandlerAttribute) ClassifyAttribute(
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

    private static (string? From, string? To, string? Trigger, string? EventId) ExtractAttributeParameters(
        AttributeSyntax attribute,
        SemanticModel semanticModel,
        HandlerKind kind)
    {
        var args = attribute.ArgumentList?.Arguments;
        if (args is null || args.Value.Count < 3)
            return (null, null, null, null);

        string? from = null;
        string? to = null;
        string? trigger = null;
        string? eventId = null;

        // First three positional arguments: from, to, trigger
        var fromValue = semanticModel.GetConstantValue(args.Value[0].Expression);
        if (fromValue.HasValue && fromValue.Value is string f)
            from = f;

        var toValue = semanticModel.GetConstantValue(args.Value[1].Expression);
        if (toValue.HasValue && toValue.Value is string t)
            to = t;

        var triggerValue = semanticModel.GetConstantValue(args.Value[2].Expression);
        if (triggerValue.HasValue && triggerValue.Value is string tr)
            trigger = tr;

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
