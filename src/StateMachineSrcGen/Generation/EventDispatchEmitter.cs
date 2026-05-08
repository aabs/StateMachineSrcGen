using System.Linq;
using System.Text;

namespace StateMachineSrcGen.Generation;

/// <summary>
/// Generates switch/case block over @event.GetEventId() routing to per-handler transition logic.
/// Uses enum-based case labels and state comparisons (no string literals).
/// </summary>
internal static class EventDispatchEmitter
{
    /// <summary>
    /// Emits the event dispatch switch statement that routes based on eventId and current state.
    /// Uses enum case labels (e.g., case OrderEventId.Confirm:) and state comparisons
    /// using currentState.GetStateId() == StateId.Pending pattern.
    /// </summary>
    public static string Emit(ValidatedStateMachine input)
    {
        var sb = new StringBuilder();
        var transitions = input.Transitions.ToList();

        if (transitions.Count == 0)
        {
            return string.Empty;
        }

        // Group transitions by EventId (trigger name)
        var groupedByEventId = transitions
            .OrderBy(t => t.DeclarationOrder)
            .GroupBy(t => t.EventId)
            .OrderBy(g => g.Min(t => t.DeclarationOrder))
            .ToList();

        sb.AppendLine("                switch (eventId)");
        sb.AppendLine("                {");

        foreach (var eventGroup in groupedByEventId)
        {
            var triggerName = eventGroup.Key;
            // Use enum case label: case EventIdEnumType.TriggerName:
            var caseLabel = $"{input.EventIdEnumTypeName}.{triggerName}";
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

                // Generate enum-based state comparison
                var stateComparison = $"currentState.GetStateId() == {input.StateIdEnumTypeName}.{fromState}";
                sb.AppendLine($"                        if ({stateComparison})");
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
}
