// FsCheck Arbitrary generators for pipeline data models
// Provides custom generators for property-based testing of the state machine pipeline
// **Validates: Requirements 11.1, 11.2, 11.3**

using System;
using System.Collections.Immutable;
using System.Linq;
using FsCheck;
using Microsoft.CodeAnalysis;
using StateMachineSrcGen;

namespace StateMachineSrcGen.Tests.Generators;

/// <summary>
/// Custom FsCheck Arbitrary generators for all pipeline data models.
/// Produces both valid and invalid variants for comprehensive property testing.
/// </summary>
public static class StateMachineArbitraries
{
    // ─── Identifier Generation ──────────────────────────────────────────────────

    private static readonly char[] UpperLetters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray();
    private static readonly char[] LowerAlphaNum = "abcdefghijklmnopqrstuvwxyz0123456789".ToCharArray();

    /// <summary>
    /// Generates valid C# identifiers (start with uppercase letter, rest alphanumeric).
    /// </summary>
    public static Gen<string> GenValidIdentifier()
    {
        return from firstChar in Gen.Elements(UpperLetters)
               from rest in Gen.ArrayOf(Gen.Elements(LowerAlphaNum))
                   .Where(a => a.Length >= 1 && a.Length <= 8)
               select firstChar + new string(rest);
    }

    /// <summary>
    /// Generates valid C# namespace names (dot-separated identifiers).
    /// </summary>
    public static Gen<string> GenValidNamespace()
    {
        return from part1 in GenValidIdentifier()
               from part2 in GenValidIdentifier()
               select $"{part1}.{part2}";
    }

    // ─── ClassModifiers ─────────────────────────────────────────────────────────

    /// <summary>
    /// Generates arbitrary ClassModifiers flag combinations.
    /// </summary>
    public static Arbitrary<ClassModifiers> ArbitraryClassModifiers()
    {
        return Gen.Elements(
            ClassModifiers.None,
            ClassModifiers.Public,
            ClassModifiers.Partial,
            ClassModifiers.Static,
            ClassModifiers.Public | ClassModifiers.Partial,
            ClassModifiers.Public | ClassModifiers.Static,
            ClassModifiers.Partial | ClassModifiers.Static,
            ClassModifiers.Public | ClassModifiers.Partial | ClassModifiers.Static
        ).ToArbitrary();
    }

    // ─── MethodSignature ────────────────────────────────────────────────────────

    /// <summary>
    /// Generates arbitrary MethodSignature instances (valid and invalid).
    /// </summary>
    public static Arbitrary<MethodSignature> ArbitraryMethodSignature()
    {
        var gen = from isPublic in Arb.Generate<bool>()
                  from isStatic in Arb.Generate<bool>()
                  from returnType in Gen.Elements("string", "int", "bool", "void", "MyState", "Task<string>")
                  from paramCount in Gen.Elements(0, 1, 2, 3)
                  from parameters in Gen.ArrayOf(paramCount, GenParameterInfo())
                  select new MethodSignature
                  {
                      IsPublic = isPublic,
                      IsStatic = isStatic,
                      ReturnType = returnType,
                      Parameters = new EquatableArray<ParameterInfo>(parameters.ToImmutableArray())
                  };

        return gen.ToArbitrary();
    }

    /// <summary>
    /// Generates valid transition MethodSignature (public, static, returns state type, takes state+event).
    /// </summary>
    public static Gen<MethodSignature> GenValidTransitionSignature(string stateType = "MyState", string eventType = "MyEvent")
    {
        return Gen.Constant(new MethodSignature
        {
            IsPublic = true,
            IsStatic = true,
            ReturnType = stateType,
            Parameters = new EquatableArray<ParameterInfo>(ImmutableArray.Create(
                new ParameterInfo { Name = "state", TypeName = stateType },
                new ParameterInfo { Name = "event", TypeName = eventType }))
        });
    }

    /// <summary>
    /// Generates valid guard MethodSignature (public, static, returns bool).
    /// </summary>
    public static Gen<MethodSignature> GenValidGuardSignature(string stateType = "MyState", string eventType = "MyEvent")
    {
        return Gen.Constant(new MethodSignature
        {
            IsPublic = true,
            IsStatic = true,
            ReturnType = "bool",
            Parameters = new EquatableArray<ParameterInfo>(ImmutableArray.Create(
                new ParameterInfo { Name = "state", TypeName = stateType },
                new ParameterInfo { Name = "event", TypeName = eventType }))
        });
    }

    // ─── ParameterInfo ──────────────────────────────────────────────────────────

    /// <summary>
    /// Generates arbitrary ParameterInfo instances.
    /// </summary>
    public static Gen<ParameterInfo> GenParameterInfo()
    {
        return from name in GenValidIdentifier()
               from typeName in Gen.Elements("string", "int", "bool", "object", "MyState", "MyEvent")
               select new ParameterInfo { Name = name, TypeName = typeName };
    }

    // ─── ParsedState ────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates arbitrary ParsedState instances.
    /// </summary>
    public static Arbitrary<ParsedState> ArbitraryParsedState()
    {
        var gen = from name in GenValidIdentifier()
                  from isInitial in Arb.Generate<bool>()
                  select new ParsedState
                  {
                      Name = name,
                      IsInitial = isInitial,
                      Location = Location.None
                  };
        return gen.ToArbitrary();
    }

    /// <summary>
    /// Generates ParsedState arrays with controlled name collisions for duplicate detection testing.
    /// </summary>
    public static Gen<ParsedState[]> GenParsedStatesWithCollisions()
    {
        return from duplicateName in GenValidIdentifier()
               from uniqueName in GenValidIdentifier()
               let safeName = duplicateName == uniqueName ? uniqueName + "X" : uniqueName
               select new[]
               {
                   new ParsedState { Name = duplicateName, IsInitial = true, Location = Location.None },
                   new ParsedState { Name = safeName, IsInitial = false, Location = Location.None },
                   new ParsedState { Name = duplicateName, IsInitial = false, Location = Location.None }
               };
    }

    // ─── ParsedTrigger ──────────────────────────────────────────────────────────

    /// <summary>
    /// Generates arbitrary ParsedTrigger instances.
    /// </summary>
    public static Arbitrary<ParsedTrigger> ArbitraryParsedTrigger()
    {
        var gen = from name in GenValidIdentifier()
                  select new ParsedTrigger
                  {
                      Name = name,
                      Location = Location.None
                  };
        return gen.ToArbitrary();
    }

    /// <summary>
    /// Generates ParsedTrigger arrays with controlled name collisions.
    /// </summary>
    public static Gen<ParsedTrigger[]> GenParsedTriggersWithCollisions()
    {
        return from duplicateName in GenValidIdentifier()
               from uniqueName in GenValidIdentifier()
               let safeName = duplicateName == uniqueName ? uniqueName + "X" : uniqueName
               select new[]
               {
                   new ParsedTrigger { Name = duplicateName, Location = Location.None },
                   new ParsedTrigger { Name = safeName, Location = Location.None },
                   new ParsedTrigger { Name = duplicateName, Location = Location.None }
               };
    }

    // ─── ParsedHandler ──────────────────────────────────────────────────────────

    /// <summary>
    /// Generates arbitrary ParsedHandler instances.
    /// </summary>
    public static Arbitrary<ParsedHandler> ArbitraryParsedHandler()
    {
        var gen = from methodName in GenValidIdentifier()
                  from fromState in GenValidIdentifier()
                  from toState in GenValidIdentifier()
                  from trigger in GenValidIdentifier()
                  from kind in Gen.Elements(HandlerKind.Transition, HandlerKind.Guard, HandlerKind.SideEffect)
                  from hasEventId in Arb.Generate<bool>()
                  from eventIdValue in GenValidIdentifier()
                  let eventId = hasEventId ? eventIdValue : (string?)null
                  select new ParsedHandler
                  {
                      MethodName = methodName,
                      FromState = fromState,
                      ToState = toState,
                      Trigger = trigger,
                      EventId = eventId,
                      Kind = kind,
                      Signature = new MethodSignature
                      {
                          IsPublic = true,
                          IsStatic = true,
                          ReturnType = "MyState",
                          Parameters = new EquatableArray<ParameterInfo>(ImmutableArray.Create(
                              new ParameterInfo { Name = "state", TypeName = "MyState" },
                              new ParameterInfo { Name = "event", TypeName = "MyEvent" }))
                      },
                      Location = Location.None
                  };
        return gen.ToArbitrary();
    }

    /// <summary>
    /// Generates ParsedHandler with a specific EventId value for dispatch testing.
    /// </summary>
    public static Gen<ParsedHandler> GenParsedHandlerWithEventId()
    {
        return from methodName in GenValidIdentifier()
               from fromState in GenValidIdentifier()
               from toState in GenValidIdentifier()
               from trigger in GenValidIdentifier()
               from eventId in Gen.Elements("Start", "Stop", "Pause", "Resume", "Reset", "1", "2", "42", "100")
               select new ParsedHandler
               {
                   MethodName = methodName,
                   FromState = fromState,
                   ToState = toState,
                   Trigger = trigger,
                   EventId = eventId,
                   Kind = HandlerKind.Transition,
                   Signature = new MethodSignature
                   {
                       IsPublic = true,
                       IsStatic = true,
                       ReturnType = "MyState",
                       Parameters = new EquatableArray<ParameterInfo>(ImmutableArray.Create(
                           new ParameterInfo { Name = "state", TypeName = "MyState" },
                           new ParameterInfo { Name = "event", TypeName = "MyEvent" }))
                   },
                   Location = Location.None
               };
    }

    // ─── ParsedStateMachine ─────────────────────────────────────────────────────

    /// <summary>
    /// Generates valid ParsedStateMachine instances (all constraints satisfied).
    /// </summary>
    public static Arbitrary<ParsedStateMachine> ValidParsedStateMachine()
    {
        var gen = from className in GenValidIdentifier()
                  from ns in GenValidNamespace()
                  from stateType in GenValidIdentifier()
                  from eventType in GenValidIdentifier()
                  from stateName1 in GenValidIdentifier()
                  from stateName2 in GenValidIdentifier()
                  from triggerName in GenValidIdentifier()
                  let s1 = stateName1 == stateName2 ? stateName1 + "A" : stateName1
                  let s2 = stateName1 == stateName2 ? stateName2 + "B" : stateName2
                  select new ParsedStateMachine
                  {
                      Namespace = ns,
                      ClassName = className,
                      StateTypeName = stateType,
                      EventTypeName = eventType,
                      StateIdEnumTypeName = "string",
                      EventIdEnumTypeName = "string",
                      States = new EquatableArray<ParsedState>(ImmutableArray.Create(
                          new ParsedState { Name = s1, IsInitial = true, Location = Location.None },
                          new ParsedState { Name = s2, IsInitial = false, Location = Location.None })),
                      Events = new EquatableArray<ParsedEvent>(ImmutableArray.Create(
                          new ParsedEvent { Name = triggerName, IntValue = 0, Location = Location.None })),
                      Handlers = new EquatableArray<ParsedHandler>(ImmutableArray.Create(
                          new ParsedHandler
                          {
                              MethodName = $"Handle{triggerName}",
                              FromState = s1,
                              ToState = s2,
                              Trigger = triggerName,
                              EventId = triggerName.ToLowerInvariant(),
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
                          })),
                      Modifiers = ClassModifiers.Public | ClassModifiers.Partial | ClassModifiers.Static,
                      InitialStateName = s1,
                      TerminalStateNames = new EquatableArray<string>(ImmutableArray<string>.Empty),
                      EntryCallbacks = new EquatableArray<ParsedEntryCallback>(ImmutableArray<ParsedEntryCallback>.Empty),
                      CleanupHandler = null,
                      Location = Location.None
                  };
        return gen.ToArbitrary();
    }

    /// <summary>
    /// Generates invalid ParsedStateMachine instances (various constraint violations).
    /// </summary>
    public static Arbitrary<ParsedStateMachine> InvalidParsedStateMachine()
    {
        return Gen.Elements(
            CreateInvalidMachine(stateCount: 0),
            CreateInvalidMachine(noInitialState: true),
            CreateInvalidMachine(multipleInitialStates: true),
            CreateInvalidMachine(missingModifiers: true)
        ).ToArbitrary();
    }

    private static ParsedStateMachine CreateInvalidMachine(
        int stateCount = 2,
        bool noInitialState = false,
        bool multipleInitialStates = false,
        bool missingModifiers = false)
    {
        ParsedState[] states;
        if (stateCount == 0)
        {
            states = Array.Empty<ParsedState>();
        }
        else if (multipleInitialStates)
        {
            states = new[]
            {
                new ParsedState { Name = "A", IsInitial = true, Location = Location.None },
                new ParsedState { Name = "B", IsInitial = true, Location = Location.None }
            };
        }
        else if (noInitialState)
        {
            states = new[]
            {
                new ParsedState { Name = "A", IsInitial = false, Location = Location.None },
                new ParsedState { Name = "B", IsInitial = false, Location = Location.None }
            };
        }
        else
        {
            states = new[]
            {
                new ParsedState { Name = "A", IsInitial = true, Location = Location.None },
                new ParsedState { Name = "B", IsInitial = false, Location = Location.None }
            };
        }

        var triggers = new[] { new ParsedTrigger { Name = "Go", Location = Location.None } };
        var handlers = states.Length >= 2
            ? new[]
            {
                new ParsedHandler
                {
                    MethodName = "HandleGo",
                    FromState = "A",
                    ToState = "B",
                    Trigger = "Go",
                    EventId = "go",
                    Kind = HandlerKind.Transition,
                    Signature = new MethodSignature
                    {
                        IsPublic = true,
                        IsStatic = true,
                        ReturnType = "MyState",
                        Parameters = new EquatableArray<ParameterInfo>(ImmutableArray.Create(
                            new ParameterInfo { Name = "state", TypeName = "MyState" },
                            new ParameterInfo { Name = "event", TypeName = "MyEvent" }))
                    },
                    Location = Location.None
                }
            }
            : Array.Empty<ParsedHandler>();

        return new ParsedStateMachine
        {
            Namespace = "TestNs",
            ClassName = "InvalidMachine",
            StateTypeName = "MyState",
            EventTypeName = "MyEvent",
            StateIdEnumTypeName = "string",
            EventIdEnumTypeName = "string",
            States = new EquatableArray<ParsedState>(states.ToImmutableArray()),
            Events = new EquatableArray<ParsedEvent>(ImmutableArray.Create(
                new ParsedEvent { Name = "Go", IntValue = 0, Location = Location.None })),
            Handlers = new EquatableArray<ParsedHandler>(handlers.ToImmutableArray()),
            Modifiers = missingModifiers ? ClassModifiers.Public : (ClassModifiers.Public | ClassModifiers.Partial | ClassModifiers.Static),
            InitialStateName = noInitialState ? null : "A",
            TerminalStateNames = new EquatableArray<string>(ImmutableArray<string>.Empty),
            EntryCallbacks = new EquatableArray<ParsedEntryCallback>(ImmutableArray<ParsedEntryCallback>.Empty),
            CleanupHandler = null,
            Location = Location.None
        };
    }

    // ─── ValidatedStateMachine ──────────────────────────────────────────────────

    /// <summary>
    /// Generates always-valid ValidatedStateMachine instances (for generation tests).
    /// </summary>
    public static Arbitrary<ValidatedStateMachine> ValidValidatedStateMachine()
    {
        var gen = from className in GenValidIdentifier()
                  from ns in GenValidNamespace()
                  from stateType in GenValidIdentifier()
                  from eventType in GenValidIdentifier()
                  from state1 in GenValidIdentifier()
                  from state2 in GenValidIdentifier()
                  from trigger in GenValidIdentifier()
                  let s1 = state1 == state2 ? state1 + "X" : state1
                  let s2 = state1 == state2 ? state2 + "Y" : state2
                  let initial = new ValidatedState { Name = s1, EnumValue = 0, IsInitial = true, IsTerminal = false }
                  let terminal = new ValidatedState { Name = s2, EnumValue = 1, IsInitial = false, IsTerminal = true }
                  select new ValidatedStateMachine
                  {
                      Namespace = ns,
                      ClassName = className,
                      StateTypeName = stateType,
                      EventTypeName = eventType,
                      StateIdEnumTypeName = "string",
                      EventIdEnumTypeName = "string",
                      States = new EquatableArray<ValidatedState>(ImmutableArray.Create(initial, terminal)),
                      InitialState = initial,
                      Transitions = new EquatableArray<ValidatedTransition>(ImmutableArray.Create(
                          new ValidatedTransition
                          {
                              FromState = s1,
                              ToState = s2,
                              Trigger = trigger,
                              FromStateEnumValue = 0,
                              ToStateEnumValue = 1,
                              TriggerEnumValue = 0,
                              EventId = trigger.ToLowerInvariant(),
                              HandlerMethodName = $"Handle{trigger}",
                              GuardMethodName = null,
                              SideEffectMethodName = null,
                              IsTerminal = true,
                              DeclarationOrder = 0
                          })),
                      EntryCallbacks = new EquatableArray<ValidatedEntryCallback>(ImmutableArray<ValidatedEntryCallback>.Empty),
                      CleanupHandlerMethodName = null
                  };
        return gen.ToArbitrary();
    }

    /// <summary>
    /// Generates ValidatedStateMachine with guards and side effects.
    /// </summary>
    public static Arbitrary<ValidatedStateMachine> ValidatedStateMachineWithGuards()
    {
        var gen = from className in GenValidIdentifier()
                  from ns in GenValidNamespace()
                  from state1 in GenValidIdentifier()
                  from state2 in GenValidIdentifier()
                  from trigger in GenValidIdentifier()
                  let s1 = state1 == state2 ? state1 + "X" : state1
                  let s2 = state1 == state2 ? state2 + "Y" : state2
                  let initial = new ValidatedState { Name = s1, EnumValue = 0, IsInitial = true, IsTerminal = false }
                  let terminal = new ValidatedState { Name = s2, EnumValue = 1, IsInitial = false, IsTerminal = true }
                  select new ValidatedStateMachine
                  {
                      Namespace = ns,
                      ClassName = className,
                      StateTypeName = "string",
                      EventTypeName = "TestEvent",
                      StateIdEnumTypeName = "string",
                      EventIdEnumTypeName = "string",
                      States = new EquatableArray<ValidatedState>(ImmutableArray.Create(initial, terminal)),
                      InitialState = initial,
                      Transitions = new EquatableArray<ValidatedTransition>(ImmutableArray.Create(
                          new ValidatedTransition
                          {
                              FromState = s1,
                              ToState = s2,
                              Trigger = trigger,
                              FromStateEnumValue = 0,
                              ToStateEnumValue = 1,
                              TriggerEnumValue = 0,
                              EventId = trigger.ToLowerInvariant(),
                              HandlerMethodName = $"Handle{trigger}",
                              GuardMethodName = $"Can{trigger}",
                              SideEffectMethodName = $"On{trigger}",
                              IsTerminal = true,
                              DeclarationOrder = 0
                          })),
                      EntryCallbacks = new EquatableArray<ValidatedEntryCallback>(ImmutableArray<ValidatedEntryCallback>.Empty),
                      CleanupHandlerMethodName = null
                  };
        return gen.ToArbitrary();
    }
}
