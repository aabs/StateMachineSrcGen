# Implementation Plan: Generic State Machine API

## Overview

This plan migrates the StateMachineSrcGen public API from string-based `[State]`/`[Trigger]` attributes to a generic, enum-driven approach with four type parameters. Implementation follows strict TDD: tests are written before implementation code. Tasks are organized by pipeline stage (Parsing → Analysis → Generation) with property-based tests validating each correctness property. The old string-based API is completely replaced — no backward compatibility needed.

## Tasks

- [x] 1. Foundation: New attributes, data models, and FsCheck generators
  - [x] 1.1 Create new attribute classes (`InitialStateAttribute`, `TerminalStateAttribute`, `OnEnterAttribute`, `OnTerminalAttribute`)
    - Create `InitialStateAttribute` with `int State` property, `AllowMultiple = false`
    - Create `TerminalStateAttribute` with `int State` property, `AllowMultiple = true`
    - Create `OnEnterAttribute` with targeted (int state) and catch-all (parameterless) constructors
    - Create `OnTerminalAttribute` as a simple marker attribute
    - _Requirements: 3.1, 10.1, 10.7, 13.1_

  - [x] 1.2 Update existing attributes (`TransitionAttribute`, `GuardAttribute`, `SideEffectAttribute`) to accept `int` parameters
    - Replace string-based constructors with `int from, int to, int trigger` constructors
    - Remove old string-based `From`, `To`, `Trigger` properties
    - Remove `StateAttribute` and `TriggerAttribute` classes entirely
    - _Requirements: 4.1, 4.2, 4.6, 5.1, 5.2, 8.1, 8.4, 2.1, 2.2_

  - [x] 1.3 Create new data model types (`ParsedEvent`, `ParsedEntryCallback`, `ParsedCleanupHandler`, `ValidatedEntryCallback`)
    - Create `ParsedEvent` record struct with `Name`, `IntValue`, `Location`
    - Create `ParsedEntryCallback` record struct with `MethodName`, `TargetStateName`, `IsCatchAll`, `Signature`, `Location`
    - Create `ParsedCleanupHandler` record struct with `MethodName`, `Signature`, `Location`
    - Create `ValidatedEntryCallback` record struct with `MethodName`, `TargetStateName`, `IsCatchAll`, `ReturnsTState`
    - _Requirements: 13.1, 10.7_

  - [x] 1.4 Update existing data models (`ParsedStateMachine`, `ValidatedStateMachine`, `ValidatedTransition`, `ValidatedState`, `HandlerKind`)
    - Add `StateIdEnumTypeName`, `EventIdEnumTypeName`, `Events`, `InitialStateName`, `TerminalStateNames`, `EntryCallbacks`, `CleanupHandler` to `ParsedStateMachine`
    - Add `StateIdEnumTypeName`, `EventIdEnumTypeName`, `EntryCallbacks`, `CleanupHandlerMethodName` to `ValidatedStateMachine`
    - Add `FromStateEnumValue`, `ToStateEnumValue`, `TriggerEnumValue`, `IsTerminal` to `ValidatedTransition`
    - Add `EnumValue` to `ValidatedState`
    - Add `EntryCallback` and `Cleanup` to `HandlerKind` enum
    - _Requirements: 7.1, 10.5, 13.9_

  - [x] 1.5 Create `TransitionResult<TState>` and `TransitionOutcome` types in the Attributes project
    - Implement `TransitionResult<TState>` with `Succeeded`, `GuardRejected`, `NoTransition` factory methods
    - Implement `TransitionOutcome` enum
    - _Requirements: 7.5 (Clarification: GuardRejected distinct from NoTransition)_

  - [x] 1.6 Create FsCheck custom `Arbitrary` generators for new data models
    - Create `GenericApiArbitraries.cs` in `tests/StateMachineSrcGen.Tests/Generators/`
    - Implement generators for valid/invalid enum configurations, `ParsedStateMachine` with enum fields, entry callbacks, cleanup handlers
    - Implement generators for `ValidatedStateMachine` with terminal/non-terminal transitions
    - _Requirements: All (testing infrastructure)_

- [x] 2. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 3. New diagnostics descriptors (SMSG018–SMSG027)
  - [x] 3.1 Add diagnostic descriptors to `DiagnosticDescriptors.cs`
    - Add SMSG018 (invalid enum value), SMSG019 (missing type constraint), SMSG020 (initial+terminal warning), SMSG021 (multiple cleanup handlers), SMSG022 (multiple catch-all OnEnter), SMSG023 (duplicate targeted OnEnter), SMSG024 (invalid entry callback signature), SMSG025 (invalid cleanup handler signature), SMSG026 (Flags enum), SMSG027 (invalid type parameter count)
    - Remove SMSG017 descriptor (compiler handles non-enum types)
    - _Requirements: 9.1, 9.2, 9.3, 9.4_

- [x] 4. Parsing stage: Tests first, then implementation
  - [x] 4.1 Write property test: Enum member extraction produces complete state set (Property 1)
    - **Property 1: Enum member extraction produces complete state set**
    - For any valid enum type used as state ID, parser produces state set whose names exactly match enum member names
    - **Validates: Requirements 1.3, 2.3**

  - [x] 4.2 Write property test: Enum member extraction produces complete event set (Property 2)
    - **Property 2: Enum member extraction produces complete event set**
    - For any valid enum type used as event ID, parser produces event set whose names exactly match enum member names
    - **Validates: Requirements 1.4, 2.4**

  - [x] 4.3 Write property test: Integer-to-enum resolution round trip (Property 4)
    - **Property 4: Integer-to-enum resolution round trip**
    - For any valid enum member, casting to int and resolving back produces original member name
    - **Validates: Requirements 8.2**

  - [x] 4.4 Implement updated `DeclarationParser` for 4-type-parameter resolution
    - Extract four type parameters (`TStateId`, `TEventId`, `TState`, `TEvent`) from class declaration
    - Resolve concrete enum types via semantic model
    - Enumerate enum members using `INamedTypeSymbol.GetMembers()` to build state/event sets
    - Parse `[InitialState]` and `[TerminalState]` class-level attributes (resolve int → enum member)
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 3.1, 10.1, 10.2_

  - [x] 4.5 Implement updated `HandlerParser` for int-to-enum resolution
    - Resolve integer attribute arguments to enum member names for `[Transition]`, `[Guard]`, `[SideEffect]`
    - Parse `[OnEnter]` methods (targeted and catch-all) with signature detection
    - Parse `[OnTerminal]` cleanup handler methods
    - _Requirements: 4.1, 4.2, 4.6, 5.1, 5.2, 8.1, 8.2, 13.1, 10.7_

  - [x] 4.6 Update `SyntaxExtractor` to recognize new attribute patterns
    - Update `HasStateMachineAttributes` to detect `InitialState`, `TerminalState`, `OnEnter`, `OnTerminal`
    - Remove detection of old `State` and `Trigger` attributes
    - _Requirements: 2.1, 2.2_

  - [x] 4.7 Update `ParsedModelFactory` to assemble new model shape
    - Wire new fields (`Events`, `InitialStateName`, `TerminalStateNames`, `EntryCallbacks`, `CleanupHandler`) into `ParsedStateMachine`
    - _Requirements: 1.3, 1.4, 3.1, 10.1, 13.1_

- [x] 5. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 6. Analysis stage: Tests first, then implementation
  - [x] 6.1 Write property test: `[Flags]` enum produces diagnostic (Property 3)
    - **Property 3: [Flags] enum produces diagnostic**
    - For any enum decorated with [Flags] used as state/event ID, generator emits SMSG026
    - **Validates: Clarification (Flags enum rejection)**

  - [x] 6.2 Write property test: Invalid enum value produces diagnostic (Property 5)
    - **Property 5: Invalid enum value produces diagnostic**
    - For any integer not corresponding to a defined enum member, generator emits SMSG018
    - **Validates: Requirements 3.4, 4.3, 4.4, 4.5, 5.3, 8.3, 10.3, 13.10**

  - [x] 6.3 Write property test: Initial state cardinality enforcement (Property 6)
    - **Property 6: Initial state cardinality enforcement**
    - Zero [InitialState] → SMSG005; multiple [InitialState] → SMSG006
    - **Validates: Requirements 3.2, 3.3**

  - [x] 6.4 Write property test: Valid definitions produce no SMSG002/SMSG003 (Property 7)
    - **Property 7: Valid enum-based definitions produce no SMSG002/SMSG003**
    - Fully valid enum-based definitions emit zero SMSG002 or SMSG003 diagnostics
    - **Validates: Requirements 6.3**

  - [x] 6.5 Write property test: Duplicate transition detection preserved (Property 8)
    - **Property 8: Duplicate transition detection preserved**
    - Two+ handlers with same (From, To, Trigger) triple → SMSG001
    - **Validates: Requirements 6.4**

  - [x] 6.6 Write property test: Unreachable state detection for enum members (Property 9)
    - **Property 9: Unreachable state detection for enum members**
    - Non-initial enum member not targeted by any transition → SMSG009
    - **Validates: Requirements 6.5**

  - [x] 6.7 Write property test: Entry callback uniqueness enforcement (Property 13)
    - **Property 13: Entry callback uniqueness enforcement**
    - Multiple parameterless [OnEnter] → SMSG022; multiple targeted [OnEnter] same state → SMSG023
    - **Validates: Requirements 13.5, 13.11**

  - [x] 6.8 Write property test: Cleanup handler uniqueness enforcement (Property 14)
    - **Property 14: Cleanup handler uniqueness enforcement**
    - Multiple [OnTerminal] methods → SMSG021
    - **Validates: Requirements 10.8**

  - [x] 6.9 Implement `EnumValidator` (new validator)
    - Validate state/event ID types are enums (redundant with compiler, but validates [Flags])
    - Validate all attribute int values resolve to valid enum members
    - Emit SMSG026 for [Flags] enums, SMSG018 for invalid values
    - _Requirements: 6.1, 6.2, 4.3, 4.4, 4.5, 5.3, 8.3_

  - [x] 6.10 Update `StateValidator` for `[InitialState]`/`[TerminalState]` validation
    - Validate exactly one [InitialState] (SMSG005/SMSG006)
    - Validate [TerminalState] values are valid enum members
    - Emit SMSG020 warning for initial+terminal overlap
    - _Requirements: 3.2, 3.3, 3.4, 10.3, 10.4_

  - [x] 6.11 Implement `EntryCallbackValidator` (new validator)
    - Validate at most one catch-all [OnEnter] (SMSG022)
    - Validate no duplicate targeted [OnEnter] for same state (SMSG023)
    - Validate at most one [OnTerminal] (SMSG021)
    - Validate entry callback signatures (SMSG024)
    - Validate cleanup handler signature (SMSG025)
    - _Requirements: 13.5, 13.11, 10.8, 13.7, 13.8, 13.12, 10.9_

  - [x] 6.12 Update `TransitionValidator` for enum-based validation
    - Simplify: enum membership replaces string-based undefined-state/trigger checks
    - Preserve duplicate transition detection (SMSG001)
    - Preserve unreachable state detection (SMSG009) for non-initial enum members
    - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5_

  - [x] 6.13 Update `SignatureValidator` for entry callback and cleanup handler signatures
    - Validate targeted [OnEnter] signature: `(TState, TEvent) → TState`
    - Validate catch-all [OnEnter] signature: `(TState, TEvent) → void`
    - Validate [OnTerminal] signature: `(TState) → Task`
    - _Requirements: 13.7, 13.8, 13.12, 10.9_

  - [x] 6.14 Update `AnalysisPipeline` to wire new validators and build updated `ValidatedStateMachine`
    - Add `EnumValidator` and `EntryCallbackValidator` to validation chain
    - Update `BuildValidatedModel` to populate new fields (enum values, terminal flag, entry callbacks, cleanup handler)
    - _Requirements: All analysis requirements_

- [x] 7. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 8. Generation stage: Tests first, then implementation
  - [x] 8.1 Write property test: Generated dispatch uses enum comparisons (Property 10)
    - **Property 10: Generated dispatch uses enum comparisons**
    - Generated code contains enum member references and no string literal comparisons for routing
    - **Validates: Requirements 7.1, 7.2, 7.3, 7.4**

  - [x] 8.2 Write property test: Non-terminal orchestration ordering (Property 11)
    - **Property 11: Non-terminal orchestration ordering**
    - Generated code follows: lock → guard → handler → targeted entry → catch-all entry → persist → side-effect → unlock
    - **Validates: Requirements 7.5, 13.6, 13.9**

  - [x] 8.3 Write property test: Terminal orchestration ordering (Property 12)
    - **Property 12: Terminal orchestration ordering**
    - Generated code follows: lock → guard → handler → targeted entry → catch-all entry → persist → cleanup → unlock (no side-effect)
    - **Validates: Requirements 10.5, 10.10**

  - [x] 8.4 Write property test: Targeted entry callback returns TState (Property 15)
    - **Property 15: Targeted entry callback returns TState**
    - Generated code uses return value of targeted entry callback as state for subsequent operations
    - **Validates: Requirements 13.7, 13.8**

  - [x] 8.5 Write property test: Catch-all entry callback is void and observational (Property 16)
    - **Property 16: Catch-all entry callback is void and observational**
    - Catch-all [OnEnter] has void return, invoked after targeted callback without using return value
    - **Validates: Requirements 13.12, 13.3**

  - [x] 8.6 Write property test: Terminal transition without cleanup completes normally (Property 17)
    - **Property 17: Terminal transition without cleanup completes normally**
    - Terminal transition with no [OnTerminal] handler completes (handler → entry → persist) and returns Success
    - **Validates: Requirements 10.6**

  - [x] 8.7 Write property test: Generated code compiles without errors (Property 18)
    - **Property 18: Generated code compiles without errors**
    - For any valid ValidatedStateMachine, generation produces compilable C# source
    - **Validates: Requirements 7.1, 7.2, 7.3, 7.4, 7.5, 7.6**

  - [x] 8.8 Rewrite `EventDispatchEmitter` for enum-based switch dispatch
    - Emit `switch (eventId)` with enum case labels (e.g., `case OrderEventId.Confirm:`)
    - Emit state comparisons using `currentState.GetStateId() == StateId.Pending` pattern
    - Remove all string literal comparisons
    - _Requirements: 7.1, 7.2, 7.3, 7.4_

  - [x] 8.9 Update `OrchestrationEmitter` for new ordering with entry callbacks
    - Non-terminal: guard → handler → targeted entry → catch-all entry → persist → side-effect
    - Terminal: guard → handler → targeted entry → catch-all entry → persist → cleanup (no side-effect)
    - Emit `TransitionResult<TState>.Succeeded(newState)` / `.GuardRejected` / `.NoTransition`
    - _Requirements: 7.5, 7.6, 10.5, 10.10, 13.6, 13.9_

  - [x] 8.10 Implement `EntryCallbackEmitter` (new emitter)
    - Emit targeted entry callback invocation (uses return value as new state)
    - Emit catch-all entry callback invocation (void, observational)
    - Support both sync and async signatures
    - _Requirements: 13.2, 13.3, 13.6, 13.7, 13.8, 13.12_

  - [x] 8.11 Implement `CleanupEmitter` (new emitter)
    - Emit async cleanup handler invocation for terminal-state transitions
    - Emit `await OnTerminalCleanup(newState).ConfigureAwait(false)` pattern
    - _Requirements: 10.5, 10.9, 10.10_

  - [x] 8.12 Update `HandleMethodEmitter` for enum types and `TransitionResult<TState>` return type
    - Update method signature to return `Task<TransitionResult<TState>>`
    - Use enum types in dispatch logic
    - Wire `EventDispatchEmitter`, `OrchestrationEmitter`, `EntryCallbackEmitter`, `CleanupEmitter`
    - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5_

  - [x] 8.13 Update `GenerationPipeline` to wire new emitters
    - Integrate `EntryCallbackEmitter` and `CleanupEmitter` into the generation pipeline
    - _Requirements: All generation requirements_

- [x] 9. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 10. Pipeline wiring and integration
  - [x] 10.1 Update `StateMachineGenerator` incremental pipeline for new parsing flow
    - Update `SyntaxExtractor.HasStateMachineAttributes` predicate for new attributes
    - Ensure incremental caching works correctly with new data model shapes
    - _Requirements: 1.1, 1.6_

  - [x] 10.2 Write integration tests: Full pipeline compilation (Property 18 end-to-end)
    - **Property 18: Generated code compiles without errors (end-to-end)**
    - In-memory Roslyn compilation tests verifying complete pipeline produces valid C#
    - Test with various enum configurations, guards, side-effects, entry callbacks, cleanup handlers
    - **Validates: Requirements 7.1, 7.2, 7.3, 7.4, 7.5, 7.6**

- [x] 11. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 12. Playground migration and documentation
  - [x] 12.1 Migrate `OrderMachine` playground to new generic enum-based API
    - Define `OrderStateId` and `OrderEventId` enums
    - Update `OrderState` and `OrderEvent` types with proper interface implementations
    - Rewrite `OrderMachine` class with four type parameters and int-cast attributes
    - Add `[InitialState]` and `[TerminalState]` declarations
    - _Requirements: 11.6_

  - [x] 12.2 Add guard example to playground
    - Add a guard method demonstrating conditional transition blocking (e.g., cannot ship if no items)
    - Exercise guard rejection path in `Program.cs` with printed output
    - _Requirements: 11.1, 11.2, 11.5_

  - [x] 12.3 Add side-effect example to playground
    - Add a side-effect method demonstrating post-transition logic (e.g., notification/logging)
    - Exercise successful side-effect path in `Program.cs` with printed output
    - _Requirements: 11.3, 11.4, 11.5_

  - [x] 12.4 Update README with guards and side-effects documentation
    - Add dedicated section explaining guard purpose and behavior
    - Add code example showing guard declaration and rejection behavior
    - Add dedicated section explaining side-effect purpose and behavior
    - Add code example showing side-effect declaration
    - Document orchestration ordering
    - Document method signature requirements for guards and side-effects
    - Update NuGet package description to reference guards and side-effects
    - _Requirements: 12.1, 12.2, 12.3, 12.4, 12.5, 12.6, 12.7, 12.8_

  - [x] 12.5 Update README with new generic API documentation
    - Replace string-based examples with enum-based examples
    - Document four type parameters and constraints
    - Document `[InitialState]`, `[TerminalState]`, `[OnEnter]`, `[OnTerminal]` attributes
    - Update diagnostics table with SMSG018–SMSG027
    - Remove references to `[State]` and `[Trigger]` attributes
    - _Requirements: 1.1, 1.2, 1.3, 3.1, 4.1, 10.1, 13.1_

- [x] 13. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- TDD methodology: property tests (tasks 4.1–4.3, 6.1–6.8, 8.1–8.7) are written before their corresponding implementations — these are mandatory per project constitution
- The old `[State]`/`[Trigger]` API is removed entirely — no backward compatibility path needed
- All 18 correctness properties from the design document have corresponding test tasks
