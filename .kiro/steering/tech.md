# Tech Stack

## Platform

- .NET (C#)
- Roslyn Incremental Source Generators (`IIncrementalGenerator`)

## Solution Format

- `.slnx` — XML-based .NET solution format (requires .NET 9+ SDK)

## Key Libraries

- `Microsoft.CodeAnalysis.CSharp` — Roslyn compiler APIs for syntax/semantic analysis
- `Microsoft.CodeAnalysis.Analyzers` — analyzer/generator development support
- `xUnit` — test framework
- `FsCheck.Xunit` (xunit.fscheck) — property-based testing integration

## Build & Commands

```bash
# Restore dependencies
dotnet restore

# Build the solution
dotnet build

# Run all tests
dotnet test

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"

# Pack NuGet package
dotnet pack
```

## Development Conventions

### Source Generator

- Use `IIncrementalGenerator` (incremental API only, never legacy `ISourceGenerator`)
- Generator assembly targets `netstandard2.0` (Roslyn host requirement)
- Use the `[Generator]` attribute on generator classes
- All syntax tree analysis and code generation logic must be implemented as **pure functions** with minimal dependency on IDE/host runtime
- Pipeline stages (parsing → analysis → generation) must be independently testable units

### Testing (Mandatory TDD with Property-Based Tests)

- **TDD is non-negotiable** — tests are written before implementation
- **Property-based testing** via xUnit + FsCheck is the primary testing strategy
- Every pipeline stage must have comprehensive property-based tests
- Target **100% code coverage** — no untested paths
- Aggressively test corner cases: empty inputs, malformed syntax, duplicate states, circular transitions, missing targets, etc.
- Use in-memory compilation for integration-level generator tests
- Use snapshot/approval testing for verifying generated output stability
- Tests must be deterministic and fast

### Reliability & Fault Tolerance

- The generator must never crash the compiler host
- All error paths must produce Roslyn diagnostics (warnings/errors), not exceptions
- Malformed or incomplete user input must degrade gracefully with actionable diagnostics
- No partial code emission — output is either complete and correct, or not emitted

### Performance

- Leverage Roslyn's incremental caching — avoid recomputation on unrelated edits
- Minimize allocations in hot paths
- Use value types and `Equatable` implementations for pipeline data models to enable correct caching
