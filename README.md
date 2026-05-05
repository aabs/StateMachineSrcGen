# StateMachineSrcGen

A Roslyn Incremental Source Generator that transforms concise, attribute-decorated C# class declarations into fully-functional state machine implementations at compile time — zero runtime reflection, zero boilerplate.

## Features

- **Zero runtime reflection** — all dispatch, orchestration, and wiring is generated at compile time
- **Incremental generation** — only regenerates when relevant source changes; IDE stays responsive
- **Full orchestration protocol** — lock → load → guard → action → save → side-effect → release
- **Pluggable persistence** — implement `IStatePersistence<TState>` for any storage backend
- **Pluggable locking** — implement `IStateLock<TState>` for distributed or local concurrency
- **Automatic event dispatch** — routing via `IDispatchableEvent<TEventId>` and generated switch statements
- **Compile-time validation** — structural errors surface as compiler diagnostics, not runtime exceptions

## Installation

```bash
dotnet add package StateMachineSrcGen
```

The package auto-configures both the source generator and the lightweight attributes assembly. No manual `.csproj` edits beyond the package reference.

## Quick Start

### 1. Define your state and event types

```csharp
using StateMachineSrcGen;

// Your state can be any type — records, classes, primitives
public record OrderState(string Status, DateTime? ConfirmedAt = null);

// Events must implement IDispatchableEvent<TEventId> for routing
public record OrderEvent(string Action, string? Reason = null) : IDispatchableEvent<string>
{
    public string GetEventId() => Action;
}
```

### 2. Declare the state machine

```csharp
[State("Pending", IsInitial = true)]
[State("Confirmed")]
[State("Cancelled")]
[Trigger("Confirm")]
[Trigger("Cancel")]
public static partial class OrderMachine : IStateMachine<OrderState, OrderEvent>, IStatePersistence<OrderState>
{
    // Transition handler: Pending → Confirmed when "Confirm" event arrives
    [Transition("Pending", "Confirmed", "Confirm", EventId = "confirm")]
    public static OrderState HandleConfirm(OrderState state, OrderEvent @event)
        => state with { Status = "Confirmed", ConfirmedAt = DateTime.UtcNow };

    // Guard: only allow cancellation if not already confirmed
    [Guard("Pending", "Cancelled", "Cancel")]
    public static bool CanCancel(OrderState state, OrderEvent @event)
        => state.Status == "Pending";

    // Transition handler: Pending → Cancelled
    [Transition("Pending", "Cancelled", "Cancel", EventId = "cancel")]
    public static OrderState HandleCancel(OrderState state, OrderEvent @event)
        => state with { Status = "Cancelled" };

    // Side effect: runs after successful cancellation (state already saved)
    [SideEffect("Pending", "Cancelled", "Cancel")]
    public static void OnCancelled(OrderState state, OrderEvent @event)
    {
        Console.WriteLine($"Order cancelled: {state.Status}");
    }

    // Required interface stubs (generated code provides the real implementation)
    public Task<TransitionResult> HandleAsync(OrderEvent @event) => throw new NotImplementedException();
    public Task<OrderState> LoadAsync() => throw new NotImplementedException();
    public Task SaveAsync(OrderState state) => throw new NotImplementedException();
}
```

### 3. Use the generated state machine

```csharp
// The generator produces a HandleAsync method that orchestrates everything
var result = await OrderMachine.HandleAsync(new OrderEvent("confirm"));

switch (result)
{
    case TransitionResult.Success:
        Console.WriteLine("Order confirmed!");
        break;
    case TransitionResult.NotHandled:
        Console.WriteLine("No matching transition for current state");
        break;
    case TransitionResult.LockFailed:
        Console.WriteLine("Could not acquire lock");
        break;
}
```

## Concepts

### States and Triggers

States are declared with `[State("Name")]` on the class. Exactly one must be marked `IsInitial = true`. Triggers are declared with `[Trigger("Name")]` and represent event categories.

### Transition Handlers

Methods decorated with `[Transition("From", "To", "Trigger", EventId = "...")]` define what happens when a transition fires. They receive the current state and event, and return the new state.

### Guards

Methods decorated with `[Guard("From", "To", "Trigger")]` return `bool` and determine whether a transition is permitted. When multiple transitions match the same state+event, guards are evaluated in declaration order — first `true` wins.

### Side Effects

Methods decorated with `[SideEffect("From", "To", "Trigger")]` run after the state has been persisted. If a side effect throws, the state change is NOT rolled back (it's already saved).

### Event Dispatch

Your event type must implement `IDispatchableEvent<TEventId>`. The generated code calls `GetEventId()` and uses the returned value in a switch statement to route to the correct handler. This eliminates all manual dispatch boilerplate.

### Persistence

The generated code calls `LoadAsync()` before processing and `SaveAsync(newState)` after a successful transition. By default, an in-memory implementation is provided. Replace it by implementing `IStatePersistence<TState>`:

```csharp
public class DatabasePersistence : IStatePersistence<OrderState>
{
    public async Task<OrderState> LoadAsync()
    {
        // Load from your database
    }

    public async Task SaveAsync(OrderState state)
    {
        // Save to your database
    }
}
```

### Locking

The generated code acquires a lock before any work and releases it in a `finally` block. The default `NoOpLock` does nothing. For distributed systems, implement `IStateLock<TState>`:

```csharp
public class RedisLock : IStateLock<OrderState>
{
    public async Task<bool> AcquireAsync()
    {
        // Acquire distributed lock; return false if unavailable
    }

    public async Task ReleaseAsync()
    {
        // Release the lock
    }
}
```

## Orchestration Protocol

Every call to `HandleAsync` follows this exact sequence:

```
1. Acquire lock         → if fails, return LockFailed
2. Load state           → if throws, release lock, propagate exception
3. Extract event ID     → @event.GetEventId()
4. Match transition     → switch on (eventId, currentState)
5. Evaluate guard       → if false, try next; if all false, return NotHandled
6. Invoke handler       → if throws, release lock, propagate (no save)
7. Save new state       → if throws, release lock, propagate
8. Invoke side effect   → if throws, propagate (state already saved)
9. Release lock
10. Return Success
```

## Compile-Time Diagnostics

The generator validates your definition at compile time and reports clear errors:

| ID | Severity | Meaning |
|----|----------|---------|
| SMSG001 | Error | Duplicate transition handler (same From/To/Trigger) |
| SMSG002 | Error | Handler references undeclared state |
| SMSG003 | Error | Handler references undeclared trigger |
| SMSG004 | Error | No states declared |
| SMSG005 | Error | No initial state designated |
| SMSG006 | Error | Multiple initial states |
| SMSG007 | Error | Duplicate state names |
| SMSG008 | Error | Duplicate trigger names |
| SMSG009 | Warning | Unreachable state (no inbound transitions) |
| SMSG010 | Error | Class missing required modifiers (public static partial) |
| SMSG011 | Error | Missing IStateMachine implementation |
| SMSG012 | Error | Invalid handler method signature |
| SMSG013 | Error | Missing IStatePersistence implementation |
| SMSG014 | Error | Transition missing target state |
| SMSG015 | Error | Internal generator error |
| SMSG016 | Error | Event type missing IDispatchableEvent |

## Class Declaration Requirements

Your state machine class must be:
- `public static partial class`
- Implement `IStateMachine<TState, TEvent>`
- Implement `IStatePersistence<TState>`
- Event type must implement `IDispatchableEvent<TEventId>`

Handler methods must be:
- `public static`
- Accept `(TState state, TEvent @event)` parameters
- Return `TState` (transitions), `bool` (guards), or `void` (side effects)

## Building from Source

```bash
dotnet restore
dotnet build
dotnet test
```

## License

MIT
