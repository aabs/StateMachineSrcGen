using System.Text;

namespace StateMachineSrcGen.Generation;

/// <summary>
/// Generates the orchestration protocol for each transition:
/// Non-terminal: guard → handler → targeted entry → catch-all entry → persist → side-effect
/// Terminal: guard → handler → targeted entry → catch-all entry → persist → cleanup (no side-effect)
/// Returns TransitionResult&lt;TState&gt;.Succeeded(newState) / .GuardRejected / .NoTransition
/// </summary>
internal static class OrchestrationEmitter
{
    /// <summary>
    /// Emits the orchestration logic for a single transition.
    /// </summary>
    public static string Emit(ValidatedTransition transition, ValidatedStateMachine input)
    {
        var sb = new StringBuilder();
        var indent = "                            ";

        // Guard evaluation (if guard exists)
        if (transition.GuardMethodName != null)
        {
            sb.AppendLine($"{indent}if (!{transition.GuardMethodName}(currentState, @event))");
            sb.AppendLine($"{indent}    return StateMachineSrcGen.TransitionResult<{input.StateTypeName}>.GuardRejected;");
            sb.AppendLine();
        }

        // Handler invocation — produces new state
        sb.AppendLine($"{indent}var newState = {transition.HandlerMethodName}(currentState, @event);");

        // Entry callbacks (targeted then catch-all)
        var entryCode = EntryCallbackEmitter.Emit(transition, input, indent);
        if (!string.IsNullOrEmpty(entryCode))
        {
            sb.Append(entryCode);
        }

        // Persist new state
        sb.AppendLine($"{indent}await _persistence.SaveAsync(newState).ConfigureAwait(false);");

        if (transition.IsTerminal)
        {
            // Terminal: cleanup (no side-effect)
            var cleanupCode = CleanupEmitter.Emit(input, indent);
            if (!string.IsNullOrEmpty(cleanupCode))
            {
                sb.Append(cleanupCode);
            }
        }
        else
        {
            // Non-terminal: side-effect (if exists)
            if (transition.SideEffectMethodName != null)
            {
                sb.AppendLine($"{indent}{transition.SideEffectMethodName}(newState, @event);");
            }
        }

        // Return success
        sb.AppendLine($"{indent}return StateMachineSrcGen.TransitionResult<{input.StateTypeName}>.Succeeded(newState);");

        return sb.ToString();
    }
}
