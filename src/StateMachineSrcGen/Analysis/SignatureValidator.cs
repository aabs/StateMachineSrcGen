using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using StateMachineSrcGen.Diagnostics;

namespace StateMachineSrcGen.Analysis;

/// <summary>
/// Validates handler method signatures match expected patterns.
/// Covers transition handlers, guards, side effects, entry callbacks, and cleanup handlers.
/// </summary>
internal static class SignatureValidator
{
    /// <summary>
    /// Validates handler method signatures and returns any diagnostics.
    /// </summary>
    public static ImmutableArray<Diagnostic> Validate(ParsedStateMachine input)
    {
        var diagnostics = new List<Diagnostic>();

        foreach (var handler in input.Handlers)
        {
            // Skip entry callbacks and cleanup handlers - they are validated by EntryCallbackValidator
            if (handler.Kind == HandlerKind.EntryCallback || handler.Kind == HandlerKind.Cleanup)
                continue;

            var sig = handler.Signature;
            var issues = new List<string>();

            if (!sig.IsPublic)
                issues.Add("not public");
            if (!sig.IsStatic)
                issues.Add("not static");

            // Validate parameters: expect (TState, TEvent)
            var parameters = sig.Parameters.ToList();
            if (parameters.Count != 2)
            {
                issues.Add($"expected 2 parameters but found {parameters.Count}");
            }

            // Validate return type based on handler kind
            switch (handler.Kind)
            {
                case HandlerKind.Transition:
                    if (sig.ReturnType != input.StateTypeName)
                        issues.Add($"expected return type '{input.StateTypeName}' but found '{sig.ReturnType}'");
                    break;
                case HandlerKind.Guard:
                    if (sig.ReturnType != "bool" && sig.ReturnType != "Boolean" && sig.ReturnType != "System.Boolean")
                        issues.Add($"expected return type 'bool' but found '{sig.ReturnType}'");
                    break;
                case HandlerKind.SideEffect:
                    if (sig.ReturnType != "void")
                        issues.Add($"expected return type 'void' but found '{sig.ReturnType}'");
                    break;
            }

            if (issues.Count > 0)
            {
                var expectedSig = GetExpectedSignature(handler.Kind, input.StateTypeName, input.EventTypeName);
                diagnostics.Add(Diagnostic.Create(
                    DiagnosticDescriptors.InvalidHandlerSignature,
                    handler.Location,
                    handler.MethodName,
                    expectedSig,
                    string.Join("; ", issues)));
            }
        }

        return diagnostics.ToImmutableArray();
    }

    private static string GetExpectedSignature(HandlerKind kind, string stateType, string eventType)
    {
        return kind switch
        {
            HandlerKind.Transition => $"{stateType} MethodName({stateType} state, {eventType} @event)",
            HandlerKind.Guard => $"bool MethodName({stateType} state, {eventType} @event)",
            HandlerKind.SideEffect => $"void MethodName({stateType} state, {eventType} @event)",
            _ => "unknown"
        };
    }
}
