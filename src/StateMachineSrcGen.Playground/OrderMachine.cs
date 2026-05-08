using StateMachineSrcGen;

namespace StateMachineSrcGen.Playground;

[State("Pending", IsInitial = true)]
[State("Confirmed")]
[Trigger("Confirm")]
public static partial class OrderMachine
{
    [Transition("Pending", "Confirmed", "Confirm", EventId = "confirm")]
    public static string HandleConfirm(string state, OrderEvent @event)
        => "Confirmed";
}
