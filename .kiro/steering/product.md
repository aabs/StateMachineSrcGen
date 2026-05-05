# Product Summary

StateMachineSrcGen is a NuGet package providing a .NET Incremental Source Generator. It examines concise, declarative state machine definitions written by the user and generates the complete implementation code needed to:

- Verify the structural correctness of the state machine definition
- Evaluate transitions based on current state and trigger
- Invoke user-defined custom transition code (guards, actions, side effects)
- Persist the current state of the model to a pluggable storage mechanism

## Design Philosophy

- The user writes a minimal, declarative fragment; the generator produces everything else
- Generated code must be correct, readable, and debuggable
- The generator must be completely reliable — no silent failures, no partial output
- Fault tolerance: malformed input produces clear diagnostics, never crashes the compiler
- Performance: the incremental pipeline must avoid unnecessary recomputation

## Non-Goals

- Runtime reflection or dynamic dispatch (everything is compile-time)
- Requiring users to reference Roslyn or generator internals
