using System.Threading.Tasks;

namespace StateMachineSrcGen;

/// <summary>Main state machine interface.</summary>
public interface IStateMachine<TState, TEvent>
{
    /// <summary>
    /// Handles an incoming event by dispatching it to the appropriate transition handler.
    /// </summary>
    /// <param name="event">The event to process.</param>
    /// <returns>A <see cref="TransitionResult"/> indicating the outcome of the transition attempt.</returns>
    Task<TransitionResult> HandleAsync(TEvent @event);
}
