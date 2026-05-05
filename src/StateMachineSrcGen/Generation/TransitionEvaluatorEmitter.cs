using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace StateMachineSrcGen.Generation;

/// <summary>
/// Generates guard evaluation in declaration order, first-match selection logic,
/// and NotHandled fallthrough.
/// </summary>
internal static class TransitionEvaluatorEmitter
{
    /// <summary>
    /// Emits the transition evaluation logic for a set of transitions sharing the same (FromState, EventId).
    /// Guards are evaluated in declaration order; first match wins.
    /// </summary>
    public static string Emit(List<ValidatedTransition> transitions, ValidatedStateMachine input)
    {
        var sb = new StringBuilder();

        foreach (var transition in transitions.OrderBy(t => t.DeclarationOrder))
        {
            sb.Append(OrchestrationEmitter.Emit(transition, input));
        }

        return sb.ToString();
    }
}
