using StateMachineSrcGen;

namespace StateMachineSrcGen.Playground;

/// <summary>
/// Enum representing the valid states of the order state machine.
/// Each member is a discrete state the order can be in.
/// </summary>
public enum OrderStateId
{
    Pending,
    Confirmed,
    Shipped,
    Cancelled
}

/// <summary>
/// Rich state object for the order state machine.
/// Implements IStateMachineState&lt;OrderStateId&gt; so the generated dispatch logic
/// uses GetStateId() for transition comparisons, while the full object
/// (including Items and ItemCount) is what gets persisted and passed to handlers.
/// </summary>
public record OrderState(OrderStateId Id, List<OrderItem> Items) : IStateMachineState<OrderStateId>
{
    /// <summary>Gets the number of items in the order.</summary>
    public int ItemCount => Items.Count;

    public OrderStateId GetStateId() => Id;
}

/// <summary>
/// A single line item in an order.
/// </summary>
public record OrderItem(string ProductName, int Quantity, decimal UnitPrice);
