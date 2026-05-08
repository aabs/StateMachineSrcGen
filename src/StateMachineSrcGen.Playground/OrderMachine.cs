using StateMachineSrcGen;

namespace StateMachineSrcGen.Playground;

/// <summary>
/// Order state machine using the new generic enum-based API.
/// Demonstrates transitions, guards, and side-effects.
/// </summary>
[InitialState((int)OrderStateId.Pending)]
[TerminalState((int)OrderStateId.Cancelled)]
public static partial class OrderMachine
{
    // --- Guards ---

    /// <summary>
    /// Guard: Cannot ship an order with no items.
    /// Returns true if the transition is allowed, false to block it.
    /// </summary>
    [Guard((int)OrderStateId.Confirmed, (int)OrderStateId.Shipped, (int)OrderEventId.Ship)]
    public static bool CanShip(OrderState state, OrderEvent @event)
        => state.ItemCount > 0;

    // --- Transitions ---

    [Transition((int)OrderStateId.Pending, (int)OrderStateId.Confirmed, (int)OrderEventId.Confirm)]
    public static OrderState HandleConfirm(OrderState state, OrderEvent @event)
        => state with { Id = OrderStateId.Confirmed };

    [Transition((int)OrderStateId.Confirmed, (int)OrderStateId.Shipped, (int)OrderEventId.Ship)]
    public static OrderState HandleShip(OrderState state, OrderEvent @event)
        => state with { Id = OrderStateId.Shipped };

    [Transition((int)OrderStateId.Pending, (int)OrderStateId.Cancelled, (int)OrderEventId.Cancel)]
    public static OrderState HandleCancel(OrderState state, OrderEvent @event)
        => state with { Id = OrderStateId.Cancelled };

    // --- Side Effects ---

    /// <summary>
    /// Side-effect: Runs after a successful Pending → Confirmed transition.
    /// Demonstrates post-transition logic like sending notifications.
    /// </summary>
    [SideEffect((int)OrderStateId.Pending, (int)OrderStateId.Confirmed, (int)OrderEventId.Confirm)]
    public static void AfterConfirm(OrderState state, OrderEvent @event)
    {
        Console.WriteLine("  [Side-Effect] Order confirmed! Sending notification...");
    }
}
