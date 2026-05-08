using StateMachineSrcGen;
using StateMachineSrcGen.Playground;

Console.WriteLine("StateMachineSrcGen Playground");
Console.WriteLine("=============================");
Console.WriteLine();

// Create a temp folder for persistence
var tempDir = Path.Combine(Path.GetTempPath(), "StateMachineSrcGen_Playground");
var stateFile = Path.Combine(tempDir, "order-state.json");
Console.WriteLine($"Persistence folder: {tempDir}");
Console.WriteLine($"State file:         {stateFile}");
Console.WriteLine();

// Order items that travel with the state
var items = new List<OrderItem>
{
    new("Widget", 3, 9.99m),
    new("Gadget", 1, 24.95m)
};

// Wire up file-based persistence with initial state "Pending"
OrderMachine.UsePersistence(new FileOrderPersistence(stateFile, "Pending", items));

// Show initial state
Console.WriteLine("--- Initial State ---");
PrintPersistedFile(stateFile);

// Send "confirm" event
Console.WriteLine("--- Sending 'confirm' event ---");
var result = await OrderMachine.HandleAsync(new OrderEvent("confirm"));
Console.WriteLine($"Result: {result}");
PrintPersistedFile(stateFile);

// Send "ship" event
Console.WriteLine("--- Sending 'ship' event ---");
result = await OrderMachine.HandleAsync(new OrderEvent("ship"));
Console.WriteLine($"Result: {result}");
PrintPersistedFile(stateFile);

// Send "ship" again — should be NotHandled since we're already in "Shipped"
Console.WriteLine("--- Sending 'ship' again (invalid from Shipped) ---");
result = await OrderMachine.HandleAsync(new OrderEvent("ship"));
Console.WriteLine($"Result: {result}");
Console.WriteLine();

// Cleanup
Console.WriteLine($"Cleaning up: {tempDir}");
try
{
    if (Directory.Exists(tempDir))
        Directory.Delete(tempDir, recursive: true);
}
catch (IOException ex)
{
    Console.WriteLine($"  Could not delete temp folder (likely held by another process): {ex.Message}");
    Console.WriteLine($"  You can delete it manually: {tempDir}");
}

static void PrintPersistedFile(string path)
{
    if (File.Exists(path))
    {
        Console.WriteLine(File.ReadAllText(path));
    }
    else
    {
        Console.WriteLine("  (no file yet — using initial state)");
        Console.WriteLine();
    }
}
