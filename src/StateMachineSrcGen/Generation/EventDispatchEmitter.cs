using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace StateMachineSrcGen.Generation;

/// <summary>
/// Generates switch/case block over @event.GetEventId() routing to per-handler transition logic.
/// </summary>
internal static class EventDispatchEmitter
{
    /// <summary>
    /// Emits the event dispatch switch statement that routes based on eventId and current state.
    /// </summary>
    public static string Emit(ValidatedStateMachine input)
    {
        var sb = new StringBuilder();
        var transitions = input.Transitions.ToList();

        if (transitions.Count == 0)
        {
            return string.Empty;
        }

        // Group transitions by EventId
        var groupedByEventId = transitions
            .OrderBy(t => t.DeclarationOrder)
            .GroupBy(t => t.EventId)
            .OrderBy(g => g.Min(t => t.DeclarationOrder))
            .ToList();

        sb.AppendLine("                switch (eventId)");
        sb.AppendLine("                {");

        foreach (var eventGroup in groupedByEventId)
        {
            var eventId = eventGroup.Key;
            var caseLabel = string.IsNullOrEmpty(eventId) ? "\"\"" : $"\"{EscapeString(eventId)}\"";
            sb.AppendLine($"                    case {caseLabel}:");
            sb.AppendLine("                    {");

            // Group by FromState within this eventId
            var groupedByState = eventGroup
                .OrderBy(t => t.DeclarationOrder)
                .GroupBy(t => t.FromState)
                .ToList();

            foreach (var stateGroup in groupedByState)
            {
                var fromState = stateGroup.Key;
                var stateTransitions = stateGroup.OrderBy(t => t.DeclarationOrder).ToList();

                sb.AppendLine($"                        if (currentState == \"{EscapeString(fromState)}\")");
                sb.AppendLine("                        {");

                sb.Append(TransitionEvaluatorEmitter.Emit(stateTransitions, input));

                sb.AppendLine("                        }");
            }

            sb.AppendLine("                        break;");
            sb.AppendLine("                    }");
        }

        sb.AppendLine("                }");

        return sb.ToString();
    }

    private static string EscapeString(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
