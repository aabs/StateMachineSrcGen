using StateMachineSrcGen;

namespace StateMachineSrcGen.Playground;

/// <summary>
/// Rich state object for the order state machine.
/// Implements IStateMachineState&lt;string&gt; so the generated dispatch logic
/// uses GetStateId() for transition comparisons, while the full object
/// (including Items) is what gets persisted and passed to handlers.
/// </summary>
public record OrderState(string Status, List<OrderItem> Items) : IStateMachineState<string>
{
    public string GetStateId() => Status;
}

/// <summary>
/// A single line item in an order.
/// </summary>
public record OrderItem(string ProductName, int Quantity, decimal UnitPrice);
