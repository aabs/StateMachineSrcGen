# Implementation Plan: State Machine Source Generator

## Overview

This plan implements the StateMachineSrcGen Roslyn Incremental Source Generator following strict TDD: tests are written before implementation at every step. The work is organized by pipeline stage (Data Models → Parsing → Analysis → Generation → Orchestration → Pipeline Wiring → Integration), with each task building incrementally on previous tasks. Property-based tests (FsCheck) validate correctness properties from the design; unit tests cover specific edge cases.

## Tasks

- [ ] 1. Project scaffolding and infrastructure
  - [ ] 1.1 Create solution structure and project files
    - Create `src/StateMachineSrcGen/StateMachineSrcGen.csproj` targeting netstandard2.0 with Roslyn dependencies
    - Create `src/StateMachineSrcGen.Attributes/StateMachineSrcGen.Attributes.csproj` targeting netstandard2.0 with NO Roslyn dependency
    - Create `src/StateMachineSrcGen.Tests/StateMachineSrcGen.Tests.csproj` targeting net9.0 with xUnit + FsCheck.Xunit references
    - Update `StateMachineSrcGen.slnx` to include all three projects
    - Create directory structure: Parsing/, Analysis/, Generation/, Diagnostics/, Pipeline/ in generator project
    - Create directory structure: Parsing/, Analysis/, Generation/, DataModel/, Orchestration/, Integration/ in test project
    - _Requirements: 12.1, 12.2, 12.3_

  - [ ] 1.2 Create marker attributes in the Attributes assembly
    - Implement `TransitionAttribute` (From, To, Trigger, EventId properties)
    - Implement `GuardAttribute` (From, To, Trigger)
    - Implement `SideEffectAttribute` (From, To, Trigger)
    - Implement `StateAttribute` (Name, IsInitial)
    - Implement `TriggerAttribute` (Name)
    - _Requirements: 1.1, 12.1, 12.2_

  - [ ] 1.3 Create user-facing interfaces in the Attributes assembly
    - Implement `IStateMachine<TState, TEvent>` with `Task<TransitionResult> HandleAsync(TEvent @event)`
    - Implement `IStatePersistence<TState>` with `LoadAsync()` and `SaveAsync(TState)`
    - Implement `IStateLock<TState>` with `AcquireAsync()` and `ReleaseAsync()`
    - Implement `IDispatchableEvent<TEventId>` with `GetEventId()`
    - Implement `TransitionResult` enum (Success, NotHandled, LockFailed)
    - _Requirements: 5.2, 6.1, 6.2, 15.1, 15.2, 15.9, 16.1, 16.2_

  - [ ] 1.4 Create diagnostic descriptors
    - Implement `DiagnosticDescriptors` static class with all SMSG001–SMSG016 descriptors
    - Each descriptor has unique ID, severity, message format, and category
    - _Requirements: 8.3, 8.4_

- [ ] 2. Data models and value equality
  - [ ] 2.1 Write property tests for data model equality
    - Create `src/StateMachineSrcGen.Tests/DataModel/EqualityProperties.cs`
    - **Property 24: Data model value equality**
    - Test that identical `ParsedStateMachine` instances are equal and differing instances are not equal
    - Test equality for `ParsedState`, `ParsedTrigger`, `ParsedHandler`, `ValidatedStateMachine`, `ValidatedState`, `ValidatedTransition`
    - **Validates: Requirements 10.2**

  - [ ] 2.2 Write property tests for EquatableArray
    - Create `src/StateMachineSrcGen.Tests/DataModel/EquatableArrayProperties.cs`
    - Test element-wise equality, inequality on different elements, inequality on different lengths
    - Test `GetHashCode` consistency with equality
    - Test `IEnumerable<T>` implementation
    - _Requirements: 10.2_

  - [ ] 2.3 Implement EquatableArray<T>
    - Create `src/StateMachineSrcGen/EquatableArray.cs`
    - Implement `IEquatable<EquatableArray<T>>` with element-wise comparison
    - Implement `IEnumerable<T>` for iteration
    - Implement `GetHashCode` combining element hashes
    - _Requirements: 10.2_

  - [ ] 2.4 Implement pipeline data models
    - Create `ParsedStateMachine`, `ParsedState`, `ParsedTrigger`, `ParsedHandler` as readonly record structs
    - Create `MethodSignature`, `ParameterInfo` as readonly record structs
    - Create `ClassModifiers` flags enum and `HandlerKind` enum
    - Create `ValidatedStateMachine`, `ValidatedState`, `ValidatedTransition` as readonly record structs
    - All models use `EquatableArray<T>` for collection fields
    - _Requirements: 10.2, 11.1, 11.2, 11.3_

- [ ] 3. Checkpoint — Verify foundation
  - Ensure all tests pass (`dotnet test`), ask the user if questions arise.

- [ ] 4. Parsing stage — tests first
  - [ ] 4.1 Write property tests for handler attribute parsing
    - Create `src/StateMachineSrcGen.Tests/Parsing/HandlerParsingProperties.cs`
    - **Property 1: Parsing extracts handler attributes correctly**
    - For any method with `[Transition]` attribute, verify `ParsedHandler.FromState`, `ToState`, `Trigger` match attribute parameters
    - **Validates: Requirements 1.1**

  - [ ]* 4.2 Write property tests for class declaration validation
    - Create tests in `src/StateMachineSrcGen.Tests/Parsing/HandlerParsingProperties.cs`
    - **Property 27: Class declaration validation**
    - Test that missing public/partial/static modifiers or wrong generic parameter count emits SMSG010
    - **Validates: Requirements 14.1, 14.2**

  - [ ]* 4.3 Write property tests for IStateMachine implementation validation
    - Add tests in `src/StateMachineSrcGen.Tests/Parsing/HandlerParsingProperties.cs`
    - **Property 28: IStateMachine implementation validation**
    - Test detection of missing or incorrectly parameterized IStateMachine implementation (SMSG011)
    - **Validates: Requirements 14.3, 14.4**

  - [ ]* 4.4 Write property tests for handler signature validation
    - Add tests in `src/StateMachineSrcGen.Tests/Parsing/HandlerParsingProperties.cs`
    - **Property 29: Handler signature validation**
    - Test that non-public, non-static, wrong parameters, or wrong return type emits SMSG012
    - **Validates: Requirements 14.5, 14.6**

  - [ ]* 4.5 Write property tests for IStatePersistence validation
    - Add tests in `src/StateMachineSrcGen.Tests/Parsing/HandlerParsingProperties.cs`
    - **Property 30: IStatePersistence implementation validation**
    - Test detection of missing IStatePersistence implementation (SMSG013)
    - **Validates: Requirements 14.7, 14.8**

  - [ ]* 4.6 Write property tests for IDispatchableEvent validation
    - Create `src/StateMachineSrcGen.Tests/Parsing/DispatchInterfaceParsingProperties.cs`
    - **Property 34: Missing IDispatchableEvent detection**
    - Test that missing IDispatchableEvent on event type is detected
    - **Validates: Requirements 16.5, 16.6**

  - [ ]* 4.7 Write property tests for parsing reliability
    - Create `src/StateMachineSrcGen.Tests/Parsing/ParsingReliabilityProperties.cs`
    - **Property 23: Pipeline stages never throw (parsing stage)**
    - Test that malformed, null-containing, or edge-case inputs never throw unhandled exceptions
    - **Validates: Requirements 9.1, 9.2, 9.3, 9.4**

  - [ ]* 4.8 Write property tests for parsing determinism
    - Create `src/StateMachineSrcGen.Tests/Parsing/ParsingDeterminismProperties.cs`
    - **Property 25: Pipeline stage determinism (parsing stage)**
    - Test that invoking parsing multiple times with same input produces identical output
    - **Validates: Requirements 11.1**

  - [ ] 4.9 Implement SyntaxExtractor
    - Create `src/StateMachineSrcGen/Parsing/SyntaxExtractor.cs`
    - Filter syntax nodes with `[State]`, `[Trigger]`, `[Transition]`, `[Guard]`, `[SideEffect]` attributes
    - Return candidate class declarations for further parsing
    - _Requirements: 1.1, 14.1_

  - [ ] 4.10 Implement DeclarationParser
    - Create `src/StateMachineSrcGen/Parsing/DeclarationParser.cs`
    - Extract class modifiers (public, partial, static), generic type parameters
    - Detect IStateMachine and IStatePersistence interface implementations
    - Detect IDispatchableEvent implementation on event type and extract TEventId
    - _Requirements: 14.1, 14.3, 14.7, 16.5_

  - [ ] 4.11 Implement HandlerParser
    - Create `src/StateMachineSrcGen/Parsing/HandlerParser.cs`
    - Extract method signatures, attribute parameters (From, To, Trigger, EventId)
    - Classify handlers by kind (Transition, Guard, SideEffect)
    - Validate handler signatures (public, static, correct parameters and return type)
    - _Requirements: 1.1, 1.2, 14.5, 14.6_

  - [ ] 4.12 Implement ParsedModelFactory
    - Create `src/StateMachineSrcGen/Parsing/ParsedModelFactory.cs`
    - Assemble extracted data into `ParsedStateMachine` value type
    - Wrap all parsing logic in try-catch, emit SMSG015 on unexpected errors
    - _Requirements: 9.1, 11.1_

- [ ] 5. Checkpoint — Parsing stage complete
  - Ensure all tests pass (`dotnet test`), ask the user if questions arise.

- [ ] 6. Analysis stage — tests first
  - [ ] 6.1 Write property tests for duplicate detection
    - Create `src/StateMachineSrcGen.Tests/Analysis/DuplicateDetectionProperties.cs`
    - **Property 2: Duplicate handler detection** — same (From, To, Trigger) triple emits SMSG001
    - **Property 6: Duplicate state name detection** — same state name emits SMSG007
    - **Property 7: Duplicate trigger name detection** — same trigger name emits SMSG008
    - **Validates: Requirements 1.3, 2.4, 2.5**

  - [ ]* 6.2 Write property tests for reference validation
    - Create `src/StateMachineSrcGen.Tests/Analysis/ReferenceValidationProperties.cs`
    - **Property 3: Undefined state or trigger reference detection** — handler referencing undeclared state/trigger emits error
    - **Property 4: Empty state set rejection** — empty States collection emits SMSG004
    - **Property 5: Initial state cardinality enforcement** — zero initial states emits SMSG005, multiple emits SMSG006
    - **Validates: Requirements 1.4, 2.1, 2.2, 2.3**

  - [ ]* 6.3 Write property tests for reachability analysis
    - Create `src/StateMachineSrcGen.Tests/Analysis/ReachabilityProperties.cs`
    - **Property 8: Unreachable state warning** — non-initial state with no inbound transition emits SMSG009
    - **Property 9: Terminal states are valid** — states with no outbound transitions produce no diagnostic
    - **Validates: Requirements 2.6, 2.7**

  - [ ]* 6.4 Write property tests for error emission behavior
    - Create `src/StateMachineSrcGen.Tests/Analysis/ErrorEmissionProperties.cs`
    - **Property 21: Errors prevent source emission** — any Error-severity diagnostic means zero source files emitted
    - **Property 22: Diagnostics include message and location** — every diagnostic has non-empty message and non-null location
    - **Validates: Requirements 8.1, 8.3, 8.5**

  - [ ]* 6.5 Write property tests for analysis reliability
    - Create `src/StateMachineSrcGen.Tests/Analysis/AnalysisReliabilityProperties.cs`
    - **Property 23: Pipeline stages never throw (analysis stage)**
    - Test that any input (including malformed) never throws unhandled exceptions
    - **Validates: Requirements 9.1, 9.2, 9.3, 9.4**

  - [ ]* 6.6 Write property tests for analysis determinism
    - Create `src/StateMachineSrcGen.Tests/Analysis/AnalysisDeterminismProperties.cs`
    - **Property 25: Pipeline stage determinism (analysis stage)**
    - Test that invoking analysis multiple times with same input produces identical output
    - **Validates: Requirements 11.2**

  - [ ] 6.7 Implement StructureValidator
    - Create `src/StateMachineSrcGen/Analysis/StructureValidator.cs`
    - Validate class shape: public, partial, static, two generic type parameters
    - Validate IStateMachine and IStatePersistence implementations
    - Validate IDispatchableEvent implementation on event type
    - Emit SMSG010, SMSG011, SMSG013, SMSG016 as appropriate
    - _Requirements: 14.1–14.8, 16.5, 16.6_

  - [ ] 6.8 Implement StateValidator
    - Create `src/StateMachineSrcGen/Analysis/StateValidator.cs`
    - Check for empty states (SMSG004), initial state cardinality (SMSG005/SMSG006)
    - Check for duplicate state names (SMSG007)
    - Detect unreachable states (SMSG009)
    - _Requirements: 2.1, 2.2, 2.4, 2.6_

  - [ ] 6.9 Implement TriggerValidator
    - Create `src/StateMachineSrcGen/Analysis/TriggerValidator.cs`
    - Check for duplicate trigger names (SMSG008)
    - _Requirements: 2.5_

  - [ ] 6.10 Implement TransitionValidator
    - Create `src/StateMachineSrcGen/Analysis/TransitionValidator.cs`
    - Check for duplicate handlers (SMSG001)
    - Check for undefined state/trigger references (SMSG002/SMSG003)
    - Check for missing target states (SMSG014)
    - _Requirements: 1.3, 1.4, 2.3_

  - [ ] 6.11 Implement SignatureValidator
    - Create `src/StateMachineSrcGen/Analysis/SignatureValidator.cs`
    - Validate handler method signatures match expected patterns (SMSG012)
    - _Requirements: 14.5, 14.6_

  - [ ] 6.12 Implement AnalysisPipeline (orchestrates validators)
    - Create `src/StateMachineSrcGen/Analysis/AnalysisPipeline.cs`
    - Orchestrate all validators, collect diagnostics
    - Build `ValidatedStateMachine` from `ParsedStateMachine` when no errors
    - Identify terminal states (no outbound transitions)
    - Wrap in try-catch, emit SMSG015 on unexpected errors
    - Return empty result on any Error-severity diagnostic (no partial output)
    - _Requirements: 2.7, 8.1, 8.5, 9.2, 11.2_

- [ ] 7. Checkpoint — Analysis stage complete
  - Ensure all tests pass (`dotnet test`), ask the user if questions arise.

- [ ] 8. Generation stage — tests first
  - [ ]* 8.1 Write property tests for compilation correctness
    - Create `src/StateMachineSrcGen.Tests/Generation/CompilationProperties.cs`
    - **Property 17: Generated code compiles without warnings**
    - For any valid `ValidatedStateMachine`, generated C# compiles without errors or warnings under nullable context
    - **Property 26: Full pipeline round-trip compilation**
    - Running full pipeline and compiling output in-memory produces a valid .NET assembly
    - **Validates: Requirements 7.1, 11.4**

  - [ ]* 8.2 Write property tests for code quality
    - Create `src/StateMachineSrcGen.Tests/Generation/CodeQualityProperties.cs`
    - **Property 18: No reflection in generated code** — no System.Reflection, dynamic, Type.GetMethod references
    - **Property 19: XML documentation on public members** — all public members have `///` comments
    - **Property 20: Partial class emission** — generated class includes `partial` keyword
    - **Validates: Requirements 7.2, 7.3, 7.5**

  - [ ]* 8.3 Write property tests for generation reliability
    - Create `src/StateMachineSrcGen.Tests/Generation/GenerationReliabilityProperties.cs`
    - **Property 23: Pipeline stages never throw (generation stage)**
    - Test that any valid input never throws unhandled exceptions
    - **Validates: Requirements 9.3, 9.4**

  - [ ]* 8.4 Write property tests for generation determinism
    - Create `src/StateMachineSrcGen.Tests/Generation/GenerationDeterminismProperties.cs`
    - **Property 25: Pipeline stage determinism (generation stage)**
    - Test that invoking generation multiple times with same input produces identical output
    - **Validates: Requirements 11.3**

  - [ ] 8.5 Implement HandleMethodEmitter
    - Create `src/StateMachineSrcGen/Generation/HandleMethodEmitter.cs`
    - Generate the `HandleAsync` method body with lock acquire, load, dispatch, save, release pattern
    - _Requirements: 3.1, 4.1, 4.2, 4.3, 5.1_

  - [ ] 8.6 Implement EventDispatchEmitter
    - Create `src/StateMachineSrcGen/Generation/EventDispatchEmitter.cs`
    - Generate switch/case block over `@event.GetEventId()` routing to per-handler methods
    - Generate state+eventId matching logic
    - _Requirements: 16.3, 16.4, 16.7, 16.8_

  - [ ] 8.7 Implement TransitionEvaluator emitter
    - Create `src/StateMachineSrcGen/Generation/TransitionEvaluatorEmitter.cs`
    - Generate guard evaluation in declaration order
    - Generate first-match selection logic
    - Generate NotHandled fallthrough
    - _Requirements: 3.2, 3.3, 3.4, 3.5_

  - [ ] 8.8 Implement PersistenceEmitter
    - Create `src/StateMachineSrcGen/Generation/PersistenceEmitter.cs`
    - Generate default `InMemoryPersistence<TState>` class
    - _Requirements: 5.4, 6.3_

  - [ ] 8.9 Implement LockEmitter
    - Create `src/StateMachineSrcGen/Generation/LockEmitter.cs`
    - Generate default `NoOpLock<TState>` class
    - _Requirements: 15.7_

  - [ ] 8.10 Implement OrchestrationEmitter
    - Create `src/StateMachineSrcGen/Generation/OrchestrationEmitter.cs`
    - Generate the load→guard→action→save→sideeffect protocol with try/finally for lock release
    - Generate exception propagation without state save on action failure
    - Generate exception propagation after save on side effect failure
    - _Requirements: 4.4, 4.5, 5.1, 5.3, 5.6, 5.7, 15.4, 15.5, 15.6_

  - [ ] 8.11 Implement SourceFormatter
    - Create `src/StateMachineSrcGen/Generation/SourceFormatter.cs`
    - Apply consistent formatting, XML doc comments on public members
    - Emit `partial class` declarations with correct namespace
    - _Requirements: 7.1, 7.3, 7.4, 7.5_

  - [ ] 8.12 Implement GenerationPipeline (orchestrates emitters)
    - Create `src/StateMachineSrcGen/Generation/GenerationPipeline.cs`
    - Orchestrate all emitters to produce complete source text
    - Wrap in try-catch, emit SMSG015 on unexpected errors
    - _Requirements: 7.1, 7.2, 9.3, 11.3_

- [ ] 9. Checkpoint — Generation stage complete
  - Ensure all tests pass (`dotnet test`), ask the user if questions arise.

- [ ] 10. Orchestration behavior — tests first
  - [ ]* 10.1 Write property tests for transition dispatch correctness
    - Create `src/StateMachineSrcGen.Tests/Orchestration/EventDispatchProperties.cs`
    - **Property 10: Transition dispatch correctness** — correct transitions selected for (state, trigger) pair
    - **Property 33: Event dispatch extraction via GetEventId** — GetEventId() routes to correct handler based on EventId
    - **Property 35: Exhaustive dispatch with NotHandled fallthrough** — unmatched event IDs return NotHandled
    - **Validates: Requirements 3.1, 3.3, 16.1, 16.3, 16.4, 16.7, 16.8**

  - [ ]* 10.2 Write property tests for guard-gated transition selection
    - Create `src/StateMachineSrcGen.Tests/Orchestration/OrchestrationOrderProperties.cs`
    - **Property 11: Guard-gated transition selection** — guards evaluated in declaration order, first true wins
    - **Property 12: Orchestration protocol ordering** — acquire→load→guard→action→save→sideeffect→release order
    - **Validates: Requirements 3.2, 3.4, 3.5, 4.1, 4.2, 4.3, 5.1**

  - [ ]* 10.3 Write property tests for failure handling
    - Create `src/StateMachineSrcGen.Tests/Orchestration/FailureHandlingProperties.cs`
    - **Property 13: Action failure prevents state persistence** — exception in handler skips save
    - **Property 14: Side effect failure does not roll back state** — save already completed before side effect
    - **Property 15: State round-trip through persistence** — handler receives exact loaded state, save receives exact returned state
    - **Property 16: Load failure short-circuits orchestration** — load exception skips handler and save
    - **Validates: Requirements 4.4, 4.5, 5.3, 5.6, 5.7**

  - [ ]* 10.4 Write property tests for lock lifecycle
    - Create `src/StateMachineSrcGen.Tests/Orchestration/LockProperties.cs`
    - **Property 31: Lock lifecycle correctness** — lock acquired before operations, released after (or on failure)
    - **Property 32: Lock acquisition failure prevents transition** — failed acquire returns LockFailed, no further work
    - **Validates: Requirements 15.4, 15.5, 15.6**

- [ ] 11. Pipeline wiring and incremental generator
  - [ ] 11.1 Implement the IIncrementalGenerator pipeline orchestrator
    - Create `src/StateMachineSrcGen/Pipeline/StateMachineGenerator.cs`
    - Implement `IIncrementalGenerator.Initialize` with `SyntaxProvider` filtering
    - Wire Parsing → Analysis → Generation stages
    - Register source output and diagnostic reporting
    - Use `[Generator]` attribute
    - _Requirements: 10.1, 10.3_

  - [ ] 11.2 Implement incremental caching integration
    - Ensure pipeline data models enable correct caching via value equality
    - Use `RegisterSourceOutput` with proper `IncrementalValuesProvider` transforms
    - Verify unrelated file changes don't trigger re-execution
    - _Requirements: 10.1, 10.2, 10.3_

- [ ] 12. Checkpoint — Pipeline wiring complete
  - Ensure all tests pass (`dotnet test`), ask the user if questions arise.

- [ ] 13. Integration tests
  - [ ]* 13.1 Write end-to-end compilation integration tests
    - Create `src/StateMachineSrcGen.Tests/Integration/EndToEndCompilationTests.cs`
    - Test full pipeline with valid state machine definitions compiling in-memory
    - Test that generated code produces valid assemblies
    - Test multiple state machines in same compilation
    - _Requirements: 11.4, 7.1_

  - [ ]* 13.2 Write incremental cache integration tests
    - Create `src/StateMachineSrcGen.Tests/Integration/IncrementalCacheTests.cs`
    - Test that unrelated file changes don't trigger re-execution
    - Test that changes to state machine definition do trigger re-execution
    - _Requirements: 10.1, 10.3_

  - [ ]* 13.3 Write NuGet packaging validation tests
    - Create `src/StateMachineSrcGen.Tests/Integration/NuGetPackagingTests.cs`
    - Verify attributes assembly has no Roslyn dependency
    - Verify generator assembly is correctly packaged as analyzer
    - _Requirements: 12.1, 12.2, 12.3_

  - [ ]* 13.4 Write snapshot/approval tests for generated output stability
    - Create `src/StateMachineSrcGen.Tests/Generation/Snapshots/` with approval test files
    - Verify generated output matches expected snapshots for representative state machines
    - _Requirements: 7.4_

- [ ] 14. FsCheck custom generators
  - [ ] 14.1 Implement FsCheck Arbitrary generators for pipeline data models
    - Create `src/StateMachineSrcGen.Tests/Generators/` directory
    - Implement `Arbitrary<ParsedStateMachine>` (valid and invalid variants)
    - Implement `Arbitrary<ParsedState>`, `Arbitrary<ParsedTrigger>`, `Arbitrary<ParsedHandler>`
    - Implement `Arbitrary<ValidatedStateMachine>` (always structurally valid)
    - Implement `Arbitrary<ClassModifiers>`, `Arbitrary<MethodSignature>`
    - Include controlled name collisions and EventId value generation
    - _Requirements: 11.1, 11.2, 11.3_

- [ ] 15. NuGet packaging and CI/CD
  - [ ] 15.1 Configure NuGet package metadata and packaging
    - Add NuGet metadata to generator .csproj (PackageId, Description, Authors, License, etc.)
    - Configure generator as analyzer in NuGet package (IncludeBuildOutput, BuildOutputTargetFolder)
    - Configure attributes assembly as dependency
    - Ensure package auto-configures without manual project file edits
    - _Requirements: 12.4, 13.2_

  - [ ] 15.2 Create GitHub Actions release workflow
    - Create `.github/workflows/release.yml`
    - Trigger on `v*` tag push to main branch only
    - Steps: restore → build (Release) → test → pack → push to nuget.org
    - Derive version from tag (strip `v` prefix)
    - Support stable and pre-release versions
    - Use repository secret for nuget.org authentication
    - Halt on any build/test failure
    - _Requirements: 13.1, 13.2, 13.3, 13.4, 13.5, 13.6, 13.7_

- [ ] 16. Final checkpoint — Full integration verification
  - Ensure all tests pass (`dotnet test`), ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- TDD order is enforced: test tasks precede their corresponding implementation tasks
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation at each pipeline stage boundary
- Property tests validate universal correctness properties from the design document
- FsCheck custom generators (task 14) can be implemented earlier if needed by property test tasks — they are placed late to allow iterative refinement as the pipeline takes shape
- The orchestration tests (task 10) validate the behavior of generated code by compiling and executing it in-memory
