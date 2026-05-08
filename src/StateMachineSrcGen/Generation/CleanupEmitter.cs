using System.Text;

namespace StateMachineSrcGen.Generation;

/// <summary>
/// Emits async cleanup handler invocation for terminal-state transitions.
/// The cleanup handler is invoked after persist and before lock release.
/// </summary>
internal static class CleanupEmitter
{
    /// <summary>
    /// Emits the cleanup handler invocation for a terminal-state transition.
    /// Uses the pattern: await CleanupMethodName(newState).ConfigureAwait(false)
    /// </summary>
    /// <param name="input">The validated state machine model.</param>
    /// <param name="indent">The indentation prefix for generated lines.</param>
    /// <returns>Generated code for cleanup handler invocation, or empty string if no cleanup handler.</returns>
    public static string Emit(ValidatedStateMachine input, string indent)
    {
        if (input.CleanupHandlerMethodName == null)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"{indent}await {input.CleanupHandlerMethodName}(newState).ConfigureAwait(false);");
        return sb.ToString();
    }
}
