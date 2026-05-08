using StateMachineSrcGen;

namespace StateMachineSrcGen.Playground;

/// <summary>
/// Event type for the order state machine.
/// The Action property is used as the dispatch key via IDispatchableEvent.
/// </summary>
public record OrderEvent(string Action) : IDispatchableEvent<string>
{
    public string GetEventId() => Action;
}
