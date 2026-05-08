using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using StateMachineSrcGen.Diagnostics;

namespace StateMachineSrcGen.Analysis;

/// <summary>
/// Validates entry callbacks and cleanup handlers:
/// - At most one catch-all [OnEnter] (SMSG022)
/// - No duplicate targeted [OnEnter] for same state (SMSG023)
/// - At most one [OnTerminal] (SMSG021)
/// - Entry callback signatures (SMSG024)
/// - Cleanup handler signature (SMSG025)
/// </summary>
internal static class EntryCallbackValidator
{
    /// <summary>
    /// Validates entry callbacks and cleanup handlers, returning any diagnostics.
    /// </summary>
    public static ImmutableArray<Diagnostic> Validate(ParsedStateMachine input)
    {
        var diagnostics = new List<Diagnostic>();

        // SMSG022: Multiple catch-all [OnEnter] methods
        var catchAllCallbacks = input.EntryCallbacks.Where(cb => cb.IsCatchAll).ToList();
        if (catchAllCallbacks.Count > 1)
        {
            // Report on the second and subsequent catch-all callbacks
            foreach (var duplicate in catchAllCallbacks.Skip(1))
            {
                diagnostics.Add(Diagnostic.Create(
                    DiagnosticDescriptors.MultipleCatchAllOnEnter,
                    duplicate.Location));
            }
        }

        // SMSG023: Duplicate targeted [OnEnter] for same state
        var targetedCallbacks = input.EntryCallbacks
            .Where(cb => !cb.IsCatchAll && cb.TargetStateName != null)
            .GroupBy(cb => cb.TargetStateName!)
            .Where(g => g.Count() > 1);

        foreach (var group in targetedCallbacks)
        {
            foreach (var duplicate in group.Skip(1))
            {
                diagnostics.Add(Diagnostic.Create(
                    DiagnosticDescriptors.DuplicateTargetedOnEnter,
                    duplicate.Location,
                    group.Key));
            }
        }

        // SMSG021: Multiple [OnTerminal] cleanup handlers
        // Check both the CleanupHandler field and handlers with Kind = Cleanup
        var cleanupHandlers = input.Handlers
            .Where(h => h.Kind == HandlerKind.Cleanup)
            .ToList();

        if (cleanupHandlers.Count > 1)
        {
            foreach (var duplicate in cleanupHandlers.Skip(1))
            {
                diagnostics.Add(Diagnostic.Create(
                    DiagnosticDescriptors.MultipleCleanupHandlers,
                    duplicate.Location));
            }
        }

        // SMSG024: Invalid entry callback signatures
        foreach (var callback in input.EntryCallbacks)
        {
            var sig = callback.Signature;
            var parameters = sig.Parameters.ToList();

            if (callback.IsCatchAll)
            {
                // Catch-all: (TState, TEvent) → void
                var issues = new List<string>();
                if (parameters.Count != 2)
                    issues.Add($"expected 2 parameters but found {parameters.Count}");
                if (sig.ReturnType != "void")
                    issues.Add($"expected return type 'void' but found '{sig.ReturnType}'");

                if (issues.Count > 0)
                {
                    diagnostics.Add(Diagnostic.Create(
                        DiagnosticDescriptors.InvalidEntryCallbackSignature,
                        callback.Location,
                        callback.MethodName,
                        $"void {callback.MethodName}({input.StateTypeName} state, {input.EventTypeName} @event)"));
                }
            }
            else
            {
                // Targeted: (TState, TEvent) → TState
                var issues = new List<string>();
                if (parameters.Count != 2)
                    issues.Add($"expected 2 parameters but found {parameters.Count}");
                if (sig.ReturnType != input.StateTypeName)
                    issues.Add($"expected return type '{input.StateTypeName}' but found '{sig.ReturnType}'");

                if (issues.Count > 0)
                {
                    diagnostics.Add(Diagnostic.Create(
                        DiagnosticDescriptors.InvalidEntryCallbackSignature,
                        callback.Location,
                        callback.MethodName,
                        $"{input.StateTypeName} {callback.MethodName}({input.StateTypeName} state, {input.EventTypeName} @event)"));
                }
            }
        }

        // SMSG025: Invalid cleanup handler signature
        if (input.CleanupHandler != null)
        {
            var cleanupSig = input.CleanupHandler.Value.Signature;
            var cleanupParams = cleanupSig.Parameters.ToList();
            var hasIssues = false;

            if (cleanupParams.Count != 1)
                hasIssues = true;
            if (cleanupSig.ReturnType != "Task" &&
                cleanupSig.ReturnType != "System.Threading.Tasks.Task")
                hasIssues = true;

            if (hasIssues)
            {
                diagnostics.Add(Diagnostic.Create(
                    DiagnosticDescriptors.InvalidCleanupHandlerSignature,
                    input.CleanupHandler.Value.Location,
                    input.CleanupHandler.Value.MethodName));
            }
        }

        return diagnostics.ToImmutableArray();
    }
}
