using System.Linq;
using System.Text;

namespace StateMachineSrcGen.Generation;

/// <summary>
/// Emits targeted and catch-all entry callback invocations.
/// Targeted entry callbacks return TState (captured as new state).
/// Catch-all entry callbacks are void (observational, no capture).
/// </summary>
internal static class EntryCallbackEmitter
{
    /// <summary>
    /// Emits entry callback invocations for a transition targeting a specific state.
    /// First emits the targeted entry callback (if one exists for the target state),
    /// then emits the catch-all entry callback (if one exists).
    /// </summary>
    /// <param name="transition">The transition being processed.</param>
    /// <param name="input">The validated state machine model.</param>
    /// <param name="indent">The indentation prefix for generated lines.</param>
    /// <returns>Generated code for entry callback invocations.</returns>
    public static string Emit(ValidatedTransition transition, ValidatedStateMachine input, string indent)
    {
        var sb = new StringBuilder();

        var entryCallbacks = input.EntryCallbacks.ToList();
        if (entryCallbacks.Count == 0)
        {
            return string.Empty;
        }

        // Find targeted entry callback for the transition's target state
        var targetedCallback = entryCallbacks
            .FirstOrDefault(ec => !ec.IsCatchAll && ec.TargetStateName == transition.ToState);

        // Find catch-all entry callback
        var catchAllCallback = entryCallbacks
            .FirstOrDefault(ec => ec.IsCatchAll);

        // Emit targeted entry callback (returns TState, captures return value)
        if (targetedCallback.MethodName != null)
        {
            if (targetedCallback.ReturnsTState)
            {
                sb.AppendLine($"{indent}newState = {targetedCallback.MethodName}(newState, @event);");
            }
            else
            {
                sb.AppendLine($"{indent}{targetedCallback.MethodName}(newState, @event);");
            }
        }

        // Emit catch-all entry callback (void, observational - no return value capture)
        if (catchAllCallback.MethodName != null)
        {
            sb.AppendLine($"{indent}{catchAllCallback.MethodName}(newState, @event);");
        }

        return sb.ToString();
    }
}
