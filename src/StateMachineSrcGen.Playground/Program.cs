using StateMachineSrcGen;
using StateMachineSrcGen.Playground;

Console.WriteLine("StateMachineSrcGen Playground");
Console.WriteLine("=============================");
Console.WriteLine();

// --- Scenario 1: Order with items (guard passes, side-effect fires) ---
Console.WriteLine("=== Scenario 1: Order with items ===");
Console.WriteLine();

var tempDir = Path.Combine(Path.GetTempPath(), "StateMachineSrcGen_Playground");
var stateFile = Path.Combine(tempDir, "order-state.json");

var orderWithItems = new OrderState(OrderStateId.Pending, new List<OrderItem>
{
    new("Widget", 3, 9.99m),
    new("Gadget", 1, 24.95m)
});

OrderMachine.UsePersistence(new FileOrderPersistence(stateFile, orderWithItems));

Console.WriteLine($"Initial state: {orderWithItems.GetStateId()}, Items: {orderWithItems.ItemCount}");
Console.WriteLine();

// Confirm the order — side-effect should print notification
Console.WriteLine("--- Sending 'Confirm' event ---");
var result = await OrderMachine.HandleAsync(new OrderEvent(OrderEventId.Confirm));
Console.WriteLine($"  Result: {result.Outcome}, New State: {result.State?.GetStateId()}");
Console.WriteLine();

// Ship the order — guard passes because ItemCount > 0
Console.WriteLine("--- Sending 'Ship' event (guard should PASS: has items) ---");
result = await OrderMachine.HandleAsync(new OrderEvent(OrderEventId.Ship));
Console.WriteLine($"  Result: {result.Outcome}, New State: {result.State?.GetStateId()}");
Console.WriteLine();

// Try shipping again — should be NoTransition (already in Shipped state)
Console.WriteLine("--- Sending 'Ship' again (invalid from Shipped) ---");
result = await OrderMachine.HandleAsync(new OrderEvent(OrderEventId.Ship));
Console.WriteLine($"  Result: {result.Outcome}");
Console.WriteLine();

// --- Scenario 2: Order without items (guard rejects shipping) ---
Console.WriteLine("=== Scenario 2: Order without items (guard rejection) ===");
Console.WriteLine();

var stateFile2 = Path.Combine(tempDir, "order-state-2.json");

var emptyOrder = new OrderState(OrderStateId.Pending, new List<OrderItem>());

OrderMachine.UsePersistence(new FileOrderPersistence(stateFile2, emptyOrder));

Console.WriteLine($"Initial state: {emptyOrder.GetStateId()}, Items: {emptyOrder.ItemCount}");
Console.WriteLine();

// Confirm the empty order
Console.WriteLine("--- Sending 'Confirm' event ---");
result = await OrderMachine.HandleAsync(new OrderEvent(OrderEventId.Confirm));
Console.WriteLine($"  Result: {result.Outcome}, New State: {result.State?.GetStateId()}");
Console.WriteLine();

// Try to ship — guard rejects because ItemCount == 0
Console.WriteLine("--- Sending 'Ship' event (guard should REJECT: no items) ---");
result = await OrderMachine.HandleAsync(new OrderEvent(OrderEventId.Ship));
Console.WriteLine($"  Result: {result.Outcome} (Guard rejected: cannot ship empty order)");
Console.WriteLine();

// --- Cleanup ---
Console.WriteLine("=== Cleanup ===");
try
{
    if (Directory.Exists(tempDir))
        Directory.Delete(tempDir, recursive: true);
    Console.WriteLine($"Cleaned up: {tempDir}");
}
catch (IOException ex)
{
    Console.WriteLine($"Could not delete temp folder: {ex.Message}");
}
