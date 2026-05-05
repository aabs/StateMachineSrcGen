using System.Text;

namespace StateMachineSrcGen.Generation;

/// <summary>
/// Generates the load→guard→action→save→sideeffect protocol with try/finally for lock release.
/// Generates exception propagation without state save on action failure.
/// Generates exception propagation after save on side effect failure.
/// </summary>
internal static class OrchestrationEmitter
{
    /// <summary>
    /// Emits the orchestration logic for a single transition:
    /// guard evaluation → action invocation → state save → side effect invocation.
    /// </summary>
    public static string Emit(ValidatedTransition transition, ValidatedStateMachine input)
    {
        var sb = new StringBuilder();
        var indent = "                            ";

        // Guard evaluation (if guard exists)
        if (transition.GuardMethodName != null)
        {
            sb.AppendLine($"{indent}if ({transition.GuardMethodName}(currentState, @event))");
            sb.AppendLine($"{indent}{{");
            EmitActionSaveSideEffect(sb, transition, input, indent + "    ");
            sb.AppendLine($"{indent}}}");
        }
        else
        {
            // No guard — always execute
            EmitActionSaveSideEffect(sb, transition, input, indent);
        }

        return sb.ToString();
    }

    private static void EmitActionSaveSideEffect(
        StringBuilder sb, ValidatedTransition transition, ValidatedStateMachine input, string indent)
    {
        // Action invocation — produces new state
        sb.AppendLine($"{indent}var newState = {transition.HandlerMethodName}(currentState, @event);");

        // Save new state to persistence
        sb.AppendLine($"{indent}await _persistence.SaveAsync(newState).ConfigureAwait(false);");

        // Side effect invocation (if side effect exists) — after save
        if (transition.SideEffectMethodName != null)
        {
            sb.AppendLine($"{indent}{transition.SideEffectMethodName}(newState, @event);");
        }

        // Return success
        sb.AppendLine($"{indent}return StateMachineSrcGen.TransitionResult.Success;");
    }
}
