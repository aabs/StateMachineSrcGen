using StateMachineSrcGen;

namespace StateMachineSrcGen.Playground;

[State("Pending", IsInitial = true)]
[State("Confirmed")]
[State("Shipped")]
[Trigger("Confirm")]
[Trigger("Ship")]
public static partial class OrderMachine
{
    [Transition("Pending", "Confirmed", "Confirm", EventId = "confirm")]
    public static OrderState HandleConfirm(OrderState state, OrderEvent @event)
        => state with { Status = "Confirmed" };

    [Transition("Confirmed", "Shipped", "Ship", EventId = "ship")]
    public static OrderState HandleShip(OrderState state, OrderEvent @event)
        => state with { Status = "Shipped" };
}
