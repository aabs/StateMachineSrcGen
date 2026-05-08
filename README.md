# StateMachineSrcGen

A Roslyn incremental source generator that turns concise, attribute-decorated state machine declarations into generated dispatch/orchestration code at compile time.

## Features

- Incremental Roslyn source generation
- Attribute-driven transition, guard, and side-effect model
- Generated async orchestration (`HandleAsync`) with default in-memory persistence and no-op lock
- Compile-time validation diagnostics (`SMSG*`)

## Installation

```bash
dotnet add package StateMachineSrcGen
```

The package includes the source generator and required attributes/interfaces.

## Current Supported Usage

The current end-to-end generation path is centered on string state values.

### 1. Define event type

```csharp
using StateMachineSrcGen;

public record OrderEvent(string Action) : IDispatchableEvent<string>
{
    public string GetEventId() => Action;
}
```

### 2. Declare machine class

```csharp
using StateMachineSrcGen;

[State("Pending", IsInitial = true)]
[State("Confirmed")]
[Trigger("Confirm")]
public static partial class OrderMachine
{
    [Transition("Pending", "Confirmed", "Confirm", EventId = "confirm")]
    public static string HandleConfirm(string state, OrderEvent @event)
        => "Confirmed";
}
```

### 3. Call generated API

```csharp
var result = await OrderMachine.HandleAsync(new OrderEvent("confirm"));
```

## Declaration Contract (Current)

Class requirements currently enforced:
- `public static partial class`
- Event type used by handlers must implement `IDispatchableEvent<TEventId>`
- Class generic parameters must be either 0 or 2

Handler requirements currently enforced:
- `public static`
- Exactly two parameters: `(TState state, TEvent @event)`
- Return type by attribute kind:
  - `[Transition]` => `TState`
  - `[Guard]` => `bool`
  - `[SideEffect]` => `void`

Model requirements currently enforced:
- At least one `[State]`
- Exactly one initial state (`IsInitial = true`)
- Unique state names and unique trigger names
- Handler references must point to declared states/triggers
- No duplicate transition handlers with same `(From, To, Trigger)`

## Runtime Notes

Generated code currently:
- Calls `@event.GetEventId()` and routes via generated switch logic.
- Uses an internal default `InMemoryPersistence` and `NoOpLock`.
- Executes orchestration in this order: acquire lock, load, dispatch/guard/action, save, side effect, release.

## Custom State Persistence

The generated code defaults to an in-memory persistence provider. To persist state durably (file system, database, etc.), implement `IStatePersistence<TState>` and wire it into the machine via a partial class.

### 1. Implement `IStatePersistence<TState>`

```csharp
using System.Text.Json;
using StateMachineSrcGen;

public sealed class FileOrderPersistence : IStatePersistence<string>
{
    private readonly string _filePath;
    private readonly string _initialStatus;
    private static readonly JsonSerializerOptions s_jsonOptions = new() { WriteIndented = true };

    public FileOrderPersistence(string filePath, string initialStatus)
    {
        _filePath = filePath;
        _initialStatus = initialStatus;
    }

    public Task<string> LoadAsync()
    {
        if (!File.Exists(_filePath))
            return Task.FromResult(_initialStatus);

        var json = File.ReadAllText(_filePath);
        var state = JsonSerializer.Deserialize<string>(json, s_jsonOptions);
        return Task.FromResult(state ?? _initialStatus);
    }

    public Task SaveAsync(string state)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(state, s_jsonOptions);
        File.WriteAllText(_filePath, json);
        return Task.CompletedTask;
    }
}
```

### 2. Expose a configuration method via partial class

The generated code declares `_persistence` as a `private static` field in the partial class. Because your hand-written code is part of the same partial class, you can access and reassign it:

```csharp
public static partial class OrderMachine
{
    public static void UsePersistence(IStatePersistence<string> persistence)
    {
        _persistence = persistence;
    }
}
```

### 3. Wire it up before use

```csharp
var stateFile = Path.Combine(Path.GetTempPath(), "order-state.json");
OrderMachine.UsePersistence(new FileOrderPersistence(stateFile, "Pending"));

var result = await OrderMachine.HandleAsync(new OrderEvent("confirm"));
// State is now persisted to disk
```

The same pattern works for `_lock` if you need a custom `IStateLock<TState>` implementation (e.g., distributed locking).

## Compile-Time Diagnostics

The generator validates your definition at compile time and reports diagnostics:

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
| SMSG010 | Error | Invalid class declaration (modifiers / class generic parameter count) |
| SMSG012 | Error | Invalid handler method signature |
| SMSG014 | Error | Transition missing target state |
| SMSG015 | Error | Internal generator error |
| SMSG016 | Error | Event type missing IDispatchableEvent |

Diagnostics `SMSG011` and `SMSG013` exist as descriptors in code but are not currently part of the active validation path.

## Building from Source

```bash
dotnet restore
dotnet build
dotnet test
```

## License

MIT
