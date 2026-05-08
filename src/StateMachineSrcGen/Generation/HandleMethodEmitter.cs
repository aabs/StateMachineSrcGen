using System.Text;

namespace StateMachineSrcGen.Generation;

/// <summary>
/// Generates the HandleAsync method body with lock acquire, load, dispatch, save, release pattern.
/// Returns Task&lt;TransitionResult&lt;TState&gt;&gt; using enum-based dispatch.
/// </summary>
internal static class HandleMethodEmitter
{
    /// <summary>
    /// Emits the HandleAsync method for the state machine.
    /// Uses enum types for dispatch and returns TransitionResult&lt;TState&gt;.
    /// </summary>
    public static string Emit(ValidatedStateMachine input)
    {
        var sb = new StringBuilder();

        sb.AppendLine("        /// <summary>Handles an incoming event by dispatching to the appropriate transition handler.</summary>");
        sb.AppendLine($"        public static async System.Threading.Tasks.Task<StateMachineSrcGen.TransitionResult<{input.StateTypeName}>> HandleAsync({input.EventTypeName} @event)");
        sb.AppendLine("        {");
        sb.AppendLine("            await _lock.AcquireAsync().ConfigureAwait(false);");
        sb.AppendLine("            try");
        sb.AppendLine("            {");
        sb.AppendLine("                var currentState = await _persistence.LoadAsync().ConfigureAwait(false);");
        sb.AppendLine($"                var eventId = @event.GetEventId();");
        sb.AppendLine();

        // Emit the dispatch logic
        sb.Append(EventDispatchEmitter.Emit(input));

        sb.AppendLine();
        sb.AppendLine($"                return StateMachineSrcGen.TransitionResult<{input.StateTypeName}>.NoTransition;");
        sb.AppendLine("            }");
        sb.AppendLine("            finally");
        sb.AppendLine("            {");
        sb.AppendLine("                await _lock.ReleaseAsync().ConfigureAwait(false);");
        sb.AppendLine("            }");
        sb.AppendLine("        }");

        return sb.ToString();
    }
}
