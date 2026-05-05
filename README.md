# StateMachineSrcGen

A Roslyn Incremental Source Generator that transforms concise, attribute-decorated C# class declarations into fully-functional state machine implementations at compile time.

## Features

- **Zero runtime reflection** — all code is generated at compile time
- **Incremental generation** — only regenerates when relevant code changes
- **Full orchestration** — generates lock acquisition, state persistence, guard evaluation, and side effect invocation
- **Pluggable persistence** — implement `IStatePersistence<TState>` for custom storage
- **Pluggable locking** — implement `IStateLock<TState>` for custom concurrency control
- **Event dispatch** — automatic routing via `IDispatchableEvent<TEventId>`

## Quick Start

```csharp
using StateMachineSrcGen;

public record OrderState(string Status);
public record OrderEvent(string Action) : IDispatchableEvent<string>
{
    public string GetEventId() => Action;
}

[State("Pending", IsInitial = true)]
[State("Confirmed")]
[Trigger("Confirm")]
public static partial class OrderMachine : IStateMachine<string, OrderEvent>, IStatePersistence<string>
{
    [Transition("Pending", "Confirmed", "Confirm", EventId = "confirm")]
    public static string HandleConfirm(string state, OrderEvent @event) => "Confirmed";
}
```

## License

MIT
