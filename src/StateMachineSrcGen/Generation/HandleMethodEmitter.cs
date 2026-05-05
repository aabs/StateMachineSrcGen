using System.Text;

namespace StateMachineSrcGen.Generation;

/// <summary>
/// Generates the HandleAsync method body with lock acquire, load, dispatch, save, release pattern.
/// </summary>
internal static class HandleMethodEmitter
{
    /// <summary>
    /// Emits the HandleAsync method for the state machine.
    /// </summary>
    public static string Emit(ValidatedStateMachine input)
    {
        var sb = new StringBuilder();

        sb.AppendLine("        /// <summary>Handles an incoming event by dispatching to the appropriate transition handler.</summary>");
        sb.AppendLine($"        public static async System.Threading.Tasks.Task<StateMachineSrcGen.TransitionResult> HandleAsync({input.EventTypeName} @event)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (!await _lock.AcquireAsync().ConfigureAwait(false))");
        sb.AppendLine("                return StateMachineSrcGen.TransitionResult.LockFailed;");
        sb.AppendLine("            try");
        sb.AppendLine("            {");
        sb.AppendLine("                var currentState = await _persistence.LoadAsync().ConfigureAwait(false);");
        sb.AppendLine("                var eventId = @event.GetEventId();");
        sb.AppendLine();

        // Emit the dispatch logic
        sb.Append(EventDispatchEmitter.Emit(input));

        sb.AppendLine();
        sb.AppendLine("                return StateMachineSrcGen.TransitionResult.NotHandled;");
        sb.AppendLine("            }");
        sb.AppendLine("            finally");
        sb.AppendLine("            {");
        sb.AppendLine("                await _lock.ReleaseAsync().ConfigureAwait(false);");
        sb.AppendLine("            }");
        sb.AppendLine("        }");

        return sb.ToString();
    }
}
