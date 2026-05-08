// FsCheck Arbitrary generators for the generic enum-based API data models
// Provides custom generators for property-based testing of the updated pipeline
// **Validates: All (testing infrastructure)**

using System;
using System.Collections.Immutable;
using System.Linq;
using FsCheck;
using Microsoft.CodeAnalysis;
using StateMachineSrcGen;

namespace StateMachineSrcGen.Tests.Generators;

/// <summary>
/// Custom FsCheck Arbitrary generators for the generic enum-based API data models.
/// Produces generators for ParsedStateMachine, ValidatedStateMachine, entry callbacks,
/// cleanup handlers, and enum configurations used by property-based tests.
/// </summary>
public static class GenericApiArbitraries
{
    // ─── Enum Configuration Generators ──────────────────────────────────────────

    /// <summary>
    /// Generates a valid enum configuration: a set of 2-8 member names with sequential int values.
    /// Returns (memberNames, intValues) where intValues are 0-based sequential.
    /// </summary>
    public static Gen<(string[] Names, int[] Values)> GenValidEnumConfiguration()
    {
        return from count in Gen.Choose(2, 8)
               from names in Gen.ArrayOf(count, GenEnumMemberName())
                   .Where(arr => arr.Distinct().Count() == arr.Length)
               let values = Enumerable.Range(0, count).ToArray()
               select (names, values);
    }

    /// <summary>
    /// Generates a simple valid enum member name (e.g., "State0", "State1", "Event2").
    /// </summary>
    public static Gen<string> GenEnumMemberName()
    {
        return from prefix in Gen.Elements("State", "Event", "Status", "Phase", "Step", "Mode")
               from suffix in Gen.Choose(0, 99)
               select $"{prefix}{suffix}";
    }

    /// <summary>
    /// Generates an invalid enum value (an int that does not correspond to any member
    /// in an enum with the given member count).
    /// </summary>
    public static Gen<int> GenInvalidEnumValue(int memberCount)
    {
        return Gen.Choose(memberCount, memberCount + 100);
    }

    /// <summary>
    /// Generates an invalid enum value for a default-sized enum (2-8 members).
    /// Returns (memberCount, invalidValue) tuple.
    /// </summary>
    public static Gen<(int MemberCount, int InvalidValue)> GenInvalidEnumValueWithContext()
    {
        return from count in Gen.Choose(2, 8)
               from invalidValue in Gen.Choose(count, count + 100)
               select (count, invalidValue);
    }

    // ─── ParsedEvent Generators ─────────────────────────────────────────────────

    /// <summary>
    /// Generates a valid ParsedEvent with a name and sequential int value.
    /// </summary>
    public static Gen<ParsedEvent> GenParsedEvent(string name, int intValue)
    {
        return Gen.Constant(new ParsedEvent
        {
            Name = name,
            IntValue = intValue,
            Location = Location.None
        });
    }

    /// <summary>
    /// Generates an array of ParsedEvents from an enum configuration.
    /// </summary>
    public static Gen<ParsedEvent[]> GenParsedEvents()
    {
        return from config in GenValidEnumConfiguration()
               select config.Names.Zip(config.Values, (n, v) => new ParsedEvent
               {
                   Name = n,
                   IntValue = v,
                   Location = Location.None
               }).ToArray();
    }

    // ─── Entry Callback Generators ──────────────────────────────────────────────

    /// <summary>
    /// Generates a targeted ParsedEntryCallback for a specific state.
    /// </summary>
    public static Gen<ParsedEntryCallback> GenTargetedEntryCallback(string targetStateName)
    {
        return from methodName in Gen.Elements(
                   $"OnEnter{targetStateName}",
                   $"Enter{targetStateName}",
                   $"Handle{targetStateName}Entry")
               select new ParsedEntryCallback
               {
                   MethodName = methodName,
                   TargetStateName = targetStateName,
                   IsCatchAll = false,
                   Signature = new MethodSignature
                   {
                       IsPublic = true,
                       IsStatic = true,
                       ReturnType = "TState",
                       Parameters = new EquatableArray<ParameterInfo>(ImmutableArray.Create(
                           new ParameterInfo { Name = "state", TypeName = "TState" },
                           new ParameterInfo { Name = "event", TypeName = "TEvent" }))
                   },
                   Location = Location.None
               };
    }

    /// <summary>
    /// Generates a catch-all ParsedEntryCallback (no specific target state).
    /// </summary>
    public static Gen<ParsedEntryCallback> GenCatchAllEntryCallback()
    {
        return from methodName in Gen.Elements("OnEnterAny", "OnStateEntry", "HandleAnyEntry")
               select new ParsedEntryCallback
               {
                   MethodName = methodName,
                   TargetStateName = null,
                   IsCatchAll = true,
                   Signature = new MethodSignature
                   {
                       IsPublic = true,
                       IsStatic = true,
                       ReturnType = "void",
                       Parameters = new EquatableArray<ParameterInfo>(ImmutableArray.Create(
                           new ParameterInfo { Name = "state", TypeName = "TState" },
                           new ParameterInfo { Name = "event", TypeName = "TEvent" }))
                   },
                   Location = Location.None
               };
    }

    /// <summary>
    /// Generates a ParsedEntryCallback (either targeted or catch-all).
    /// </summary>
    public static Gen<ParsedEntryCallback> GenParsedEntryCallback()
    {
        return Gen.OneOf(
            GenTargetedEntryCallback("State0"),
            GenCatchAllEntryCallback());
    }

    /// <summary>
    /// Generates a ValidatedEntryCallback for a targeted state.
    /// </summary>
    public static Gen<ValidatedEntryCallback> GenValidatedTargetedEntryCallback(string targetStateName)
    {
        return from methodName in Gen.Elements(
                   $"OnEnter{targetStateName}",
                   $"Enter{targetStateName}")
               select new ValidatedEntryCallback
               {
                   MethodName = methodName,
                   TargetStateName = targetStateName,
                   IsCatchAll = false,
                   ReturnsTState = true
               };
    }

    /// <summary>
    /// Generates a ValidatedEntryCallback for catch-all.
    /// </summary>
    public static Gen<ValidatedEntryCallback> GenValidatedCatchAllEntryCallback()
    {
        return from methodName in Gen.Elements("OnEnterAny", "OnStateEntry")
               select new ValidatedEntryCallback
               {
                   MethodName = methodName,
                   TargetStateName = null,
                   IsCatchAll = true,
                   ReturnsTState = false
               };
    }

    // ─── Cleanup Handler Generators ─────────────────────────────────────────────

    /// <summary>
    /// Generates a valid ParsedCleanupHandler.
    /// </summary>
    public static Gen<ParsedCleanupHandler> GenParsedCleanupHandler()
    {
        return from methodName in Gen.Elements(
                   "OnTerminalCleanup",
                   "HandleTerminal",
                   "CleanupOnComplete",
                   "OnMachineTerminated")
               select new ParsedCleanupHandler
               {
                   MethodName = methodName,
                   Signature = new MethodSignature
                   {
                       IsPublic = true,
                       IsStatic = true,
                       ReturnType = "Task",
                       Parameters = new EquatableArray<ParameterInfo>(ImmutableArray.Create(
                           new ParameterInfo { Name = "state", TypeName = "TState" }))
                   },
                   Location = Location.None
               };
    }

    // ─── ParsedStateMachine (Generic API) Generators ────────────────────────────

    /// <summary>
    /// Generates a valid ParsedStateMachine with all new generic API fields populated.
    /// The machine is internally consistent: InitialStateName references an existing state,
    /// TerminalStateNames reference existing states, handlers reference valid states/events.
    /// </summary>
    public static Arbitrary<ParsedStateMachine> ValidGenericParsedStateMachine()
    {
        var gen = from className in StateMachineArbitraries.GenValidIdentifier()
                  from ns in StateMachineArbitraries.GenValidNamespace()
                  from stateEnumType in Gen.Elements("OrderStateId", "TaskStateId", "WorkflowStateId")
                  from eventEnumType in Gen.Elements("OrderEventId", "TaskEventId", "WorkflowEventId")
                  from stateType in Gen.Elements("OrderState", "TaskState", "WorkflowState")
                  from eventType in Gen.Elements("OrderEvent", "TaskEvent", "WorkflowEvent")
                  from stateCount in Gen.Choose(2, 5)
                  from eventCount in Gen.Choose(1, 4)
                  from stateNames in Gen.ArrayOf(stateCount, GenEnumMemberName())
                      .Where(arr => arr.Distinct().Count() == arr.Length)
                  from eventNames in Gen.ArrayOf(eventCount, GenEnumMemberName())
                      .Where(arr => arr.Distinct().Count() == arr.Length)
                  from hasCleanup in Arb.Generate<bool>()
                  from hasEntryCallback in Arb.Generate<bool>()
                  from terminalCount in Gen.Choose(0, Math.Min(2, stateCount - 1))
                  let states = stateNames.Select((name, i) => new ParsedState
                  {
                      Name = name,
                      IsInitial = i == 0,
                      Location = Location.None
                  }).ToArray()
                  let events = eventNames.Select((name, i) => new ParsedEvent
                  {
                      Name = name,
                      IntValue = i,
                      Location = Location.None
                  }).ToArray()
                  let initialStateName = stateNames[0]
                  let terminalStateNames = stateNames.Skip(1).Take(terminalCount).ToArray()
                  let handler = new ParsedHandler
                  {
                      MethodName = $"Handle{eventNames[0]}",
                      FromState = stateNames[0],
                      ToState = stateNames.Length > 1 ? stateNames[1] : stateNames[0],
                      Trigger = eventNames[0],
                      EventId = eventNames[0],
                      Kind = HandlerKind.Transition,
                      Signature = new MethodSignature
                      {
                          IsPublic = true,
                          IsStatic = true,
                          ReturnType = stateType,
                          Parameters = new EquatableArray<ParameterInfo>(ImmutableArray.Create(
                              new ParameterInfo { Name = "state", TypeName = stateType },
                              new ParameterInfo { Name = "event", TypeName = eventType }))
                      },
                      Location = Location.None
                  }
                  let entryCallbacks = hasEntryCallback && stateNames.Length > 1
                      ? new[] { new ParsedEntryCallback
                          {
                              MethodName = $"OnEnter{stateNames[1]}",
                              TargetStateName = stateNames[1],
                              IsCatchAll = false,
                              Signature = new MethodSignature
                              {
                                  IsPublic = true,
                                  IsStatic = true,
                                  ReturnType = stateType,
                                  Parameters = new EquatableArray<ParameterInfo>(ImmutableArray.Create(
                                      new ParameterInfo { Name = "state", TypeName = stateType },
                                      new ParameterInfo { Name = "event", TypeName = eventType }))
                              },
                              Location = Location.None
                          }}
                      : Array.Empty<ParsedEntryCallback>()
                  let cleanupHandler = hasCleanup
                      ? new ParsedCleanupHandler
                          {
                              MethodName = "OnTerminalCleanup",
                              Signature = new MethodSignature
                              {
                                  IsPublic = true,
                                  IsStatic = true,
                                  ReturnType = "Task",
                                  Parameters = new EquatableArray<ParameterInfo>(ImmutableArray.Create(
                                      new ParameterInfo { Name = "state", TypeName = stateType }))
                              },
                              Location = Location.None
                          }
                      : (ParsedCleanupHandler?)null
                  select new ParsedStateMachine
                  {
                      Namespace = ns,
                      ClassName = className,
                      Modifiers = ClassModifiers.Public | ClassModifiers.Partial | ClassModifiers.Static,
                      Location = Location.None,
                      StateIdEnumTypeName = stateEnumType,
                      EventIdEnumTypeName = eventEnumType,
                      StateTypeName = stateType,
                      EventTypeName = eventType,
                      States = new EquatableArray<ParsedState>(states.ToImmutableArray()),
                      Events = new EquatableArray<ParsedEvent>(events.ToImmutableArray()),
                      Handlers = new EquatableArray<ParsedHandler>(ImmutableArray.Create(handler)),
                      InitialStateName = initialStateName,
                      TerminalStateNames = new EquatableArray<string>(terminalStateNames.ToImmutableArray()),
                      EntryCallbacks = new EquatableArray<ParsedEntryCallback>(entryCallbacks.ToImmutableArray()),
                      CleanupHandler = cleanupHandler
                  };
        return gen.ToArbitrary();
    }

    /// <summary>
    /// Generates a ParsedStateMachine with no initial state (for testing SMSG005 diagnostic).
    /// </summary>
    public static Gen<ParsedStateMachine> GenParsedStateMachineWithoutInitialState()
    {
        return from className in StateMachineArbitraries.GenValidIdentifier()
               from ns in StateMachineArbitraries.GenValidNamespace()
               from stateCount in Gen.Choose(2, 4)
               from stateNames in Gen.ArrayOf(stateCount, GenEnumMemberName())
                   .Where(arr => arr.Distinct().Count() == arr.Length)
               from eventName in GenEnumMemberName()
               let states = stateNames.Select(name => new ParsedState
               {
                   Name = name,
                   IsInitial = false,
                   Location = Location.None
               }).ToArray()
               let events = new[] { new ParsedEvent { Name = eventName, IntValue = 0, Location = Location.None } }
               select new ParsedStateMachine
               {
                   Namespace = ns,
                   ClassName = className,
                   Modifiers = ClassModifiers.Public | ClassModifiers.Partial | ClassModifiers.Static,
                   Location = Location.None,
                   StateIdEnumTypeName = "TestStateId",
                   EventIdEnumTypeName = "TestEventId",
                   StateTypeName = "TestState",
                   EventTypeName = "TestEvent",
                   States = new EquatableArray<ParsedState>(states.ToImmutableArray()),
                   Events = new EquatableArray<ParsedEvent>(events.ToImmutableArray()),
                   Handlers = new EquatableArray<ParsedHandler>(ImmutableArray<ParsedHandler>.Empty),
                   InitialStateName = null,
                   TerminalStateNames = new EquatableArray<string>(ImmutableArray<string>.Empty),
                   EntryCallbacks = new EquatableArray<ParsedEntryCallback>(ImmutableArray<ParsedEntryCallback>.Empty),
                   CleanupHandler = null
               };
    }

    /// <summary>
    /// Generates a ParsedStateMachine with multiple entry callbacks for the same state
    /// (for testing SMSG023 diagnostic).
    /// </summary>
    public static Gen<ParsedStateMachine> GenParsedStateMachineWithDuplicateEntryCallbacks()
    {
        return from className in StateMachineArbitraries.GenValidIdentifier()
               from ns in StateMachineArbitraries.GenValidNamespace()
               from stateName1 in GenEnumMemberName()
               from stateName2 in GenEnumMemberName()
                   .Where(n => n != stateName1)
               from eventName in GenEnumMemberName()
               let states = new[]
               {
                   new ParsedState { Name = stateName1, IsInitial = true, Location = Location.None },
                   new ParsedState { Name = stateName2, IsInitial = false, Location = Location.None }
               }
               let events = new[] { new ParsedEvent { Name = eventName, IntValue = 0, Location = Location.None } }
               let duplicateCallbacks = new[]
               {
                   new ParsedEntryCallback
                   {
                       MethodName = $"OnEnter{stateName2}A",
                       TargetStateName = stateName2,
                       IsCatchAll = false,
                       Signature = new MethodSignature
                       {
                           IsPublic = true, IsStatic = true, ReturnType = "TestState",
                           Parameters = new EquatableArray<ParameterInfo>(ImmutableArray.Create(
                               new ParameterInfo { Name = "state", TypeName = "TestState" },
                               new ParameterInfo { Name = "event", TypeName = "TestEvent" }))
                       },
                       Location = Location.None
                   },
                   new ParsedEntryCallback
                   {
                       MethodName = $"OnEnter{stateName2}B",
                       TargetStateName = stateName2,
                       IsCatchAll = false,
                       Signature = new MethodSignature
                       {
                           IsPublic = true, IsStatic = true, ReturnType = "TestState",
                           Parameters = new EquatableArray<ParameterInfo>(ImmutableArray.Create(
                               new ParameterInfo { Name = "state", TypeName = "TestState" },
                               new ParameterInfo { Name = "event", TypeName = "TestEvent" }))
                       },
                       Location = Location.None
                   }
               }
               select new ParsedStateMachine
               {
                   Namespace = ns,
                   ClassName = className,
                   Modifiers = ClassModifiers.Public | ClassModifiers.Partial | ClassModifiers.Static,
                   Location = Location.None,
                   StateIdEnumTypeName = "TestStateId",
                   EventIdEnumTypeName = "TestEventId",
                   StateTypeName = "TestState",
                   EventTypeName = "TestEvent",
                   States = new EquatableArray<ParsedState>(states.ToImmutableArray()),
                   Events = new EquatableArray<ParsedEvent>(events.ToImmutableArray()),
                   Handlers = new EquatableArray<ParsedHandler>(ImmutableArray<ParsedHandler>.Empty),
                   InitialStateName = stateName1,
                   TerminalStateNames = new EquatableArray<string>(ImmutableArray<string>.Empty),
                   EntryCallbacks = new EquatableArray<ParsedEntryCallback>(duplicateCallbacks.ToImmutableArray()),
                   CleanupHandler = null
               };
    }

    // ─── ValidatedStateMachine (Generic API) Generators ─────────────────────────

    /// <summary>
    /// Generates a valid ValidatedStateMachine with non-terminal transitions.
    /// Includes entry callbacks and no cleanup handler.
    /// </summary>
    public static Arbitrary<ValidatedStateMachine> ValidGenericValidatedStateMachine()
    {
        var gen = from className in StateMachineArbitraries.GenValidIdentifier()
                  from ns in StateMachineArbitraries.GenValidNamespace()
                  from stateEnumType in Gen.Elements("OrderStateId", "TaskStateId", "WorkflowStateId")
                  from eventEnumType in Gen.Elements("OrderEventId", "TaskEventId", "WorkflowEventId")
                  from stateType in Gen.Elements("OrderState", "TaskState", "WorkflowState")
                  from eventType in Gen.Elements("OrderEvent", "TaskEvent", "WorkflowEvent")
                  from state1Name in GenEnumMemberName()
                  from state2Name in GenEnumMemberName()
                      .Where(n => n != state1Name)
                  from triggerName in GenEnumMemberName()
                  from hasEntryCallback in Arb.Generate<bool>()
                  from hasCatchAll in Arb.Generate<bool>()
                  let initial = new ValidatedState
                  {
                      Name = state1Name,
                      EnumValue = 0,
                      IsInitial = true,
                      IsTerminal = false
                  }
                  let target = new ValidatedState
                  {
                      Name = state2Name,
                      EnumValue = 1,
                      IsInitial = false,
                      IsTerminal = false
                  }
                  let entryCallbacks = BuildEntryCallbacks(hasEntryCallback, hasCatchAll, state2Name)
                  select new ValidatedStateMachine
                  {
                      Namespace = ns,
                      ClassName = className,
                      StateIdEnumTypeName = stateEnumType,
                      EventIdEnumTypeName = eventEnumType,
                      StateTypeName = stateType,
                      EventTypeName = eventType,
                      States = new EquatableArray<ValidatedState>(ImmutableArray.Create(initial, target)),
                      InitialState = initial,
                      Transitions = new EquatableArray<ValidatedTransition>(ImmutableArray.Create(
                          new ValidatedTransition
                          {
                              FromState = state1Name,
                              ToState = state2Name,
                              Trigger = triggerName,
                              FromStateEnumValue = 0,
                              ToStateEnumValue = 1,
                              TriggerEnumValue = 0,
                              EventId = triggerName,
                              HandlerMethodName = $"Handle{triggerName}",
                              GuardMethodName = null,
                              SideEffectMethodName = null,
                              IsTerminal = false,
                              DeclarationOrder = 0
                          })),
                      EntryCallbacks = new EquatableArray<ValidatedEntryCallback>(entryCallbacks.ToImmutableArray()),
                      CleanupHandlerMethodName = null
                  };
        return gen.ToArbitrary();
    }

    /// <summary>
    /// Generates a valid ValidatedStateMachine with terminal transitions and a cleanup handler.
    /// </summary>
    public static Arbitrary<ValidatedStateMachine> ValidatedStateMachineWithTerminalTransitions()
    {
        var gen = from className in StateMachineArbitraries.GenValidIdentifier()
                  from ns in StateMachineArbitraries.GenValidNamespace()
                  from stateEnumType in Gen.Elements("OrderStateId", "TaskStateId")
                  from eventEnumType in Gen.Elements("OrderEventId", "TaskEventId")
                  from stateType in Gen.Elements("OrderState", "TaskState")
                  from eventType in Gen.Elements("OrderEvent", "TaskEvent")
                  from state1Name in GenEnumMemberName()
                  from state2Name in GenEnumMemberName()
                      .Where(n => n != state1Name)
                  from state3Name in GenEnumMemberName()
                      .Where(n => n != state1Name && n != state2Name)
                  from triggerName in GenEnumMemberName()
                  from terminalTriggerName in GenEnumMemberName()
                      .Where(n => n != triggerName)
                  from hasCleanup in Arb.Generate<bool>()
                  from hasEntryCallback in Arb.Generate<bool>()
                  let initial = new ValidatedState
                  {
                      Name = state1Name,
                      EnumValue = 0,
                      IsInitial = true,
                      IsTerminal = false
                  }
                  let middle = new ValidatedState
                  {
                      Name = state2Name,
                      EnumValue = 1,
                      IsInitial = false,
                      IsTerminal = false
                  }
                  let terminal = new ValidatedState
                  {
                      Name = state3Name,
                      EnumValue = 2,
                      IsInitial = false,
                      IsTerminal = true
                  }
                  let entryCallbacks = hasEntryCallback
                      ? new[] { new ValidatedEntryCallback
                          {
                              MethodName = $"OnEnter{state3Name}",
                              TargetStateName = state3Name,
                              IsCatchAll = false,
                              ReturnsTState = true
                          }}
                      : Array.Empty<ValidatedEntryCallback>()
                  select new ValidatedStateMachine
                  {
                      Namespace = ns,
                      ClassName = className,
                      StateIdEnumTypeName = stateEnumType,
                      EventIdEnumTypeName = eventEnumType,
                      StateTypeName = stateType,
                      EventTypeName = eventType,
                      States = new EquatableArray<ValidatedState>(ImmutableArray.Create(initial, middle, terminal)),
                      InitialState = initial,
                      Transitions = new EquatableArray<ValidatedTransition>(ImmutableArray.Create(
                          new ValidatedTransition
                          {
                              FromState = state1Name,
                              ToState = state2Name,
                              Trigger = triggerName,
                              FromStateEnumValue = 0,
                              ToStateEnumValue = 1,
                              TriggerEnumValue = 0,
                              EventId = triggerName,
                              HandlerMethodName = $"Handle{triggerName}",
                              GuardMethodName = null,
                              SideEffectMethodName = null,
                              IsTerminal = false,
                              DeclarationOrder = 0
                          },
                          new ValidatedTransition
                          {
                              FromState = state2Name,
                              ToState = state3Name,
                              Trigger = terminalTriggerName,
                              FromStateEnumValue = 1,
                              ToStateEnumValue = 2,
                              TriggerEnumValue = 1,
                              EventId = terminalTriggerName,
                              HandlerMethodName = $"Handle{terminalTriggerName}",
                              GuardMethodName = null,
                              SideEffectMethodName = null,
                              IsTerminal = true,
                              DeclarationOrder = 1
                          })),
                      EntryCallbacks = new EquatableArray<ValidatedEntryCallback>(entryCallbacks.ToImmutableArray()),
                      CleanupHandlerMethodName = hasCleanup ? "OnTerminalCleanup" : null
                  };
        return gen.ToArbitrary();
    }

    /// <summary>
    /// Generates a ValidatedStateMachine with guards, side effects, and entry callbacks
    /// for comprehensive orchestration testing.
    /// </summary>
    public static Arbitrary<ValidatedStateMachine> ValidatedStateMachineWithFullOrchestration()
    {
        var gen = from className in StateMachineArbitraries.GenValidIdentifier()
                  from ns in StateMachineArbitraries.GenValidNamespace()
                  from stateEnumType in Gen.Elements("OrderStateId", "TaskStateId")
                  from eventEnumType in Gen.Elements("OrderEventId", "TaskEventId")
                  from stateType in Gen.Elements("OrderState", "TaskState")
                  from eventType in Gen.Elements("OrderEvent", "TaskEvent")
                  from state1Name in GenEnumMemberName()
                  from state2Name in GenEnumMemberName()
                      .Where(n => n != state1Name)
                  from triggerName in GenEnumMemberName()
                  let initial = new ValidatedState
                  {
                      Name = state1Name,
                      EnumValue = 0,
                      IsInitial = true,
                      IsTerminal = false
                  }
                  let target = new ValidatedState
                  {
                      Name = state2Name,
                      EnumValue = 1,
                      IsInitial = false,
                      IsTerminal = false
                  }
                  select new ValidatedStateMachine
                  {
                      Namespace = ns,
                      ClassName = className,
                      StateIdEnumTypeName = stateEnumType,
                      EventIdEnumTypeName = eventEnumType,
                      StateTypeName = stateType,
                      EventTypeName = eventType,
                      States = new EquatableArray<ValidatedState>(ImmutableArray.Create(initial, target)),
                      InitialState = initial,
                      Transitions = new EquatableArray<ValidatedTransition>(ImmutableArray.Create(
                          new ValidatedTransition
                          {
                              FromState = state1Name,
                              ToState = state2Name,
                              Trigger = triggerName,
                              FromStateEnumValue = 0,
                              ToStateEnumValue = 1,
                              TriggerEnumValue = 0,
                              EventId = triggerName,
                              HandlerMethodName = $"Handle{triggerName}",
                              GuardMethodName = $"Can{triggerName}",
                              SideEffectMethodName = $"After{triggerName}",
                              IsTerminal = false,
                              DeclarationOrder = 0
                          })),
                      EntryCallbacks = new EquatableArray<ValidatedEntryCallback>(ImmutableArray.Create(
                          new ValidatedEntryCallback
                          {
                              MethodName = $"OnEnter{state2Name}",
                              TargetStateName = state2Name,
                              IsCatchAll = false,
                              ReturnsTState = true
                          },
                          new ValidatedEntryCallback
                          {
                              MethodName = "OnEnterAny",
                              TargetStateName = null,
                              IsCatchAll = true,
                              ReturnsTState = false
                          })),
                      CleanupHandlerMethodName = null
                  };
        return gen.ToArbitrary();
    }

    // ─── Helper Methods ─────────────────────────────────────────────────────────

    private static ValidatedEntryCallback[] BuildEntryCallbacks(
        bool hasTargeted, bool hasCatchAll, string targetStateName)
    {
        var callbacks = ImmutableArray.CreateBuilder<ValidatedEntryCallback>();

        if (hasTargeted)
        {
            callbacks.Add(new ValidatedEntryCallback
            {
                MethodName = $"OnEnter{targetStateName}",
                TargetStateName = targetStateName,
                IsCatchAll = false,
                ReturnsTState = true
            });
        }

        if (hasCatchAll)
        {
            callbacks.Add(new ValidatedEntryCallback
            {
                MethodName = "OnEnterAny",
                TargetStateName = null,
                IsCatchAll = true,
                ReturnsTState = false
            });
        }

        return callbacks.ToArray();
    }
}
