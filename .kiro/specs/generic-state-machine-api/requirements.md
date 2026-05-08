# Requirements Document

## Introduction

This feature redesigns the StateMachineSrcGen public API to replace verbose, string-based `[State("name")]` and `[Trigger("name")]` class-level attributes with a generic, type-safe approach using enums and generic type parameters. The state machine class declares four type parameters: the state ID enum type, the event ID enum type, the full state type, and the full event type. The ID type parameters are constrained with `struct, Enum` to enforce enum-only at the language level, while the state and event types are constrained to `IStateMachineState<TStateId>` and `IDispatchableEvent<TEventId>` respectively. The generator derives valid states and triggers from enum members, eliminating redundant declarations, providing compile-time type safety, and significantly reducing boilerplate.

## Clarifications

### Session 2026-05-08

- Q: Should the generator accept `[Flags]`-attributed enums as state/event ID types? → A: Reject `[Flags]` enums with a diagnostic error.
- Q: What should `HandleAsync` return when a guard rejects a transition? → A: Distinct `TransitionResult.GuardRejected` result (separate from NoTransition).
- Q: Should the `[OnTerminal]` cleanup handler be async or sync? → A: Async (`Task`) — cleanup can await external operations.
- Q: Should `[OnEnter]` state-entry callbacks support async signatures? → A: Both sync and async supported — generator detects return type from signature.
- Q: Should `TransitionResult` carry the resulting state on success? → A: Yes — `TransitionResult<TState>` includes the new state on success.
- Q: Should the class support non-enum ID types (string, int, long)? → A: No — use four explicit type parameters with `struct, Enum` constraints on ID types. The compiler enforces enum-only; the generator does not need to emit SMSG017 for non-enum types.

## Glossary

- **Generator**: The Roslyn incremental source generator (`StateMachineGenerator`) that processes user declarations and emits state machine implementation code.
- **State_Machine_Class**: The user-authored `partial class` decorated with attributes that the Generator processes.
- **State_ID_Enum**: A user-defined enum whose members represent the valid set of states in the state machine.
- **Event_ID_Enum**: A user-defined enum whose members represent the valid set of triggers/events in the state machine.
- **State_Type**: The user-defined type implementing `IStateMachineState<TStateId>` that carries the full state payload.
- **Event_Type**: The user-defined type implementing `IDispatchableEvent<TEventId>` that carries the full event payload.
- **Transition_Attribute**: The `[Transition]` method-level attribute that declares a state transition handler.
- **Guard_Attribute**: The `[Guard]` method-level attribute that declares a guard condition for a transition.
- **SideEffect_Attribute**: The `[SideEffect]` method-level attribute that declares a side effect for a transition.
- **Initial_State_Attribute**: A class-level attribute that designates which enum member is the initial state.
- **Terminal_State_Attribute**: A class-level attribute (zero or more allowed) that designates which enum members are terminal/final states where the state machine lifecycle ends.
- **Cleanup_Handler**: An optional user-defined method invoked when the state machine transitions into a terminal state, used for end-of-lifecycle work such as resource cleanup or state machine deletion.
- **OnEnter_Attribute**: A method-level attribute (with optional state ID parameter) that marks a method as a state-entry callback, invoked whenever the state machine enters the specified state (or any state if no parameter is provided).
- **State_Entry_Callback**: A user-defined method invoked when the state machine enters a specific state (targeted) or any state (catch-all), capable of mutating the state before persistence.
- **State_Attribute**: The existing `[State("name")]` class-level attribute (to be removed).
- **Trigger_Attribute**: The existing `[Trigger("name")]` class-level attribute (to be removed).

## Requirements

### Requirement 1: Generic State Machine Class Declaration

**User Story:** As a developer, I want to declare my state machine class with four explicit generic type parameters (state ID, event ID, state type, event type), so that the valid states and triggers are derived from enum definitions and the full state/event types are explicitly parameterized.

#### Acceptance Criteria

1. THE Generator SHALL recognize a `partial class` declaration with four type parameters (`TStateId`, `TEventId`, `TState`, `TEvent`) as a state machine definition.
2. THE State_Machine_Class SHALL constrain `TStateId` with `struct, Enum` and `TEventId` with `struct, Enum` to enforce enum-only ID types at the language level.
3. THE State_Machine_Class SHALL constrain `TState` with `IStateMachineState<TStateId>` and `TEvent` with `IDispatchableEvent<TEventId>`.
4. THE Generator SHALL derive the valid set of states from the members of the concrete enum type provided for `TStateId`.
5. THE Generator SHALL derive the valid set of triggers from the members of the concrete enum type provided for `TEventId`.
6. BECAUSE the `struct, Enum` constraint is enforced by the C# compiler, THE Generator SHALL NOT need to emit SMSG017 for non-enum types — the compiler rejects them before the generator runs.

### Requirement 2: Elimination of State and Trigger Attributes

**User Story:** As a developer, I want to stop declaring `[State]` and `[Trigger]` attributes on my state machine class, so that my declarations are concise and the enum definitions serve as the single source of truth.

#### Acceptance Criteria

1. THE Generator SHALL NOT require `[State]` attributes on the State_Machine_Class when a State_ID_Enum type parameter is provided.
2. THE Generator SHALL NOT require `[Trigger]` attributes on the State_Machine_Class when an Event_ID_Enum type parameter is provided.
3. WHEN the State_Machine_Class uses the generic API with enum type parameters, THE Generator SHALL treat each enum member of the State_ID_Enum as a declared state.
4. WHEN the State_Machine_Class uses the generic API with enum type parameters, THE Generator SHALL treat each enum member of the Event_ID_Enum as a declared trigger.

### Requirement 3: Initial State Declaration

**User Story:** As a developer, I want a concise way to designate which enum member is the initial state, so that the generator knows the starting state without verbose attribute syntax.

#### Acceptance Criteria

1. THE Generator SHALL provide an `[InitialState]` class-level attribute that accepts a state ID enum value to designate the initial state.
2. WHEN no initial state is designated on the State_Machine_Class, THE Generator SHALL emit diagnostic SMSG005.
3. WHEN multiple `[InitialState]` attributes are present on the State_Machine_Class, THE Generator SHALL emit diagnostic SMSG006.
4. WHEN the value provided to `[InitialState]` is not a member of the State_ID_Enum, THE Generator SHALL emit a diagnostic error.

### Requirement 4: Enum-Based Transition Attribute

**User Story:** As a developer, I want the `[Transition]` attribute to reference enum values instead of strings, so that invalid state or trigger references are caught at compile time.

#### Acceptance Criteria

1. THE Transition_Attribute SHALL accept state ID enum values for the `From` and `To` parameters.
2. THE Transition_Attribute SHALL accept an event ID enum value for the `Trigger` parameter.
3. WHEN a Transition_Attribute references a `From` value that is not a member of the State_ID_Enum, THE Generator SHALL emit a diagnostic error.
4. WHEN a Transition_Attribute references a `To` value that is not a member of the State_ID_Enum, THE Generator SHALL emit a diagnostic error.
5. WHEN a Transition_Attribute references a `Trigger` value that is not a member of the Event_ID_Enum, THE Generator SHALL emit a diagnostic error.
6. THE Transition_Attribute SHALL use `nameof()` expressions or integer-castable enum constants to satisfy C# attribute constant-expression constraints.

### Requirement 5: Enum-Based Guard and SideEffect Attributes

**User Story:** As a developer, I want the `[Guard]` and `[SideEffect]` attributes to also reference enum values instead of strings, so that the entire transition declaration surface is type-safe.

#### Acceptance Criteria

1. THE Guard_Attribute SHALL accept state ID enum values for the `From` and `To` parameters and an event ID enum value for the `Trigger` parameter.
2. THE SideEffect_Attribute SHALL accept state ID enum values for the `From` and `To` parameters and an event ID enum value for the `Trigger` parameter.
3. WHEN a Guard_Attribute or SideEffect_Attribute references an enum value not present in the corresponding enum type, THE Generator SHALL emit a diagnostic error.

### Requirement 6: Compile-Time Validation via Enum Membership

**User Story:** As a developer, I want the generator to validate transition references against enum members at compile time, so that typos and invalid references are impossible.

#### Acceptance Criteria

1. THE Generator SHALL validate that all `From` and `To` values in Transition_Attribute, Guard_Attribute, and SideEffect_Attribute are members of the State_ID_Enum.
2. THE Generator SHALL validate that all `Trigger` values in Transition_Attribute, Guard_Attribute, and SideEffect_Attribute are members of the Event_ID_Enum.
3. WHEN validation passes, THE Generator SHALL NOT emit SMSG002 (undefined state) or SMSG003 (undefined trigger) diagnostics because enum membership makes these checks redundant.
4. THE Generator SHALL continue to emit SMSG001 (duplicate transition handler) when multiple handlers share the same (From, To, Trigger) triple.
5. THE Generator SHALL continue to emit SMSG009 (unreachable state) for non-initial enum members that are not targeted by any transition.

### Requirement 7: Generated Code Adaptation

**User Story:** As a developer, I want the generated state machine implementation to use enum comparisons instead of string comparisons, so that dispatch logic is type-safe and efficient.

#### Acceptance Criteria

1. THE Generator SHALL emit dispatch logic that compares `GetStateId()` return values against State_ID_Enum members using enum equality.
2. THE Generator SHALL emit dispatch logic that compares `GetEventId()` return values against Event_ID_Enum members using enum equality.
3. THE Generator SHALL emit a `switch` statement over Event_ID_Enum values for event dispatch routing.
4. THE Generator SHALL emit state comparisons using the State_ID_Enum values within each event case.
5. THE Generator SHALL emit the orchestration sequence as: lock-acquire → guard → transition handler → state-entry callback → persist → side-effect → lock-release (for non-terminal transitions). Lock-release SHALL be emitted in a `finally` block to ensure release even if an exception occurs during orchestration.
6. THE Generator SHALL use C# pattern matching (`switch` expressions or pattern-based `switch` statements) for the transition orchestration dispatch logic where it produces clearer, more concise generated code than nested if/else chains.

### Requirement 8: C# Attribute Constant Expression Compatibility

**User Story:** As a developer, I want the attribute API to work within C# language constraints for attribute arguments, so that the enum-based approach compiles without workarounds.

#### Acceptance Criteria

1. THE Transition_Attribute SHALL accept `int` constructor parameters that represent cast enum values (e.g., `[Transition((int)StateId.Pending, (int)StateId.Confirmed, (int)EventId.Confirm)]`).
2. THE Generator SHALL resolve integer attribute arguments back to their corresponding enum members during parsing.
3. IF the integer value provided to a Transition_Attribute does not correspond to a defined enum member, THEN THE Generator SHALL emit a diagnostic error indicating an invalid enum value.
4. THE Guard_Attribute and SideEffect_Attribute SHALL follow the same integer-cast pattern as the Transition_Attribute.

### Requirement 9: Diagnostic Updates

**User Story:** As a developer, I want clear diagnostic messages that reference enum types and members instead of string names, so that error messages guide me to the correct fix.

#### Acceptance Criteria

1. WHEN the Generator emits SMSG002 for the generic API, THE Generator SHALL reference the enum type and expected members in the diagnostic message.
2. WHEN the Generator emits SMSG003 for the generic API, THE Generator SHALL reference the enum type and expected members in the diagnostic message.
3. THE Generator SHALL emit diagnostic SMSG019 when a type parameter constraint is missing or incorrect (e.g., state type does not implement `IStateMachineState<TStateId>`).
4. THE Generator SHALL emit diagnostic SMSG018 when the `[InitialState]` value cannot be resolved to an enum member.

### Requirement 10: Terminal State Declaration and Cleanup Handler

**User Story:** As a developer, I want to designate zero or more enum members as terminal states and optionally define a cleanup handler, so that the generator can invoke end-of-lifecycle logic when the state machine reaches a final state.

#### Acceptance Criteria

1. THE Generator SHALL provide a `[TerminalState]` class-level attribute that accepts a state ID enum value to designate a terminal state.
2. THE State_Machine_Class SHALL support zero or more Terminal_State_Attribute declarations.
3. WHEN the value provided to a Terminal_State_Attribute is not a member of the State_ID_Enum, THE Generator SHALL emit a diagnostic error.
4. IF a State_ID_Enum member is designated as both the initial state and a terminal state, THEN THE Generator SHALL emit a diagnostic warning indicating that a state is both initial and terminal, but SHALL NOT reject the definition.
5. WHEN the state machine transitions into a terminal state and a Cleanup_Handler method is defined on the State_Machine_Class, THE Generator SHALL emit code that invokes the Cleanup_Handler after the transition completes.
6. WHEN the state machine transitions into a terminal state and no Cleanup_Handler method is defined, THE Generator SHALL emit code that completes the transition without invoking any cleanup logic.
7. THE Generator SHALL recognize a Cleanup_Handler as a method on the State_Machine_Class decorated with a `[OnTerminal]` method-level attribute.
8. WHEN multiple methods are decorated with `[OnTerminal]` on the State_Machine_Class, THE Generator SHALL emit a diagnostic error indicating that only one Cleanup_Handler is permitted.
9. THE Cleanup_Handler SHALL receive the final State_Type as a parameter, enabling the handler to perform end-of-lifecycle work such as state machine deletion or resource cleanup.
10. THE Generator SHALL invoke the Cleanup_Handler as the final step in the orchestration sequence for terminal-state transitions, with the ordering: lock-acquire → guard → transition handler → state-entry callback → persist → cleanup → lock-release. No side-effects SHALL be invoked after cleanup for terminal-state transitions.

### Requirement 11: Playground Example Coverage for Guards and Side Effects

**User Story:** As a developer evaluating this library, I want the playground project to include working examples of guard functions and side-effect functions, so that I can understand how they integrate with transitions in practice.

#### Acceptance Criteria

1. THE playground project SHALL include at least one guard method decorated with the Guard_Attribute that demonstrates conditional transition blocking.
2. THE playground guard example SHALL show a realistic scenario where a transition is rejected based on domain state (e.g., an order cannot ship if no items are present, or a payment cannot be confirmed if the amount is zero).
3. THE playground project SHALL include at least one side-effect method decorated with the SideEffect_Attribute that demonstrates post-transition logic.
4. THE playground side-effect example SHALL show a realistic scenario such as sending a notification, logging an audit trail, or triggering a downstream process after a transition completes.
5. THE playground `Program.cs` SHALL exercise both the guard rejection path and the successful side-effect path, printing output that demonstrates the behavior.
6. THE playground project SHALL use the new generic enum-based API for all examples once the migration is complete.

### Requirement 12: README and NuGet Package Documentation for Guards and Side Effects

**User Story:** As a developer reading the project README or NuGet package description, I want clear documentation explaining the purpose and usage of guard functions and side-effect functions, so that I understand when and how to use them.

#### Acceptance Criteria

1. THE project README SHALL include a dedicated section explaining the purpose of guard functions: they evaluate whether a transition is permitted based on current state and event, returning `bool` to allow or block the transition.
2. THE project README SHALL include a code example showing a guard method declaration with the Guard_Attribute and its expected behavior when the guard returns `false`.
3. THE project README SHALL include a dedicated section explaining the purpose of side-effect functions: they execute after a transition has been persisted and are used for non-state-mutating work such as notifications, logging, or external integrations.
4. THE project README SHALL include a code example showing a side-effect method declaration with the SideEffect_Attribute.
5. THE README documentation SHALL explain the orchestration ordering: lock-acquire → guard → transition handler → state-entry callback → persist → side-effect → lock-release (for non-terminal transitions).
6. THE README documentation SHALL explain that guards run before the transition handler and can prevent the transition entirely, while side-effects run after persist and cannot affect the transition outcome.
7. THE NuGet package description or package README SHALL reference guards and side-effects as supported features of the generator.
8. THE README SHALL document the method signature requirements for guards (`public static bool`, two parameters: state and event) and side-effects (`public static void`, two parameters: state and event).

### Requirement 13: State-Entry Callbacks

**User Story:** As a developer, I want to define methods that are automatically invoked when the state machine enters a specific state (or any state), so that I can perform state-entry logic such as timestamping, initializing sub-state, or logging without duplicating code across every transition handler that targets that state.

#### Acceptance Criteria

1. THE Generator SHALL provide an `[OnEnter]` method-level attribute that optionally accepts a state ID enum value to designate a targeted state-entry callback.
2. WHEN an `[OnEnter]` attribute is applied with a state ID enum value, THE Generator SHALL invoke that method only when the state machine enters the specified state.
3. WHEN an `[OnEnter]` attribute is applied without a state ID parameter (parameterless), THE Generator SHALL invoke that method on every state entry regardless of which state is entered.
4. THE State_Machine_Class SHALL support zero or more targeted `[OnEnter(stateId)]` methods for different states.
5. THE State_Machine_Class SHALL support at most one parameterless (catch-all) `[OnEnter]` method. WHEN multiple parameterless `[OnEnter]` methods are defined, THE Generator SHALL emit a diagnostic error.
6. WHEN both a targeted `[OnEnter(stateId)]` and a catch-all `[OnEnter]` are defined, THE Generator SHALL invoke the targeted callback first, followed by the catch-all callback.
7. THE state-entry callback SHALL return `TState` (the State_Type), enabling it to mutate the state before persistence.
8. THE state-entry callback SHALL receive the State_Type and Event_Type as parameters, consistent with transition handler signatures.
9. THE Generator SHALL invoke state-entry callbacks after the transition handler and before the persist step, with the orchestration ordering: lock-acquire → guard → transition handler → state-entry callback → persist → side-effect → lock-release.
10. WHEN the value provided to a targeted `[OnEnter]` attribute is not a member of the State_ID_Enum, THE Generator SHALL emit a diagnostic error.
11. WHEN multiple targeted `[OnEnter]` methods reference the same state ID enum value, THE Generator SHALL emit a diagnostic error indicating duplicate state-entry handlers for that state.
12. THE catch-all `[OnEnter]` method signature SHALL use `void` return type since it runs after any targeted entry callback has already produced the final state. It receives the State_Type and Event_Type as parameters for observational purposes.
