using System;

namespace StateMachineSrcGen;

/// <summary>
/// Interface that the user's event type must implement to support
/// automatic dispatch extraction. The generated code calls GetEventId()
/// to obtain the identifier used in the dispatch switch statement.
/// </summary>
public interface IDispatchableEvent<TEventId>
{
    /// <summary>
    /// Returns the event identifier used by the generated dispatch logic
    /// to route this event to the correct transition handler.
    /// </summary>
    /// <returns>The event identifier for dispatch routing.</returns>
    TEventId GetEventId();
}
