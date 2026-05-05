# Requirements Document

## Introduction

StateMachineSrcGen is a .NET Incremental Source Generator (NuGet package) that examines concise, declarative state machine definitions written by the user in C# and generates the complete boilerplate code needed to validate the state machine structure, evaluate transitions, invoke custom transition logic, and persist state. The user writes minimal fragments; the generator produces everything else at compile time with no runtime reflection.

## Glossary

- **Generator**: The Roslyn Incremental Source Generator (`IIncrementalGenerator`) that analyzes user code and emits generated source files.
- **State_Machine_Definition**: A user-authored C# class or partial class decorated with marker attributes that declares states, triggers, and transitions.
- **State**: A named, distinct condition of the state machine model (e.g., `Idle`, `Processing`, `Completed`).
- **Trigger**: A named event that may cause a transition from one state to another.
- **Transition**: A directed edge from a source State to a target State, activated by a specific Trigger.
- **Guard**: A user-defined pure function that returns a boolean indicating whether a Transition is permitted.
- **Action**: A user-defined function invoked when a Transition fires successfully.
- **Side_Effect**: A user-defined function invoked for external operations (logging, notifications) during a Transition.
- **Transition_Handler**: A user-defined pure function marked with attributes indicating it handles the transition from State A to State B under Trigger C.
- **State_Persistence_Provider**: A user-implementable interface responsible for loading (rehydrating) and saving (persisting) the current state of the model. The interface is generic over the user's full state type (not just a state enum).
- **State_Lock_Provider**: The user-implementable interface responsible for acquiring and releasing locks around state transitions to ensure thread safety and distributed concurrency.
- **Diagnostic**: A Roslyn compiler warning or error emitted by the Generator to report problems in the State_Machine_Definition.
- **Release_Workflow**: The GitHub Actions workflow responsible for building, testing, packing, and publishing the NuGet package to nuget.org when triggered by a version tag.
- **Pipeline**: The sequence of processing stages (Parsing → Analysis → Generation) within the Generator.
- **Parsing_Stage**: The Pipeline stage that extracts syntactic information from user source code into an intermediate model.
- **Analysis_Stage**: The Pipeline stage that validates the semantic correctness of the extracted model.
- **Generation_Stage**: The Pipeline stage that emits C# source code from a validated model.
- **Incremental_Cache**: Roslyn's built-in mechanism for avoiding recomputation when unrelated source files change.
- **IStateMachine**: The generic interface `IStateMachine<StateType, EventType>` that a State_Machine_Definition class must implement, requiring a `Handle(EventType @event)` method as the main entry point for processing events.
- **IStatePersistence**: The generic interface `IStatePersistence<State>` that the user must implement to provide the generated boilerplate with a mechanism for loading the initial state and saving the new state after a Transition.
- **Transition_Attribute**: The `[Transition]` marker attribute applied to static methods within the State_Machine_Definition class to designate them as Transition_Handler methods.
- **IDispatchableEvent**: The generic interface `IDispatchableEvent<TEventId>` that the user's event type must implement, exposing a `GetEventId()` method that returns the dispatch identifier used by the generated routing logic.
- **Event_ID**: The value returned by `GetEventId()` on the event payload, used by the generated switch/dispatch logic to route events to the correct Transition_Handler.

## Requirements

### Requirement 1: Transition Handler Declaration

**User Story:** As a developer, I want to define a pure function and mark it as the handler for a specific state transition (from State A to State B under Trigger C), so that the generator knows how to wire my custom logic into the state machine.

#### Acceptance Criteria

1. WHEN a method is decorated with a transition handler attribute specifying source state, target state, and trigger, THE Generator SHALL recognize that method as a Transition_Handler for the specified Transition.
2. THE Generator SHALL support Transition_Handler methods that accept the current model context and return a result indicating success or failure.
3. WHEN multiple Transition_Handler methods are declared for the same source state, target state, and trigger combination, THE Generator SHALL emit a Diagnostic error indicating a duplicate handler.
4. WHEN a Transition_Handler references a State or Trigger not declared in the State_Machine_Definition, THE Generator SHALL emit a Diagnostic error identifying the undefined State or Trigger.

### Requirement 2: State Machine Definition and Validation

**User Story:** As a developer, I want the generator to verify the structural correctness of my state machine definition at compile time, so that I receive immediate feedback on invalid configurations.

#### Acceptance Criteria

1. WHEN a State_Machine_Definition is provided, THE Analysis_Stage SHALL verify that at least one State is declared.
2. WHEN a State_Machine_Definition is provided, THE Analysis_Stage SHALL verify that exactly one initial State is designated.
3. WHEN a State_Machine_Definition contains a Transition referencing a target State not present in the definition, THE Generator SHALL emit a Diagnostic error identifying the missing target State.
4. WHEN a State_Machine_Definition contains duplicate State names, THE Generator SHALL emit a Diagnostic error identifying the duplicates.
5. WHEN a State_Machine_Definition contains duplicate Trigger names, THE Generator SHALL emit a Diagnostic error identifying the duplicates.
6. WHEN a State_Machine_Definition contains unreachable States (no inbound Transition and not the initial State), THE Generator SHALL emit a Diagnostic warning identifying the unreachable States.
7. WHEN a State_Machine_Definition contains States with no outbound Transitions (terminal states), THE Generator SHALL accept them as valid without emitting a Diagnostic.

### Requirement 3: Transition Evaluation

**User Story:** As a developer, I want the generated code to evaluate which transition to take based on the current state and the trigger received, so that my state machine behaves correctly at runtime.

#### Acceptance Criteria

1. WHEN a Trigger is applied to the generated state machine, THE generated code SHALL identify all Transitions matching the current State and the applied Trigger.
2. WHEN a matching Transition has a Guard, THE generated code SHALL invoke the Guard and proceed with the Transition only if the Guard returns true.
3. WHEN multiple Transitions match the current State and Trigger, THE generated code SHALL evaluate Guards in declaration order and select the first Transition whose Guard returns true.
4. WHEN no matching Transition exists for the current State and Trigger combination, THE generated code SHALL return a result indicating that the Trigger was not handled.
5. WHEN all matching Transitions have Guards that return false, THE generated code SHALL return a result indicating that the Trigger was not handled.

### Requirement 4: Custom Transition Code Invocation

**User Story:** As a developer, I want the generated code to invoke my guards, actions, and side effects during a transition, so that I can implement custom business logic without writing boilerplate.

#### Acceptance Criteria

1. WHEN a Transition fires, THE generated code SHALL invoke the associated Guard (if defined) before changing state.
2. WHEN a Transition fires and the Guard permits it, THE generated code SHALL invoke the associated Action before updating the persisted state.
3. WHEN a Transition fires and the Action completes, THE generated code SHALL invoke associated Side_Effect functions after the state has been updated.
4. IF an Action throws an exception, THEN THE generated code SHALL not update the state and SHALL propagate the exception to the caller.
5. IF a Side_Effect throws an exception, THEN THE generated code SHALL propagate the exception to the caller without rolling back the state change.

### Requirement 5: State Persistence and Orchestration Protocol

**User Story:** As a developer, I want the generated code to rehydrate the current state, apply a transition, and persist the new state following a strict orchestration protocol, so that my state machine survives across invocations and state transitions are applied consistently.

#### Acceptance Criteria

1. THE generated boilerplate SHALL orchestrate each state transition by executing the following four steps in order: (1) load the current full state object from the State_Persistence_Provider, (2) invoke the Transition_Handler supplying the loaded state and the event, (3) receive the new state object returned by the Transition_Handler, (4) save the new state object via the State_Persistence_Provider.
2. THE State_Persistence_Provider interface SHALL be generic over the user's full state type, enabling the persistence of rich state objects (not limited to a state enum, string, or integer).
3. THE generated code SHALL pass the complete user-defined state object (as loaded by the State_Persistence_Provider) to the Transition_Handler, and SHALL persist the complete state object returned by the Transition_Handler.
4. THE Generator SHALL emit a default State_Persistence_Provider implementation that stores the full state object in memory.
5. WHEN the user provides a custom implementation of the State_Persistence_Provider interface, THE generated code SHALL use the user-provided implementation instead of the default.
6. IF the State_Persistence_Provider fails to load the current state object, THEN THE generated code SHALL propagate the exception to the caller without invoking the Transition_Handler or attempting to save.
7. IF the Transition_Handler throws an exception, THEN THE generated code SHALL not invoke the State_Persistence_Provider save method and SHALL propagate the exception to the caller.

### Requirement 6: Persistence Provider Extensibility

**User Story:** As a developer, I want to provide my own implementation for state persistence (e.g., database, file, distributed cache), so that I can integrate the state machine with my infrastructure.

#### Acceptance Criteria

1. THE Generator SHALL emit an interface for the State_Persistence_Provider with methods for loading and saving state.
2. THE generated State_Persistence_Provider interface SHALL not depend on any specific storage technology.
3. THE Generator SHALL emit the default in-memory implementation as a separate class that the user can replace.
4. WHEN the user registers a custom State_Persistence_Provider via dependency injection or explicit assignment, THE generated code SHALL resolve and use that provider at runtime.

### Requirement 7: Code Generation Output Quality

**User Story:** As a developer, I want the generated code to be readable, debuggable, and free of runtime reflection, so that I can understand and troubleshoot the state machine behavior.

#### Acceptance Criteria

1. THE Generation_Stage SHALL emit C# source code that compiles without warnings under the nullable reference types context.
2. THE Generation_Stage SHALL emit code that uses no runtime reflection or dynamic dispatch.
3. THE Generation_Stage SHALL emit code with XML documentation comments on all public members.
4. THE Generation_Stage SHALL emit code formatted consistently with standard C# conventions.
5. THE Generation_Stage SHALL emit `partial class` declarations so that user code and generated code coexist in the same type.

### Requirement 8: Diagnostic Reporting

**User Story:** As a developer, I want clear, actionable compiler diagnostics when my state machine definition has problems, so that I can fix issues without consulting external documentation.

#### Acceptance Criteria

1. WHEN the Generator detects an error in the State_Machine_Definition, THE Generator SHALL emit a Diagnostic with severity Error that prevents code generation for that definition.
2. WHEN the Generator detects a potential issue (e.g., unreachable state), THE Generator SHALL emit a Diagnostic with severity Warning.
3. THE Generator SHALL include in each Diagnostic a message describing the problem and the location in source code where the problem was detected.
4. THE Generator SHALL assign a unique diagnostic ID (e.g., `SMSG001`) to each category of Diagnostic.
5. IF the State_Machine_Definition contains errors, THEN THE Generator SHALL not emit any generated source for that definition (no partial output).

### Requirement 9: Generator Reliability

**User Story:** As a developer, I want the source generator to never crash the compiler, so that my development experience remains stable even when my state machine definition is incomplete or malformed.

#### Acceptance Criteria

1. IF the Parsing_Stage encounters malformed syntax, THEN THE Generator SHALL emit a Diagnostic and cease processing that definition without throwing an exception.
2. IF the Analysis_Stage encounters an unexpected condition, THEN THE Generator SHALL emit a Diagnostic and cease processing that definition without throwing an exception.
3. IF the Generation_Stage encounters an internal error, THEN THE Generator SHALL emit a Diagnostic and produce no output for that definition without throwing an exception.
4. THE Generator SHALL handle null references, empty collections, and missing syntax nodes gracefully by emitting appropriate Diagnostics.

### Requirement 10: Incremental Pipeline Performance

**User Story:** As a developer, I want the generator to respond quickly to code changes and avoid unnecessary recomputation, so that my IDE remains responsive.

#### Acceptance Criteria

1. THE Pipeline SHALL use Roslyn's Incremental_Cache to avoid reprocessing State_Machine_Definitions that have not changed.
2. THE Pipeline data models SHALL implement value equality so that Roslyn's caching can correctly detect unchanged inputs.
3. WHEN a source file unrelated to any State_Machine_Definition changes, THE Generator SHALL not re-execute the Analysis_Stage or Generation_Stage.

### Requirement 11: Pipeline Stage Testability

**User Story:** As a developer of the generator, I want each pipeline stage to be an independently testable pure function, so that I can verify correctness with property-based tests.

#### Acceptance Criteria

1. THE Parsing_Stage SHALL be implemented as a pure function that accepts syntax tree data and returns a parsed model without side effects.
2. THE Analysis_Stage SHALL be implemented as a pure function that accepts a parsed model and returns a validated model or a collection of Diagnostics without side effects.
3. THE Generation_Stage SHALL be implemented as a pure function that accepts a validated model and returns generated source text without side effects.
4. FOR ALL valid parsed models, analyzing then generating then compiling SHALL produce a valid assembly (round-trip property).

### Requirement 12: Attribute-Only User Surface

**User Story:** As a consumer of the NuGet package, I want to reference only a lightweight attributes assembly without depending on Roslyn internals, so that my project remains clean and fast to compile.

#### Acceptance Criteria

1. THE Generator SHALL provide a separate attributes assembly (`StateMachineSrcGen.Attributes`) containing all marker attributes needed by the user.
2. THE attributes assembly SHALL NOT reference `Microsoft.CodeAnalysis` or any Roslyn packages.
3. THE attributes assembly SHALL target `netstandard2.0` for maximum compatibility.
4. WHEN a user references the NuGet package, THE package SHALL automatically configure the generator and attributes assembly without manual project file edits beyond the package reference.

### Requirement 13: NuGet Packaging and Release

**User Story:** As the package author, I want to publish the source generator to nuget.org via a GitHub Actions workflow triggered by a semantic version tag on the main branch, so that releases happen only after I have personally certified the generator's quality.

#### Acceptance Criteria

1. THE Release_Workflow SHALL be triggered WHEN a Git tag matching the pattern `v<semver>` (e.g., `v1.0.0`, `v1.0.0-preview.1`) is pushed to the `main` branch.
2. THE Release_Workflow SHALL derive the NuGet package version by stripping the leading `v` prefix from the Git tag.
3. THE Release_Workflow SHALL build the solution in Release configuration, run all tests, pack the NuGet package, and push the package to nuget.org in that order.
4. IF any build or test step fails, THEN THE Release_Workflow SHALL halt and not push the package to nuget.org.
5. THE Release_Workflow SHALL support both stable versions (e.g., `1.0.0`) and pre-release versions (e.g., `1.0.0-preview.1`, `1.0.0-beta.2`) derived from the Git tag.
6. THE Release_Workflow SHALL authenticate to nuget.org using a repository secret and SHALL NOT embed credentials in the workflow file.
7. THE Release_Workflow SHALL NOT execute unless the tag is on the `main` branch, preventing accidental releases from feature branches.

### Requirement 14: User-Facing Syntax Conventions

**User Story:** As a developer, I want the source generator to recognize and validate a specific set of syntax conventions for declaring state machine classes, handler methods, and required interface implementations, so that I receive immediate compile-time feedback when my declaration does not conform to the expected shape.

#### Acceptance Criteria

1. WHEN a class is decorated with state machine marker attributes, THE Parsing_Stage SHALL verify that the class is declared as `public`, `partial`, and `static` with exactly two generic type parameters representing the state type and the event type.
2. IF a State_Machine_Definition class is not declared as public, partial, and static with two generic type parameters, THEN THE Generator SHALL emit a Diagnostic error identifying which required modifiers or type parameters are missing.
3. THE Parsing_Stage SHALL verify that the State_Machine_Definition class implements the IStateMachine interface parameterized with the class's state type and event type.
4. IF the State_Machine_Definition class does not implement IStateMachine with the correct type parameters, THEN THE Generator SHALL emit a Diagnostic error indicating the missing interface implementation.
5. WHEN a method within the State_Machine_Definition class is decorated with the Transition_Attribute, THE Parsing_Stage SHALL verify that the method is declared as `public` and `static`, accepts the current state as the first parameter and the event as the second parameter, and returns the state type.
6. IF a Transition_Handler method does not conform to the required signature (public, static, accepting state and event parameters, returning state type), THEN THE Generator SHALL emit a Diagnostic error describing the expected signature.
7. THE Parsing_Stage SHALL verify that the State_Machine_Definition provides an implementation of the IStatePersistence interface parameterized with the state type, enabling the generated boilerplate to load the initial state and save the new state.
8. IF the State_Machine_Definition does not provide an IStatePersistence implementation, THEN THE Generator SHALL emit a Diagnostic error indicating that a persistence provider is required.
9. THE Generator SHALL use the validated class declaration, IStateMachine implementation, Transition_Attribute-decorated handler methods, and IStatePersistence implementation as the authoritative cues for generating the state machine boilerplate code.

### Requirement 15: Concurrency and Distributed Locking

**User Story:** As a developer, I want the generated orchestration to be thread-safe and support distributed concurrency via a user-supplied locking mechanism, so that two handlers on different machines cannot simultaneously update the same state.

#### Acceptance Criteria

1. THE Generator SHALL emit a State_Lock_Provider interface (e.g., `IStateLock<TState>`) that the user implements to provide locking around state transitions.
2. THE State_Lock_Provider interface SHALL be generic over the state type to allow lock scoping by state identity.
3. THE State_Lock_Provider interface SHALL support both distributed and local locking strategies without prescribing any specific implementation (e.g., Redlock, optimistic concurrency with version checks, distributed transactions, or in-process mutexes are all valid user implementations).
4. THE generated orchestration SHALL acquire the lock via the State_Lock_Provider before loading state and SHALL release the lock after saving the new state (or upon failure at any step).
5. IF the State_Lock_Provider fails to acquire the lock, THEN THE generated code SHALL not proceed with the transition and SHALL return a result indicating lock acquisition failure.
6. IF an exception occurs after the lock is acquired (during load, handler invocation, or save), THEN THE generated code SHALL release the lock before propagating the exception.
7. THE Generator SHALL emit a default no-op State_Lock_Provider implementation that acquires and releases without performing any actual locking, allowing users who handle concurrency at an external level to opt out of generator-level locking.
8. WHEN the user provides a custom State_Lock_Provider implementation, THE generated code SHALL use the user-provided implementation instead of the default no-op.
9. THE State_Lock_Provider interface SHALL not prescribe timeout values, retry policies, or lock granularity, leaving those decisions to the user's implementation.

### Requirement 16: Event Dispatch Extraction

**User Story:** As a developer, I want the generated dispatch logic to automatically extract an event identifier from complex event payloads and route to the correct transition handler, so that I never write switch/dispatch boilerplate myself.

#### Acceptance Criteria

1. THE event type used in a State_Machine_Definition SHALL implement the IDispatchableEvent interface parameterized with the Event_ID type (e.g., `IDispatchableEvent<TEventId>`), exposing a `GetEventId()` method that returns the dispatch identifier.
2. THE Event_ID type returned by `GetEventId()` SHALL support equality comparison so that the generated dispatch logic can use switch/case or if-else statements to match event identifiers.
3. WHEN an event is received, THE generated boilerplate SHALL invoke `GetEventId()` on the event payload and use the returned Event_ID to route to the correct Transition_Handler via a switch statement or equivalent dispatch mechanism.
4. THE generated dispatch code SHALL completely encapsulate all routing logic, so that the user does not write any switch, if-else, or dispatch code to direct events to handlers.
5. THE Parsing_Stage SHALL validate that the event type implements the IDispatchableEvent interface with a compatible Event_ID type parameter.
6. IF the event type does not implement the IDispatchableEvent interface, THEN THE Generator SHALL emit a Diagnostic error indicating the missing dispatch interface implementation.
7. THE Transition_Attribute SHALL include a property or parameter indicating which Event_ID value the decorated handler responds to, enabling the generator to map each case in the dispatch switch to the correct Transition_Handler.
8. WHEN an Event_ID value extracted at runtime has no matching Transition_Handler for the current state, THE generated dispatch code SHALL return a result indicating that the event was not handled (exhaustive dispatch with explicit fallthrough to NotHandled).
