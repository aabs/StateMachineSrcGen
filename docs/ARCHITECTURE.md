# Architecture Blueprint

This document describes the internal architecture of StateMachineSrcGen for developers who want to understand, modify, or extend the system.

## System Overview

StateMachineSrcGen is a Roslyn Incremental Source Generator that operates as a three-stage pipeline of pure functions. It reads user-authored C# class declarations decorated with marker attributes and emits complete state machine implementations at compile time.

```
User Source Code → [Parsing] → [Analysis] → [Generation] → Generated C# Source
                       ↓             ↓             ↓
                  Diagnostics    Diagnostics    Diagnostics
```

The system is split into three assemblies:

| Assembly | Target | Purpose |
|----------|--------|---------|
| `StateMachineSrcGen` | netstandard2.0 | The generator itself (Roslyn host requirement) |
| `StateMachineSrcGen.Attributes` | netstandard2.0 | Lightweight markers for user code (no Roslyn dependency) |
| `StateMachineSrcGen.Tests` | net9.0 | Property-based and integration tests |

## Project Layout

```
StateMachineSrcGen/
├── src/
│   ├── StateMachineSrcGen/              # Generator (netstandard2.0)
│   │   ├── Parsing/                     # Stage 1: Syntax extraction
│   │   ├── Analysis/                    # Stage 2: Semantic validation
│   │   ├── Generation/                  # Stage 3: Code emission
│   │   ├── Diagnostics/                 # Diagnostic descriptors
│   │   ├── Pipeline/                    # IIncrementalGenerator wiring
│   │   ├── EquatableArray.cs            # Value-equality collection wrapper
│   │   ├── CompilerPolyfills.cs         # netstandard2.0 language feature polyfills
│   │   └── [Data model files]           # ParsedStateMachine, ValidatedStateMachine, etc.
│   └── StateMachineSrcGen.Attributes/   # User-facing attributes and interfaces
├── tests/
│   └── StateMachineSrcGen.Tests/        # All tests (xUnit + FsCheck)
│       ├── Parsing/                     # Parsing stage property tests
│       ├── Analysis/                    # Analysis stage property tests
│       ├── Generation/                  # Generation stage property tests
│       ├── DataModel/                   # Value equality property tests
│       ├── Orchestration/               # Runtime behavior tests
│       ├── Integration/                 # End-to-end and cache tests
│       └── Generators/                  # FsCheck custom Arbitrary generators
├── .github/workflows/release.yml        # CI/CD release pipeline
└── StateMachineSrcGen.slnx              # Solution file
```

## Pipeline Architecture

### Entry Point: `StateMachineGenerator` (Pipeline/)

The `IIncrementalGenerator` implementation. It wires the three stages together using Roslyn's incremental API:

```csharp
[Generator]
public class StateMachineGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // 1. Filter: find classes with state machine attributes
        // 2. Transform: Parse → Analyze → Generate (pure functions)
        // 3. Output: AddSource or ReportDiagnostic
    }
}
```

The pipeline uses `CreateSyntaxProvider` with a predicate that checks for `[State]`, `[Trigger]`, `[Transition]`, `[Guard]`, or `[SideEffect]` attributes. The transform function chains all three stages. Roslyn's incremental caching avoids re-execution when inputs haven't changed — this works because all intermediate data models implement value equality.

### Stage 1: Parsing (Parsing/)

**Input:** `ClassDeclarationSyntax` + `SemanticModel`
**Output:** `ParsedStateMachine` (value type) + diagnostics

The parsing stage extracts syntactic and semantic information from user source code into a normalized intermediate model. It consists of four components:

| Component | Responsibility |
|-----------|---------------|
| `SyntaxExtractor` | Predicate filter — determines if a class has state machine attributes |
| `DeclarationParser` | Extracts class modifiers, generics, interface implementations, `[State]`/`[Trigger]` attributes |
| `HandlerParser` | Extracts `[Transition]`/`[Guard]`/`[SideEffect]` methods, validates signatures |
| `ParsedModelFactory` | Assembles components into `ParsedStateMachine`, wraps in try-catch |

**Key design decisions:**
- Uses `SemanticModel` to resolve attribute types (not string matching on syntax)
- Validates handler signatures during parsing (public, static, correct params/return)
- Detects interface implementations (`IStateMachine`, `IStatePersistence`, `IDispatchableEvent`) via symbol analysis
- Emits SMSG010-013, SMSG016 for structural violations
- Never throws — catches all exceptions and emits SMSG015

**`ParsingPipeline.Parse()`** is the public entry point that orchestrates these components.

### Stage 2: Analysis (Analysis/)

**Input:** `ParsedStateMachine` (value type)
**Output:** `ValidatedStateMachine` (value type) or `null` + diagnostics

The analysis stage validates semantic correctness of the parsed model. It operates entirely on value types — no Roslyn APIs needed. This makes it trivially testable with property-based tests.

| Component | Responsibility |
|-----------|---------------|
| `StructureValidator` | Checks class modifiers, interface implementations |
| `StateValidator` | Empty states, initial state cardinality, duplicates, reachability |
| `TriggerValidator` | Duplicate trigger names |
| `TransitionValidator` | Duplicate handlers, undefined state/trigger references |
| `SignatureValidator` | Handler method signature conformance |

**Key design decisions:**
- If ANY Error-severity diagnostic is emitted, the result is `null` (no partial output)
- Warning-severity diagnostics (SMSG009 unreachable state) do NOT prevent output
- Builds `ValidatedStateMachine` with enriched data (terminal state detection, guard/side-effect association)
- Deterministic — same input always produces same output
- Never throws — catches all exceptions and emits SMSG015

**`AnalysisPipeline.Analyze()`** is the public entry point.

### Stage 3: Generation (Generation/)

**Input:** `ValidatedStateMachine` (value type)
**Output:** `string` (generated C# source) + diagnostics

The generation stage emits complete, compilable C# source from the validated model. It uses `StringBuilder`-based code emission (no Roslyn syntax tree construction — simpler and faster for this use case).

| Component | Responsibility |
|-----------|---------------|
| `HandleMethodEmitter` | Generates the `HandleAsync` method with lock/try/finally structure |
| `EventDispatchEmitter` | Generates the switch/case block over `GetEventId()` |
| `TransitionEvaluatorEmitter` | Generates guard evaluation in declaration order |
| `OrchestrationEmitter` | Generates guard→action→save→side-effect per transition |
| `PersistenceEmitter` | Generates default `InMemoryPersistence` nested class |
| `LockEmitter` | Generates default `NoOpLock` nested class |
| `SourceFormatter` | Wraps everything in namespace, partial class, XML docs, `#nullable enable` |

**Key design decisions:**
- Generated code uses fully-qualified type names (`StateMachineSrcGen.TransitionResult`) to avoid using-statement conflicts
- Generated code uses `ConfigureAwait(false)` on all awaits
- Lock release is in a `finally` block — guaranteed even on exceptions
- No reflection, no `dynamic`, no `Type.GetMethod` in generated output
- XML documentation on all public members
- Deterministic output — same model always produces identical source text

**`GenerationPipeline.Generate()`** is the public entry point.

## Data Models

All pipeline data models are `readonly record struct` types implementing `IEquatable<T>`. This is critical for Roslyn's incremental caching — the host compares previous and current outputs to decide whether downstream stages need re-execution.

### Parsing Stage Output

```
ParsedStateMachine
├── Namespace, ClassName, StateTypeName, EventTypeName
├── States: EquatableArray<ParsedState>
├── Triggers: EquatableArray<ParsedTrigger>
├── Handlers: EquatableArray<ParsedHandler>
├── Modifiers: ClassModifiers (flags enum)
├── ImplementsIStateMachine, ImplementsIStatePersistence, ImplementsIDispatchableEvent
├── EventIdTypeName
└── Location
```

### Analysis Stage Output

```
ValidatedStateMachine
├── Namespace, ClassName, StateTypeName, EventTypeName, EventIdTypeName
├── States: EquatableArray<ValidatedState>  (enriched with IsTerminal)
├── InitialState: ValidatedState
└── Transitions: EquatableArray<ValidatedTransition>  (enriched with guard/side-effect associations)
```

### EquatableArray<T>

A `readonly struct` wrapping `ImmutableArray<T>` that provides element-wise equality. Without this, Roslyn's caching would always consider arrays as "changed" (reference equality). This is the single most important type for incremental performance.

## Diagnostics

All diagnostics are defined in `DiagnosticDescriptors.cs` as static `DiagnosticDescriptor` instances. Each has:
- A unique `SMSG###` ID
- A severity (Error or Warning)
- A message format with placeholders
- The category "StateMachineSrcGen"

The generator follows a **fail-safe** strategy: errors produce diagnostics, never exceptions. Every pipeline stage wraps its logic in try-catch, with the outermost catch emitting SMSG015 (internal error).

## Generated Code Structure

For a state machine with one transition, the generated output looks like:

```csharp
// <auto-generated/>
#nullable enable

namespace MyApp
{
    /// <summary>Generated state machine implementation for OrderMachine.</summary>
    public static partial class OrderMachine
    {
        private static StateMachineSrcGen.IStatePersistence<OrderState> _persistence = new InMemoryPersistence();
        private static StateMachineSrcGen.IStateLock<OrderState> _lock = new NoOpLock();

        /// <summary>Handles an incoming event...</summary>
        public static async Task<TransitionResult> HandleAsync(OrderEvent @event)
        {
            if (!await _lock.AcquireAsync().ConfigureAwait(false))
                return TransitionResult.LockFailed;
            try
            {
                var currentState = await _persistence.LoadAsync().ConfigureAwait(false);
                var eventId = @event.GetEventId();

                switch (eventId)
                {
                    case "confirm":
                    {
                        if (currentState == "Pending")
                        {
                            var newState = HandleConfirm(currentState, @event);
                            await _persistence.SaveAsync(newState).ConfigureAwait(false);
                            return TransitionResult.Success;
                        }
                        break;
                    }
                }

                return TransitionResult.NotHandled;
            }
            finally
            {
                await _lock.ReleaseAsync().ConfigureAwait(false);
            }
        }

        private sealed class InMemoryPersistence : IStatePersistence<OrderState> { ... }
        private sealed class NoOpLock : IStateLock<OrderState> { ... }
    }
}
```

## Testing Strategy

The project uses two complementary testing approaches:

### Property-Based Tests (FsCheck + xUnit)

Each correctness property from the design document maps to one or more `[Property]` test methods. These verify universal invariants across randomly generated inputs:

- **Parsing properties** — attribute extraction, modifier validation, interface detection
- **Analysis properties** — duplicate detection, reference validation, reachability, error emission
- **Generation properties** — compilation correctness, no reflection, XML docs, determinism
- **Orchestration properties** — dispatch correctness, guard ordering, failure handling, lock lifecycle

### Integration Tests

- **End-to-end compilation** — full pipeline produces valid assemblies
- **Incremental caching** — unrelated changes don't trigger re-generation
- **NuGet packaging** — attributes assembly has no Roslyn dependency
- **Snapshot tests** — generated output stability across refactors
- **Runtime execution** — compiled generated code behaves correctly

### Custom FsCheck Generators

`StateMachineArbitraries.cs` provides `Arbitrary<T>` generators for all data models, including:
- Valid and invalid `ParsedStateMachine` variants
- Controlled name collisions for duplicate detection testing
- Various `EventId` value types for dispatch testing

## Incremental Caching

Roslyn's incremental generator API caches intermediate results and only re-executes stages when inputs change. This works because:

1. **Value equality on all data models** — `readonly record struct` with `EquatableArray<T>` for collections
2. **Pure function stages** — no side effects, no ambient state
3. **Deterministic output** — same input always produces same output

When a user edits an unrelated file, the `SyntaxProvider` predicate filters it out immediately. When a state machine file changes, only that definition is re-processed.

## Error Handling Philosophy

1. **Generator never crashes the compiler** — all exceptions are caught and converted to SMSG015 diagnostics
2. **No partial output** — if any error is detected, zero source is emitted for that definition
3. **Actionable messages** — every diagnostic tells the user what's wrong and hints at the fix
4. **No cascading errors** — once an error is found, processing stops for that definition

## Extending the System

### Adding a new diagnostic

1. Add a `DiagnosticDescriptor` to `DiagnosticDescriptors.cs`
2. Emit it from the appropriate validator
3. Add property tests verifying it fires correctly

### Adding a new attribute

1. Add the attribute class to `StateMachineSrcGen.Attributes`
2. Update `SyntaxExtractor` to recognize it
3. Update `HandlerParser` or `DeclarationParser` to extract its data
4. Update the data models if new fields are needed
5. Update analysis validators if new rules apply
6. Update generation emitters if it affects output

### Adding a new generation feature

1. Create a new emitter in `Generation/`
2. Wire it into `GenerationPipeline`
3. Add property tests for compilation correctness and code quality
4. Add snapshot tests for output stability

## Build and Release

```bash
# Development
dotnet build
dotnet test

# Release (triggered by pushing a v* tag to main)
# See .github/workflows/release.yml
git tag v1.0.0
git push origin v1.0.0
```

The release workflow: restore → build (Release) → test → pack → push to nuget.org. It derives the package version from the git tag (strips the `v` prefix). Both stable (`1.0.0`) and pre-release (`1.0.0-preview.1`) versions are supported.
