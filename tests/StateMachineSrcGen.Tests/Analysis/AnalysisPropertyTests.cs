// Feature: generic-state-machine-api
// Property 3: [Flags] enum produces diagnostic — **Validates: Clarification (Flags enum rejection)**
// Property 5: Invalid enum value produces diagnostic — **Validates: Requirements 3.4, 4.3, 4.4, 4.5, 5.3, 8.3, 10.3, 13.10**
// Property 6: Initial state cardinality enforcement — **Validates: Requirements 3.2, 3.3**
// Property 7: Valid enum-based definitions produce no SMSG002/SMSG003 — **Validates: Requirements 6.3**
// Property 8: Duplicate transition detection preserved — **Validates: Requirements 6.4**
// Property 9: Unreachable state detection for enum members — **Validates: Requirements 6.5**
// Property 13: Entry callback uniqueness enforcement — **Validates: Requirements 13.5, 13.11**
// Property 14: Cleanup handler uniqueness enforcement — **Validates: Requirements 10.8**

using System;
using System.Collections.Immutable;
using System.Linq;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using StateMachineSrcGen;
using StateMachineSrcGen.Analysis;
using StateMachineSrcGen.Parsing;
using StateMachineSrcGen.Tests.Generators;
using StateMachineSrcGen.Tests.Parsing;

namespace StateMachineSrcGen.Tests.Analysis;

/// <summary>
/// Property-based tests for the Analysis pipeline covering Properties 3, 5, 6, 7, 8, 9, 13, and 14.
/// These tests validate diagnostic emission for various invalid configurations using the
/// AnalysisPipeline directly with constructed ParsedStateMachine inputs, and via full
/// Roslyn compilation + ParsingPipeline → AnalysisPipeline for scenarios requiring semantic analysis.
/// </summary>
public class AnalysisPropertyTests
{
    // ═══════════════════════════════════════════════════════════════════════════════
    // Property 3: [Flags] enum produces diagnostic (SMSG026)
    // For any enum decorated with [Flags] used as state/event ID, generator emits SMSG026
    // **Validates: Clarification (Flags enum rejection)**
    // ═══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Property 3: A [Flags] enum used as state ID type produces SMSG026 diagnostic.
    /// Uses in-memory Roslyn compilation to create a state machine with a [Flags] state enum.
    /// </summary>
    [Property(MaxTest = 50)]
    public Property FlagsEnum_AsStateId_EmitsSMSG026()
    {
        return Prop.ForAll(
            GenericApiArbitraries.GenValidEnumConfiguration().ToArbitrary(),
            enumConfig =>
            {
                var (memberNames, _) = enumConfig;
                if (memberNames.Length < 2)
                    return true.Label("Skipped: need at least 2 members");

                var source = GenerateFlagsEnumStateMachineSource(memberNames, flagsOnState: true);
                var diagnostics = RunFullPipeline(source);

                return diagnostics.Any(d => d.Id == "SMSG026")
                    .Label($"Expected SMSG026 for [Flags] state enum. Got: [{string.Join(", ", diagnostics.Select(d => d.Id))}]");
            });
    }

    /// <summary>
    /// Property 3: A [Flags] enum used as event ID type produces SMSG026 diagnostic.
    /// </summary>
    [Property(MaxTest = 50)]
    public Property FlagsEnum_AsEventId_EmitsSMSG026()
    {
        return Prop.ForAll(
            GenericApiArbitraries.GenValidEnumConfiguration().ToArbitrary(),
            enumConfig =>
            {
                var (memberNames, _) = enumConfig;
                if (memberNames.Length < 2)
                    return true.Label("Skipped: need at least 2 members");

                var source = GenerateFlagsEnumStateMachineSource(memberNames, flagsOnState: false);
                var diagnostics = RunFullPipeline(source);

                return diagnostics.Any(d => d.Id == "SMSG026")
                    .Label($"Expected SMSG026 for [Flags] event enum. Got: [{string.Join(", ", diagnostics.Select(d => d.Id))}]");
            });
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // Property 5: Invalid enum value produces diagnostic (SMSG018)
    // For any integer not corresponding to a defined enum member, generator emits SMSG018
    // **Validates: Requirements 3.4, 4.3, 4.4, 4.5, 5.3, 8.3, 10.3, 13.10**
    // ═══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Property 5: An invalid integer value in [InitialState] that doesn't correspond to
    /// a defined enum member produces SMSG018.
    /// </summary>
    [Property(MaxTest = 50)]
    public Property InvalidEnumValue_InInitialState_EmitsSMSG018()
    {
        return Prop.ForAll(
            GenericApiArbitraries.GenInvalidEnumValueWithContext().ToArbitrary(),
            ctx =>
            {
                var (memberCount, invalidValue) = ctx;
                var memberNames = Enumerable.Range(0, memberCount)
                    .Select(i => $"State{i}")
                    .ToArray();

                var source = GenerateStateMachineWithInvalidInitialState(memberNames, invalidValue);
                var diagnostics = RunFullPipeline(source);

                return diagnostics.Any(d => d.Id == "SMSG018")
                    .Label($"Expected SMSG018 for invalid enum value {invalidValue} in [InitialState]. " +
                           $"Got: [{string.Join(", ", diagnostics.Select(d => $"{d.Id}: {d.GetMessage()}"))}]");
            });
    }

    /// <summary>
    /// Property 5: An invalid integer value in [Transition] attribute (From parameter)
    /// that doesn't correspond to a defined enum member produces SMSG018.
    /// </summary>
    [Property(MaxTest = 50)]
    public Property InvalidEnumValue_InTransitionFrom_EmitsSMSG018()
    {
        return Prop.ForAll(
            GenericApiArbitraries.GenInvalidEnumValueWithContext().ToArbitrary(),
            ctx =>
            {
                var (memberCount, invalidValue) = ctx;
                var memberNames = Enumerable.Range(0, memberCount)
                    .Select(i => $"State{i}")
                    .ToArray();

                var source = GenerateStateMachineWithInvalidTransitionFrom(memberNames, invalidValue);
                var diagnostics = RunFullPipeline(source);

                return diagnostics.Any(d => d.Id == "SMSG018")
                    .Label($"Expected SMSG018 for invalid From value {invalidValue}. " +
                           $"Got: [{string.Join(", ", diagnostics.Select(d => $"{d.Id}: {d.GetMessage()}"))}]");
            });
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // Property 6: Initial state cardinality enforcement (SMSG005 / SMSG006)
    // Zero [InitialState] → SMSG005; multiple [InitialState] → SMSG006
    // **Validates: Requirements 3.2, 3.3**
    // ═══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Property 6a: A state machine with zero [InitialState] attributes emits SMSG005.
    /// Uses the AnalysisPipeline directly with a ParsedStateMachine that has no initial state.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NoInitialState_EmitsSMSG005()
    {
        return Prop.ForAll(
            GenericApiArbitraries.GenParsedStateMachineWithoutInitialState().ToArbitrary(),
            machine =>
            {
                var (_, diagnostics) = AnalysisPipeline.Analyze(machine);

                return diagnostics.Any(d => d.Id == "SMSG005")
                    .Label($"Expected SMSG005 for missing initial state. " +
                           $"Got: [{string.Join(", ", diagnostics.Select(d => d.Id))}]");
            });
    }

    /// <summary>
    /// Property 6b: A state machine with multiple [InitialState] attributes emits SMSG006.
    /// Constructs a ParsedStateMachine with multiple states marked as initial.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property MultipleInitialStates_EmitsSMSG006()
    {
        return Prop.ForAll(
            GenMultipleInitialStatesMachine().ToArbitrary(),
            machine =>
            {
                var (_, diagnostics) = AnalysisPipeline.Analyze(machine);

                return diagnostics.Any(d => d.Id == "SMSG006")
                    .Label($"Expected SMSG006 for multiple initial states. " +
                           $"Got: [{string.Join(", ", diagnostics.Select(d => d.Id))}]");
            });
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // Property 7: Valid enum-based definitions produce no SMSG002/SMSG003
    // Fully valid enum-based definitions emit zero SMSG002 or SMSG003 diagnostics
    // **Validates: Requirements 6.3**
    // ═══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Property 7: A fully valid state machine definition using enum-based type parameters
    /// where all attribute values resolve to valid enum members emits zero SMSG002 or SMSG003.
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(GenericApiArbitraries) })]
    public Property ValidEnumDefinition_NoSMSG002_NoSMSG003(ParsedStateMachine machine)
    {
        // Only test machines that are structurally valid (have initial state, etc.)
        if (machine.InitialStateName == null || machine.States.Count() < 2)
            return true.Label("Skipped: machine not structurally valid for this test");

        var (_, diagnostics) = AnalysisPipeline.Analyze(machine);

        var hasSMSG002 = diagnostics.Any(d => d.Id == "SMSG002");
        var hasSMSG003 = diagnostics.Any(d => d.Id == "SMSG003");

        return (!hasSMSG002 && !hasSMSG003)
            .Label($"Expected no SMSG002/SMSG003 for valid enum definition. " +
                   $"Got SMSG002={hasSMSG002}, SMSG003={hasSMSG003}. " +
                   $"All diagnostics: [{string.Join(", ", diagnostics.Select(d => d.Id))}]");
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // Property 8: Duplicate transition detection preserved (SMSG001)
    // Two+ handlers with same (From, To, Trigger) triple → SMSG001
    // **Validates: Requirements 6.4**
    // ═══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Property 8: Two or more handlers with the same (From, To, Trigger) triple
    /// produce SMSG001 diagnostic.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DuplicateTransition_EmitsSMSG001()
    {
        return Prop.ForAll(
            GenDuplicateTransitionMachine().ToArbitrary(),
            machine =>
            {
                var (_, diagnostics) = AnalysisPipeline.Analyze(machine);

                return diagnostics.Any(d => d.Id == "SMSG001")
                    .Label($"Expected SMSG001 for duplicate (From, To, Trigger) triple. " +
                           $"Got: [{string.Join(", ", diagnostics.Select(d => d.Id))}]");
            });
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // Property 9: Unreachable state detection for enum members (SMSG009)
    // Non-initial enum member not targeted by any transition → SMSG009
    // **Validates: Requirements 6.5**
    // ═══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Property 9: A non-initial enum member that is not the target of any transition
    /// produces SMSG009 warning.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property UnreachableEnumMember_EmitsSMSG009()
    {
        return Prop.ForAll(
            GenUnreachableStateMachine().ToArbitrary(),
            input =>
            {
                var (machine, unreachableStateName) = input;
                var (_, diagnostics) = AnalysisPipeline.Analyze(machine);

                return diagnostics.Any(d => d.Id == "SMSG009" &&
                                            d.GetMessage().Contains(unreachableStateName))
                    .Label($"Expected SMSG009 for unreachable state '{unreachableStateName}'. " +
                           $"Got: [{string.Join(", ", diagnostics.Select(d => $"{d.Id}: {d.GetMessage()}"))}]");
            });
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // Property 13: Entry callback uniqueness enforcement (SMSG022 / SMSG023)
    // Multiple parameterless [OnEnter] → SMSG022; multiple targeted [OnEnter] same state → SMSG023
    // **Validates: Requirements 13.5, 13.11**
    // ═══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Property 13a: Multiple parameterless (catch-all) [OnEnter] methods produce SMSG022.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property MultipleCatchAllOnEnter_EmitsSMSG022()
    {
        return Prop.ForAll(
            GenMultipleCatchAllEntryCallbackMachine().ToArbitrary(),
            machine =>
            {
                var (_, diagnostics) = AnalysisPipeline.Analyze(machine);

                return diagnostics.Any(d => d.Id == "SMSG022")
                    .Label($"Expected SMSG022 for multiple catch-all [OnEnter]. " +
                           $"Got: [{string.Join(", ", diagnostics.Select(d => d.Id))}]");
            });
    }

    /// <summary>
    /// Property 13b: Multiple targeted [OnEnter] methods referencing the same state produce SMSG023.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DuplicateTargetedOnEnter_EmitsSMSG023()
    {
        return Prop.ForAll(
            GenericApiArbitraries.GenParsedStateMachineWithDuplicateEntryCallbacks().ToArbitrary(),
            machine =>
            {
                var (_, diagnostics) = AnalysisPipeline.Analyze(machine);

                return diagnostics.Any(d => d.Id == "SMSG023")
                    .Label($"Expected SMSG023 for duplicate targeted [OnEnter] same state. " +
                           $"Got: [{string.Join(", ", diagnostics.Select(d => d.Id))}]");
            });
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // Property 14: Cleanup handler uniqueness enforcement (SMSG021)
    // Multiple [OnTerminal] methods → SMSG021
    // **Validates: Requirements 10.8**
    // ═══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Property 14: Multiple [OnTerminal] methods on the same state machine produce SMSG021.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property MultipleCleanupHandlers_EmitsSMSG021()
    {
        return Prop.ForAll(
            GenMultipleCleanupHandlersMachine().ToArbitrary(),
            machine =>
            {
                var (_, diagnostics) = AnalysisPipeline.Analyze(machine);

                return diagnostics.Any(d => d.Id == "SMSG021")
                    .Label($"Expected SMSG021 for multiple [OnTerminal] methods. " +
                           $"Got: [{string.Join(", ", diagnostics.Select(d => d.Id))}]");
            });
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // Generator Methods
    // ═══════════════════════════════════════════════════════════════════════════════


    /// <summary>
    /// Generates a ParsedStateMachine with multiple states marked as initial.
    /// </summary>
    private static Gen<ParsedStateMachine> GenMultipleInitialStatesMachine()
    {
        return from className in StateMachineArbitraries.GenValidIdentifier()
               from ns in StateMachineArbitraries.GenValidNamespace()
               from stateName1 in GenericApiArbitraries.GenEnumMemberName()
               from stateName2 in GenericApiArbitraries.GenEnumMemberName()
                   .Where(n => n != stateName1)
               from stateName3 in GenericApiArbitraries.GenEnumMemberName()
                   .Where(n => n != stateName1 && n != stateName2)
               from eventName in GenericApiArbitraries.GenEnumMemberName()
               let states = new[]
               {
                   new ParsedState { Name = stateName1, IsInitial = true, Location = Location.None },
                   new ParsedState { Name = stateName2, IsInitial = true, Location = Location.None },
                   new ParsedState { Name = stateName3, IsInitial = false, Location = Location.None }
               }
               let events = new[] { new ParsedEvent { Name = eventName, IntValue = 0, Location = Location.None } }
               let handler = new ParsedHandler
               {
                   MethodName = $"Handle{eventName}",
                   FromState = stateName1,
                   ToState = stateName3,
                   Trigger = eventName,
                   EventId = eventName,
                   Kind = HandlerKind.Transition,
                   Signature = new MethodSignature
                   {
                       IsPublic = true,
                       IsStatic = true,
                       ReturnType = "TestState",
                       Parameters = new EquatableArray<ParameterInfo>(ImmutableArray.Create(
                           new ParameterInfo { Name = "state", TypeName = "TestState" },
                           new ParameterInfo { Name = "event", TypeName = "TestEvent" }))
                   },
                   Location = Location.None
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
                   Handlers = new EquatableArray<ParsedHandler>(ImmutableArray.Create(handler)),
                   InitialStateName = stateName1,
                   TerminalStateNames = new EquatableArray<string>(ImmutableArray<string>.Empty),
                   EntryCallbacks = new EquatableArray<ParsedEntryCallback>(ImmutableArray<ParsedEntryCallback>.Empty),
                   CleanupHandler = null
               };
    }

    /// <summary>
    /// Generates a ParsedStateMachine with duplicate (From, To, Trigger) transition handlers.
    /// </summary>
    private static Gen<ParsedStateMachine> GenDuplicateTransitionMachine()
    {
        return from className in StateMachineArbitraries.GenValidIdentifier()
               from ns in StateMachineArbitraries.GenValidNamespace()
               from stateName1 in GenericApiArbitraries.GenEnumMemberName()
               from stateName2 in GenericApiArbitraries.GenEnumMemberName()
                   .Where(n => n != stateName1)
               from eventName in GenericApiArbitraries.GenEnumMemberName()
               let states = new[]
               {
                   new ParsedState { Name = stateName1, IsInitial = true, Location = Location.None },
                   new ParsedState { Name = stateName2, IsInitial = false, Location = Location.None }
               }
               let events = new[] { new ParsedEvent { Name = eventName, IntValue = 0, Location = Location.None } }
               let handler1 = new ParsedHandler
               {
                   MethodName = $"Handle{eventName}A",
                   FromState = stateName1,
                   ToState = stateName2,
                   Trigger = eventName,
                   EventId = eventName,
                   Kind = HandlerKind.Transition,
                   Signature = new MethodSignature
                   {
                       IsPublic = true,
                       IsStatic = true,
                       ReturnType = "TestState",
                       Parameters = new EquatableArray<ParameterInfo>(ImmutableArray.Create(
                           new ParameterInfo { Name = "state", TypeName = "TestState" },
                           new ParameterInfo { Name = "event", TypeName = "TestEvent" }))
                   },
                   Location = Location.None
               }
               let handler2 = new ParsedHandler
               {
                   MethodName = $"Handle{eventName}B",
                   FromState = stateName1,
                   ToState = stateName2,
                   Trigger = eventName,
                   EventId = eventName,
                   Kind = HandlerKind.Transition,
                   Signature = new MethodSignature
                   {
                       IsPublic = true,
                       IsStatic = true,
                       ReturnType = "TestState",
                       Parameters = new EquatableArray<ParameterInfo>(ImmutableArray.Create(
                           new ParameterInfo { Name = "state", TypeName = "TestState" },
                           new ParameterInfo { Name = "event", TypeName = "TestEvent" }))
                   },
                   Location = Location.None
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
                   Handlers = new EquatableArray<ParsedHandler>(ImmutableArray.Create(handler1, handler2)),
                   InitialStateName = stateName1,
                   TerminalStateNames = new EquatableArray<string>(ImmutableArray<string>.Empty),
                   EntryCallbacks = new EquatableArray<ParsedEntryCallback>(ImmutableArray<ParsedEntryCallback>.Empty),
                   CleanupHandler = null
               };
    }

    /// <summary>
    /// Generates a (ParsedStateMachine, unreachableStateName) tuple where the machine has
    /// a non-initial state that is not targeted by any transition.
    /// </summary>
    private static Gen<(ParsedStateMachine Machine, string UnreachableStateName)> GenUnreachableStateMachine()
    {
        return from className in StateMachineArbitraries.GenValidIdentifier()
               from ns in StateMachineArbitraries.GenValidNamespace()
               from stateName1 in GenericApiArbitraries.GenEnumMemberName()
               from stateName2 in GenericApiArbitraries.GenEnumMemberName()
                   .Where(n => n != stateName1)
               from unreachableName in GenericApiArbitraries.GenEnumMemberName()
                   .Where(n => n != stateName1 && n != stateName2)
               from eventName in GenericApiArbitraries.GenEnumMemberName()
               let states = new[]
               {
                   new ParsedState { Name = stateName1, IsInitial = true, Location = Location.None },
                   new ParsedState { Name = stateName2, IsInitial = false, Location = Location.None },
                   new ParsedState { Name = unreachableName, IsInitial = false, Location = Location.None }
               }
               let events = new[] { new ParsedEvent { Name = eventName, IntValue = 0, Location = Location.None } }
               // Only transition targets stateName2, leaving unreachableName unreachable
               let handler = new ParsedHandler
               {
                   MethodName = $"Handle{eventName}",
                   FromState = stateName1,
                   ToState = stateName2,
                   Trigger = eventName,
                   EventId = eventName,
                   Kind = HandlerKind.Transition,
                   Signature = new MethodSignature
                   {
                       IsPublic = true,
                       IsStatic = true,
                       ReturnType = "TestState",
                       Parameters = new EquatableArray<ParameterInfo>(ImmutableArray.Create(
                           new ParameterInfo { Name = "state", TypeName = "TestState" },
                           new ParameterInfo { Name = "event", TypeName = "TestEvent" }))
                   },
                   Location = Location.None
               }
               let machine = new ParsedStateMachine
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
                   Handlers = new EquatableArray<ParsedHandler>(ImmutableArray.Create(handler)),
                   InitialStateName = stateName1,
                   TerminalStateNames = new EquatableArray<string>(ImmutableArray<string>.Empty),
                   EntryCallbacks = new EquatableArray<ParsedEntryCallback>(ImmutableArray<ParsedEntryCallback>.Empty),
                   CleanupHandler = null
               }
               select (machine, unreachableName);
    }

    /// <summary>
    /// Generates a ParsedStateMachine with multiple catch-all (parameterless) [OnEnter] callbacks.
    /// </summary>
    private static Gen<ParsedStateMachine> GenMultipleCatchAllEntryCallbackMachine()
    {
        return from className in StateMachineArbitraries.GenValidIdentifier()
               from ns in StateMachineArbitraries.GenValidNamespace()
               from stateName1 in GenericApiArbitraries.GenEnumMemberName()
               from stateName2 in GenericApiArbitraries.GenEnumMemberName()
                   .Where(n => n != stateName1)
               from eventName in GenericApiArbitraries.GenEnumMemberName()
               let states = new[]
               {
                   new ParsedState { Name = stateName1, IsInitial = true, Location = Location.None },
                   new ParsedState { Name = stateName2, IsInitial = false, Location = Location.None }
               }
               let events = new[] { new ParsedEvent { Name = eventName, IntValue = 0, Location = Location.None } }
               let handler = new ParsedHandler
               {
                   MethodName = $"Handle{eventName}",
                   FromState = stateName1,
                   ToState = stateName2,
                   Trigger = eventName,
                   EventId = eventName,
                   Kind = HandlerKind.Transition,
                   Signature = new MethodSignature
                   {
                       IsPublic = true,
                       IsStatic = true,
                       ReturnType = "TestState",
                       Parameters = new EquatableArray<ParameterInfo>(ImmutableArray.Create(
                           new ParameterInfo { Name = "state", TypeName = "TestState" },
                           new ParameterInfo { Name = "event", TypeName = "TestEvent" }))
                   },
                   Location = Location.None
               }
               let catchAll1 = new ParsedEntryCallback
               {
                   MethodName = "OnEnterAnyA",
                   TargetStateName = null,
                   IsCatchAll = true,
                   Signature = new MethodSignature
                   {
                       IsPublic = true,
                       IsStatic = true,
                       ReturnType = "void",
                       Parameters = new EquatableArray<ParameterInfo>(ImmutableArray.Create(
                           new ParameterInfo { Name = "state", TypeName = "TestState" },
                           new ParameterInfo { Name = "event", TypeName = "TestEvent" }))
                   },
                   Location = Location.None
               }
               let catchAll2 = new ParsedEntryCallback
               {
                   MethodName = "OnEnterAnyB",
                   TargetStateName = null,
                   IsCatchAll = true,
                   Signature = new MethodSignature
                   {
                       IsPublic = true,
                       IsStatic = true,
                       ReturnType = "void",
                       Parameters = new EquatableArray<ParameterInfo>(ImmutableArray.Create(
                           new ParameterInfo { Name = "state", TypeName = "TestState" },
                           new ParameterInfo { Name = "event", TypeName = "TestEvent" }))
                   },
                   Location = Location.None
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
                   Handlers = new EquatableArray<ParsedHandler>(ImmutableArray.Create(handler)),
                   InitialStateName = stateName1,
                   TerminalStateNames = new EquatableArray<string>(ImmutableArray<string>.Empty),
                   EntryCallbacks = new EquatableArray<ParsedEntryCallback>(
                       ImmutableArray.Create(catchAll1, catchAll2)),
                   CleanupHandler = null
               };
    }

    /// <summary>
    /// Generates a ParsedStateMachine with multiple cleanup handlers ([OnTerminal] methods).
    /// The machine stores the first cleanup handler in CleanupHandler and adds a second one
    /// to simulate the "multiple [OnTerminal]" scenario. Since ParsedStateMachine only holds
    /// one CleanupHandler, we use a convention: if the machine has entry callbacks with
    /// HandlerKind.Cleanup, the validator should detect duplicates.
    /// 
    /// NOTE: The actual detection mechanism depends on how the analysis pipeline handles
    /// multiple cleanup handlers. For now, we model this by having two handlers with
    /// Kind = Cleanup in the Handlers array.
    /// </summary>
    private static Gen<ParsedStateMachine> GenMultipleCleanupHandlersMachine()
    {
        return from className in StateMachineArbitraries.GenValidIdentifier()
               from ns in StateMachineArbitraries.GenValidNamespace()
               from stateName1 in GenericApiArbitraries.GenEnumMemberName()
               from stateName2 in GenericApiArbitraries.GenEnumMemberName()
                   .Where(n => n != stateName1)
               from eventName in GenericApiArbitraries.GenEnumMemberName()
               let states = new[]
               {
                   new ParsedState { Name = stateName1, IsInitial = true, Location = Location.None },
                   new ParsedState { Name = stateName2, IsInitial = false, Location = Location.None }
               }
               let events = new[] { new ParsedEvent { Name = eventName, IntValue = 0, Location = Location.None } }
               let handler = new ParsedHandler
               {
                   MethodName = $"Handle{eventName}",
                   FromState = stateName1,
                   ToState = stateName2,
                   Trigger = eventName,
                   EventId = eventName,
                   Kind = HandlerKind.Transition,
                   Signature = new MethodSignature
                   {
                       IsPublic = true,
                       IsStatic = true,
                       ReturnType = "TestState",
                       Parameters = new EquatableArray<ParameterInfo>(ImmutableArray.Create(
                           new ParameterInfo { Name = "state", TypeName = "TestState" },
                           new ParameterInfo { Name = "event", TypeName = "TestEvent" }))
                   },
                   Location = Location.None
               }
               // Model multiple cleanup handlers via Handlers with Kind = Cleanup
               let cleanupHandler1 = new ParsedHandler
               {
                   MethodName = "OnTerminalCleanupA",
                   FromState = "",
                   ToState = "",
                   Trigger = "",
                   EventId = null,
                   Kind = HandlerKind.Cleanup,
                   Signature = new MethodSignature
                   {
                       IsPublic = true,
                       IsStatic = true,
                       ReturnType = "Task",
                       Parameters = new EquatableArray<ParameterInfo>(ImmutableArray.Create(
                           new ParameterInfo { Name = "state", TypeName = "TestState" }))
                   },
                   Location = Location.None
               }
               let cleanupHandler2 = new ParsedHandler
               {
                   MethodName = "OnTerminalCleanupB",
                   FromState = "",
                   ToState = "",
                   Trigger = "",
                   EventId = null,
                   Kind = HandlerKind.Cleanup,
                   Signature = new MethodSignature
                   {
                       IsPublic = true,
                       IsStatic = true,
                       ReturnType = "Task",
                       Parameters = new EquatableArray<ParameterInfo>(ImmutableArray.Create(
                           new ParameterInfo { Name = "state", TypeName = "TestState" }))
                   },
                   Location = Location.None
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
                   Handlers = new EquatableArray<ParsedHandler>(
                       ImmutableArray.Create(handler, cleanupHandler1, cleanupHandler2)),
                   InitialStateName = stateName1,
                   TerminalStateNames = new EquatableArray<string>(ImmutableArray.Create(stateName2)),
                   EntryCallbacks = new EquatableArray<ParsedEntryCallback>(ImmutableArray<ParsedEntryCallback>.Empty),
                   CleanupHandler = new ParsedCleanupHandler
                   {
                       MethodName = "OnTerminalCleanupA",
                       Signature = new MethodSignature
                       {
                           IsPublic = true,
                           IsStatic = true,
                           ReturnType = "Task",
                           Parameters = new EquatableArray<ParameterInfo>(ImmutableArray.Create(
                               new ParameterInfo { Name = "state", TypeName = "TestState" }))
                       },
                       Location = Location.None
                   }
               };
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // Source Generation Helpers (for full-pipeline tests using Roslyn compilation)
    // ═══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Generates source code with a [Flags] enum used as state or event ID.
    /// </summary>
    private static string GenerateFlagsEnumStateMachineSource(string[] memberNames, bool flagsOnState)
    {
        var stateMembers = flagsOnState
            ? string.Join(",\n        ", memberNames.Select((n, i) => $"{n} = {1 << i}"))
            : string.Join(",\n        ", memberNames);
        var eventMembers = !flagsOnState
            ? string.Join(",\n        ", memberNames.Select((n, i) => $"{n} = {1 << i}"))
            : "DoSomething";

        var stateFlagsAttr = flagsOnState ? "[Flags]\n    " : "";
        var eventFlagsAttr = !flagsOnState ? "[Flags]\n    " : "";

        var firstState = flagsOnState ? memberNames[0] : "Idle";
        var secondState = flagsOnState ? (memberNames.Length > 1 ? memberNames[1] : memberNames[0]) : "Running";
        var firstEvent = !flagsOnState ? memberNames[0] : "DoSomething";

        // If flags is on state, we need non-flags event enum and vice versa
        var stateEnumBody = flagsOnState
            ? stateMembers
            : "Idle,\n        Running";
        var eventEnumBody = !flagsOnState
            ? eventMembers
            : "DoSomething";

        if (!flagsOnState)
        {
            firstState = "Idle";
            secondState = "Running";
            stateEnumBody = "Idle,\n        Running";
        }

        return $$"""
            using System;
            using System.Threading.Tasks;
            using StateMachineSrcGen;

            namespace TestNamespace;

            {{stateFlagsAttr}}public enum TestStateId
            {
                {{stateEnumBody}}
            }

            {{eventFlagsAttr}}public enum TestEventId
            {
                {{eventEnumBody}}
            }

            public record TestState(TestStateId Id) : IStateMachineState<TestStateId>
            {
                public TestStateId GetStateId() => Id;
            }

            public record TestEvent(TestEventId EventType) : IDispatchableEvent<TestEventId>
            {
                public TestEventId GetEventId() => EventType;
            }

            [InitialState((int)TestStateId.{{firstState}})]
            public static partial class TestMachine
            {
                [Transition((int)TestStateId.{{firstState}}, (int)TestStateId.{{secondState}}, (int)TestEventId.{{firstEvent}})]
                public static TestState HandleEvent(TestState state, TestEvent @event)
                {
                    return state with { Id = TestStateId.{{secondState}} };
                }
            }
            """;
    }

    /// <summary>
    /// Generates source code with an invalid integer value in [InitialState].
    /// </summary>
    private static string GenerateStateMachineWithInvalidInitialState(string[] stateMembers, int invalidValue)
    {
        var enumMembers = string.Join(",\n        ", stateMembers);
        var firstState = stateMembers[0];
        var secondState = stateMembers.Length > 1 ? stateMembers[1] : stateMembers[0];

        return $$"""
            using System;
            using System.Threading.Tasks;
            using StateMachineSrcGen;

            namespace TestNamespace;

            public enum TestStateId
            {
                {{enumMembers}}
            }

            public enum TestEventId
            {
                DoSomething
            }

            public record TestState(TestStateId Id) : IStateMachineState<TestStateId>
            {
                public TestStateId GetStateId() => Id;
            }

            public record TestEvent(TestEventId EventType) : IDispatchableEvent<TestEventId>
            {
                public TestEventId GetEventId() => EventType;
            }

            [InitialState({{invalidValue}})]
            public static partial class TestMachine
            {
                [Transition((int)TestStateId.{{firstState}}, (int)TestStateId.{{secondState}}, (int)TestEventId.DoSomething)]
                public static TestState HandleEvent(TestState state, TestEvent @event)
                {
                    return state with { Id = TestStateId.{{secondState}} };
                }
            }
            """;
    }

    /// <summary>
    /// Generates source code with an invalid integer value in [Transition] From parameter.
    /// </summary>
    private static string GenerateStateMachineWithInvalidTransitionFrom(string[] stateMembers, int invalidValue)
    {
        var enumMembers = string.Join(",\n        ", stateMembers);
        var firstState = stateMembers[0];
        var secondState = stateMembers.Length > 1 ? stateMembers[1] : stateMembers[0];

        return $$"""
            using System;
            using System.Threading.Tasks;
            using StateMachineSrcGen;

            namespace TestNamespace;

            public enum TestStateId
            {
                {{enumMembers}}
            }

            public enum TestEventId
            {
                DoSomething
            }

            public record TestState(TestStateId Id) : IStateMachineState<TestStateId>
            {
                public TestStateId GetStateId() => Id;
            }

            public record TestEvent(TestEventId EventType) : IDispatchableEvent<TestEventId>
            {
                public TestEventId GetEventId() => EventType;
            }

            [InitialState((int)TestStateId.{{firstState}})]
            public static partial class TestMachine
            {
                [Transition({{invalidValue}}, (int)TestStateId.{{secondState}}, (int)TestEventId.DoSomething)]
                public static TestState HandleEvent(TestState state, TestEvent @event)
                {
                    return state with { Id = TestStateId.{{secondState}} };
                }
            }
            """;
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // Full Pipeline Helper (Parsing → Analysis)
    // ═══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Runs the full pipeline (ParsingPipeline → AnalysisPipeline) on source code
    /// and returns all diagnostics from both stages.
    /// </summary>
    private static ImmutableArray<Diagnostic> RunFullPipeline(string source)
    {
        var compilation = ParsingTestHelper.CreateCompilation(source);
        var syntaxTree = compilation.SyntaxTrees.First();
        var semanticModel = compilation.GetSemanticModel(syntaxTree);
        var root = syntaxTree.GetRoot();

        var classDeclaration = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault();

        if (classDeclaration is null)
            return ImmutableArray<Diagnostic>.Empty;

        // Stage 1: Parsing
        var (parseResult, parseDiagnostics) = ParsingPipeline.Parse(classDeclaration, semanticModel);

        if (parseResult is null)
        {
            // Return parsing diagnostics if parsing failed
            return parseDiagnostics;
        }

        // Stage 2: Analysis
        var (_, analysisDiagnostics) = AnalysisPipeline.Analyze(parseResult.Value);

        // Combine all diagnostics
        return parseDiagnostics.AddRange(analysisDiagnostics);
    }
}
