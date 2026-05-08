using StateMachineSrcGen;

namespace StateMachineSrcGen.Playground;

/// <summary>
/// Enum representing the valid events/triggers for the order state machine.
/// </summary>
public enum OrderEventId
{
    Confirm,
    Ship,
    Cancel
}

/// <summary>
/// Event type for the order state machine.
/// The EventType property is used as the dispatch key via IDispatchableEvent.
/// </summary>
public record OrderEvent(OrderEventId EventType) : IDispatchableEvent<OrderEventId>
{
    public OrderEventId GetEventId() => EventType;
}
