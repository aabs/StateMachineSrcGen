namespace StateMachineSrcGen.Playground;

/// <summary>
/// The persisted order model. Contains the state machine's status string
/// plus domain data (line items) that travels with the order through its lifecycle.
/// </summary>
public record OrderState(string Status, List<OrderItem> Items);

/// <summary>
/// A single line item in an order.
/// </summary>
public record OrderItem(string ProductName, int Quantity, decimal UnitPrice);
