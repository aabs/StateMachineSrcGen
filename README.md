# StateMachineSrcGen

A Roslyn incremental source generator that transforms concise, attribute-decorated state machine declarations into fully-functional implementations at compile time — with type-safe enum-based transitions, guards, side-effects, entry callbacks, and terminal-state cleanup.

## Features

- Type-safe, enum-driven state and event declarations
- Compile-time validation with actionable diagnostics
- Guards to conditionally block transitions
- Side-effects for post-transition logic (notifications, logging, integrations)
- State-entry callbacks (targeted and catch-all)
- Terminal states with async cleanup handlers
- Pluggable persistence and locking
- Generated async orchestration (`HandleAsync`)

## Installation

```bash
dotnet add package StateMachineSrcGen
```

The package includes the source generator and required attributes/interfaces. No runtime reflection — everything is resolved at compile time.

## Quick Start

Define your state and event ID enums, implement the state/event types, then declare your state machine:

```csharp
using StateMachineSrcGen;

// 1. Define state IDs
public enum OrderStateId { Pending, Confirmed, Shipped, Cancelled }

// 2. Define event IDs
public enum OrderEventId { Confirm, Ship, Cancel }

// 3. Define state type
public record OrderState(OrderStateId Id, List<OrderItem> Items) : IStateMachineState<OrderStateId>
{
    public int ItemCount => Items.Count;
    public OrderStateId GetStateId() => Id;
}

// 4. Define event type
public record OrderEvent(OrderEventId EventType) : IDispatchableEvent<OrderEventId>
{
    public OrderEventId GetEventId() => EventType;
}

// 5. Declare the state machine
[InitialState((int)OrderStateId.Pending)]
[TerminalState((int)OrderStateId.Cancelled)]
public static partial class OrderMachine
{
    [Transition((int)OrderStateId.Pending, (int)OrderStateId.Confirmed, (int)OrderEventId.Confirm)]
    public static OrderState HandleConfirm(OrderState state, OrderEvent @event)
        => state with { Id = OrderStateId.Confirmed };

    [Transition((int)OrderStateId.Confirmed, (int)OrderStateId.Shipped, (int)OrderEventId.Ship)]
    public static OrderState HandleShip(OrderState state, OrderEvent @event)
        => state with { Id = OrderStateId.Shipped };

    [Transition((int)OrderStateId.Pending, (int)OrderStateId.Cancelled, (int)OrderEventId.Cancel)]
    public static OrderState HandleCancel(OrderState state, OrderEvent @event)
        => state with { Id = OrderStateId.Cancelled };
}
```

Use the generated API:

```csharp
var result = await OrderMachine.HandleAsync(new OrderEvent(OrderEventId.Confirm));

if (result.IsSuccess)
    Console.WriteLine($"New state: {result.State!.GetStateId()}");
```

## State Machine Declaration

### State and Event ID Enums

Define plain enums for your states and events. Each enum member becomes a valid state or trigger:

```csharp
public enum OrderStateId { Pending, Confirmed, Shipped, Cancelled }
public enum OrderEventId { Confirm, Ship, Cancel }
```

`[Flags]` enums are not supported and will produce diagnostic SMSG026.

### State and Event Types

Your state type must implement `IStateMachineState<TStateId>`:

```csharp
public record OrderState(OrderStateId Id, List<OrderItem> Items) : IStateMachineState<OrderStateId>
{
    public OrderStateId GetStateId() => Id;
}
```

Your event type must implement `IDispatchableEvent<TEventId>`:

```csharp
public record OrderEvent(OrderEventId EventType) : IDispatchableEvent<OrderEventId>
{
    public OrderEventId GetEventId() => EventType;
}
```

### Machine Class

The state machine is a `public static partial class`. The generator derives valid states from the state ID enum and valid triggers from the event ID enum — no `[State]` or `[Trigger]` attributes needed:

```csharp
[InitialState((int)OrderStateId.Pending)]
[TerminalState((int)OrderStateId.Cancelled)]
public static partial class OrderMachine
{
    // Transition handlers, guards, side-effects go here
}
```

Enum values are passed as `(int)` casts in attributes because C# requires constant expressions in attribute arguments. The generator resolves them back to enum members at compile time.

## Attributes Reference

### `[InitialState(int state)]`

Class-level. Designates which enum member is the starting state. Exactly one is required.

```csharp
[InitialState((int)OrderStateId.Pending)]
```

### `[TerminalState(int state)]`

Class-level. Designates terminal/final states where the state machine lifecycle ends. Zero or more allowed.

```csharp
[TerminalState((int)OrderStateId.Cancelled)]
```

### `[Transition(int from, int to, int trigger)]`

Method-level. Declares a transition handler for a specific (from-state, to-state, trigger) triple.

```csharp
[Transition((int)OrderStateId.Pending, (int)OrderStateId.Confirmed, (int)OrderEventId.Confirm)]
public static OrderState HandleConfirm(OrderState state, OrderEvent @event)
    => state with { Id = OrderStateId.Confirmed };
```

Signature: `public static TState MethodName(TState state, TEvent @event)`

### `[Guard(int from, int to, int trigger)]`

Method-level. Declares a guard condition for a transition. See [Guards](#guards).

### `[SideEffect(int from, int to, int trigger)]`

Method-level. Declares a side-effect that runs after a transition is persisted. See [Side Effects](#side-effects).

### `[OnEnter]` / `[OnEnter(int state)]`

Method-level. Declares a state-entry callback. See [Entry Callbacks](#entry-callbacks).

### `[OnTerminal]`

Method-level. Declares the cleanup handler invoked when entering a terminal state. See [Terminal States and Cleanup](#terminal-states-and-cleanup).

## Guards

Guards evaluate whether a transition is permitted based on the current state and event. They run before the transition handler and can prevent the transition entirely.

A guard returns `bool`: `true` allows the transition, `false` blocks it.

```csharp
/// <summary>
/// Cannot ship an order with no items.
/// </summary>
[Guard((int)OrderStateId.Confirmed, (int)OrderStateId.Shipped, (int)OrderEventId.Ship)]
public static bool CanShip(OrderState state, OrderEvent @event)
    => state.ItemCount > 0;
```

**Signature requirement:** `public static bool MethodName(TState state, TEvent @event)`

When a guard returns `false`, the transition is blocked and `HandleAsync` returns `TransitionResult.GuardRejected`:

```csharp
var emptyOrder = new OrderState(OrderStateId.Confirmed, new List<OrderItem>());
// ... (persistence configured with emptyOrder)

var result = await OrderMachine.HandleAsync(new OrderEvent(OrderEventId.Ship));
// result.Outcome == TransitionOutcome.GuardRejected
// No state mutation, no persistence write, no side-effects
```

Guards are associated with a specific transition triple `(from, to, trigger)`. If no guard is defined for a transition, the transition proceeds unconditionally.

## Side Effects

Side-effects execute after a transition has been successfully persisted. They are used for non-state-mutating work such as sending notifications, logging audit trails, or triggering downstream processes.

Side-effects cannot affect the transition outcome — by the time they run, the new state is already persisted.

```csharp
/// <summary>
/// Send a notification after order confirmation.
/// </summary>
[SideEffect((int)OrderStateId.Pending, (int)OrderStateId.Confirmed, (int)OrderEventId.Confirm)]
public static void AfterConfirm(OrderState state, OrderEvent @event)
{
    Console.WriteLine("Order confirmed! Sending notification...");
}
```

**Signature requirement:** `public static void MethodName(TState state, TEvent @event)`

Side-effects are associated with a specific transition triple `(from, to, trigger)`. They only run on successful non-terminal transitions. Terminal transitions invoke the cleanup handler instead.

## Entry Callbacks

Entry callbacks are invoked whenever the state machine enters a specific state (targeted) or any state (catch-all). They run after the transition handler but before persistence.

### Targeted Entry Callback

Invoked only when entering the specified state. Returns `TState`, allowing state mutation before persistence:

```csharp
[OnEnter((int)OrderStateId.Confirmed)]
public static OrderState OnEnterConfirmed(OrderState state, OrderEvent @event)
    => state with { /* add timestamp, initialize sub-state, etc. */ };
```

**Signature:** `public static TState MethodName(TState state, TEvent @event)`

### Catch-All Entry Callback

Invoked on every state entry regardless of which state is entered. Has `void` return type — it's observational only:

```csharp
[OnEnter]
public static void OnEnterAny(OrderState state, OrderEvent @event)
{
    Console.WriteLine($"Entered state: {state.GetStateId()}");
}
```

**Signature:** `public static void MethodName(TState state, TEvent @event)`

### Ordering

When both a targeted and catch-all callback exist for a transition, the targeted callback runs first (its return value becomes the state), then the catch-all runs for observation.

At most one catch-all `[OnEnter]` is permitted. Multiple targeted `[OnEnter]` methods may exist for different states, but only one per state.

## Terminal States and Cleanup

Terminal states represent the end of the state machine lifecycle. When the machine transitions into a terminal state, no further transitions are possible.

Declare terminal states with `[TerminalState]`:

```csharp
[TerminalState((int)OrderStateId.Cancelled)]
```

### Cleanup Handler

An optional async cleanup handler runs when entering a terminal state. Use it for end-of-lifecycle work like resource cleanup or state machine deletion:

```csharp
[OnTerminal]
public static async Task CleanupOrder(OrderState state)
{
    await DeleteOrderResources(state);
}
```

**Signature:** `public static Task MethodName(TState state)`

Only one `[OnTerminal]` method is permitted per state machine class. The cleanup handler replaces side-effects for terminal transitions — side-effects do not run when entering a terminal state.

## Orchestration Ordering

The generated `HandleAsync` method follows a strict orchestration sequence.

### Non-Terminal Transitions

```
1. Acquire lock
2. Load current state
3. Evaluate guard (if defined) → reject if false
4. Execute transition handler → produces new state
5. Execute targeted [OnEnter] callback (if defined) → may mutate state
6. Execute catch-all [OnEnter] callback (if defined) → observational
7. Persist new state
8. Execute side-effect (if defined)
9. Release lock (in finally block)
```

### Terminal Transitions

```
1. Acquire lock
2. Load current state
3. Evaluate guard (if defined) → reject if false
4. Execute transition handler → produces new state
5. Execute targeted [OnEnter] callback (if defined) → may mutate state
6. Execute catch-all [OnEnter] callback (if defined) → observational
7. Persist new state
8. Execute cleanup handler (if defined)
9. Release lock (in finally block)
```

The lock is always released in a `finally` block, ensuring release even if an exception occurs during orchestration.

## TransitionResult

`HandleAsync` returns `TransitionResult<TState>`, which carries the outcome and the resulting state:

```csharp
var result = await OrderMachine.HandleAsync(new OrderEvent(OrderEventId.Confirm));

switch (result.Outcome)
{
    case TransitionOutcome.Success:
        Console.WriteLine($"Transitioned to: {result.State!.GetStateId()}");
        break;
    case TransitionOutcome.GuardRejected:
        Console.WriteLine("Guard blocked the transition");
        break;
    case TransitionOutcome.NoTransition:
        Console.WriteLine("No valid transition for current state + event");
        break;
}
```

| Outcome | Meaning |
|---------|---------|
| `Success` | Transition completed; `State` contains the new state |
| `GuardRejected` | A guard returned `false`; no state mutation occurred |
| `NoTransition` | No matching transition exists for the current state and event |

## Custom Persistence

The generated code defaults to an in-memory persistence provider. To persist state durably, implement `IStatePersistence<TState>` and wire it via a partial class:

```csharp
public static partial class OrderMachine
{
    public static void UsePersistence(IStatePersistence<OrderState> persistence)
    {
        _persistence = persistence;
    }
}
```

The same pattern works for `_lock` if you need a custom `IStateLock<TState>` implementation (e.g., distributed locking).

## Compile-Time Diagnostics

The generator validates your definition at compile time and reports actionable diagnostics:

| ID | Severity | Meaning |
|----|----------|---------|
| SMSG001 | Error | Duplicate transition handler (same From/To/Trigger triple) |
| SMSG002 | Error | Handler references undeclared state |
| SMSG003 | Error | Handler references undeclared trigger |
| SMSG004 | Error | No states declared |
| SMSG005 | Error | No initial state designated |
| SMSG006 | Error | Multiple initial states |
| SMSG007 | Error | Duplicate state names |
| SMSG008 | Error | Duplicate trigger names |
| SMSG009 | Warning | Unreachable state (no inbound transitions) |
| SMSG010 | Error | Invalid class declaration (modifiers) |
| SMSG012 | Error | Invalid handler method signature |
| SMSG014 | Error | Transition missing target state |
| SMSG015 | Error | Internal generator error |
| SMSG016 | Error | Event type missing `IDispatchableEvent` |
| SMSG018 | Error | Invalid enum value in attribute (not a member of the target enum) |
| SMSG019 | Error | State type missing `IStateMachineState<TStateId>` |
| SMSG020 | Warning | State is both initial and terminal |
| SMSG021 | Error | Multiple `[OnTerminal]` cleanup handlers |
| SMSG022 | Error | Multiple catch-all `[OnEnter]` methods |
| SMSG023 | Error | Duplicate targeted `[OnEnter]` for same state |
| SMSG024 | Error | Invalid `[OnEnter]` method signature |
| SMSG025 | Error | Invalid `[OnTerminal]` method signature |
| SMSG026 | Error | `[Flags]` enum used as state/event ID type |
| SMSG027 | Error | Invalid class type parameter count |

## Building from Source

```bash
dotnet restore
dotnet build
dotnet test
```

## License

MIT
