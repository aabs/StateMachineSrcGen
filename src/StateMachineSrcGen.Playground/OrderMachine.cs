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
    public static string HandleConfirm(string state, OrderEvent @event)
        => "Confirmed";

    [Transition("Confirmed", "Shipped", "Ship", EventId = "ship")]
    public static string HandleShip(string state, OrderEvent @event)
        => "Shipped";
}
