// Feature: state-machine-source-generator, Property 24: Data model value equality
// **Validates: Requirements 10.2**

using FsCheck;
using FsCheck.Xunit;
using Microsoft.CodeAnalysis;
using StateMachineSrcGen;

namespace StateMachineSrcGen.Tests.DataModel;

/// <summary>
/// Property 24: Data model value equality
/// For any two instances constructed with identical field values, equality comparison shall return true.
/// For any two instances with at least one differing field, equality shall return false.
/// </summary>
public class EqualityProperties
{
    // ─── ParsedState ───────────────────────────────────────────────────────────

    [Property]
    public bool ParsedState_IdenticalInstances_AreEqual(NonEmptyString name, bool isInitial)
    {
        var a = new ParsedState
        {
            Name = name.Get,
            IsInitial = isInitial,
            Location = Location.None
        };
        var b = new ParsedState
        {
            Name = name.Get,
            IsInitial = isInitial,
            Location = Location.None
        };

        return a.Equals(b) && b.Equals(a) && a == b;
    }

    [Property]
    public bool ParsedState_DifferentName_AreNotEqual(NonEmptyString name1, NonEmptyString name2, bool isInitial)
    {
        if (name1.Get == name2.Get) return true; // skip trivial case

        var a = new ParsedState { Name = name1.Get, IsInitial = isInitial, Location = Location.None };
        var b = new ParsedState { Name = name2.Get, IsInitial = isInitial, Location = Location.None };

        return !a.Equals(b) && a != b;
    }

    [Property]
    public bool ParsedState_DifferentIsInitial_AreNotEqual(NonEmptyString name)
    {
        var a = new ParsedState { Name = name.Get, IsInitial = true, Location = Location.None };
        var b = new ParsedState { Name = name.Get, IsInitial = false, Location = Location.None };

        return !a.Equals(b) && a != b;
    }

    [Property]
    public bool ParsedState_EqualInstances_HaveSameHashCode(NonEmptyString name, bool isInitial)
    {
        var a = new ParsedState { Name = name.Get, IsInitial = isInitial, Location = Location.None };
        var b = new ParsedState { Name = name.Get, IsInitial = isInitial, Location = Location.None };

        return a.GetHashCode() == b.GetHashCode();
    }

    // ─── ParsedTrigger ─────────────────────────────────────────────────────────

    [Property]
    public bool ParsedTrigger_IdenticalInstances_AreEqual(NonEmptyString name)
    {
        var a = new ParsedTrigger { Name = name.Get, Location = Location.None };
        var b = new ParsedTrigger { Name = name.Get, Location = Location.None };

        return a.Equals(b) && b.Equals(a) && a == b;
    }

    [Property]
    public bool ParsedTrigger_DifferentName_AreNotEqual(NonEmptyString name1, NonEmptyString name2)
    {
        if (name1.Get == name2.Get) return true; // skip trivial case

        var a = new ParsedTrigger { Name = name1.Get, Location = Location.None };
        var b = new ParsedTrigger { Name = name2.Get, Location = Location.None };

        return !a.Equals(b) && a != b;
    }

    [Property]
    public bool ParsedTrigger_EqualInstances_HaveSameHashCode(NonEmptyString name)
    {
        var a = new ParsedTrigger { Name = name.Get, Location = Location.None };
        var b = new ParsedTrigger { Name = name.Get, Location = Location.None };

        return a.GetHashCode() == b.GetHashCode();
    }

    // ─── ParsedHandler ─────────────────────────────────────────────────────────

    [Property]
    public bool ParsedHandler_IdenticalInstances_AreEqual(
        NonEmptyString methodName,
        NonEmptyString fromState,
        NonEmptyString toState,
        NonEmptyString trigger)
    {
        var sig = new MethodSignature
        {
            IsPublic = true,
            IsStatic = true,
            ReturnType = "MyState",
            Parameters = new EquatableArray<ParameterInfo>(
                System.Collections.Immutable.ImmutableArray.Create(
                    new ParameterInfo { Name = "state", TypeName = "MyState" },
                    new ParameterInfo { Name = "event", TypeName = "MyEvent" }))
        };

        var a = new ParsedHandler
        {
            MethodName = methodName.Get,
            FromState = fromState.Get,
            ToState = toState.Get,
            Trigger = trigger.Get,
            EventId = null,
            Kind = HandlerKind.Transition,
            Signature = sig,
            Location = Location.None
        };
        var b = new ParsedHandler
        {
            MethodName = methodName.Get,
            FromState = fromState.Get,
            ToState = toState.Get,
            Trigger = trigger.Get,
            EventId = null,
            Kind = HandlerKind.Transition,
            Signature = sig,
            Location = Location.None
        };

        return a.Equals(b) && b.Equals(a) && a == b;
    }

    [Property]
    public bool ParsedHandler_DifferentFromState_AreNotEqual(
        NonEmptyString methodName,
        NonEmptyString fromState1,
        NonEmptyString fromState2,
        NonEmptyString toState,
        NonEmptyString trigger)
    {
        if (fromState1.Get == fromState2.Get) return true;

        var sig = new MethodSignature
        {
            IsPublic = true,
            IsStatic = true,
            ReturnType = "MyState",
            Parameters = new EquatableArray<ParameterInfo>(
                System.Collections.Immutable.ImmutableArray.Create(
                    new ParameterInfo { Name = "state", TypeName = "MyState" },
                    new ParameterInfo { Name = "event", TypeName = "MyEvent" }))
        };

        var a = new ParsedHandler
        {
            MethodName = methodName.Get,
            FromState = fromState1.Get,
            ToState = toState.Get,
            Trigger = trigger.Get,
            EventId = null,
            Kind = HandlerKind.Transition,
            Signature = sig,
            Location = Location.None
        };
        var b = new ParsedHandler
        {
            MethodName = methodName.Get,
            FromState = fromState2.Get,
            ToState = toState.Get,
            Trigger = trigger.Get,
            EventId = null,
            Kind = HandlerKind.Transition,
            Signature = sig,
            Location = Location.None
        };

        return !a.Equals(b) && a != b;
    }

    [Property]
    public bool ParsedHandler_DifferentKind_AreNotEqual(
        NonEmptyString methodName,
        NonEmptyString fromState,
        NonEmptyString toState,
        NonEmptyString trigger)
    {
        var sig = new MethodSignature
        {
            IsPublic = true,
            IsStatic = true,
            ReturnType = "MyState",
            Parameters = new EquatableArray<ParameterInfo>(
                System.Collections.Immutable.ImmutableArray.Create(
                    new ParameterInfo { Name = "state", TypeName = "MyState" },
                    new ParameterInfo { Name = "event", TypeName = "MyEvent" }))
        };

        var a = new ParsedHandler
        {
            MethodName = methodName.Get,
            FromState = fromState.Get,
            ToState = toState.Get,
            Trigger = trigger.Get,
            EventId = null,
            Kind = HandlerKind.Transition,
            Signature = sig,
            Location = Location.None
        };
        var b = new ParsedHandler
        {
            MethodName = methodName.Get,
            FromState = fromState.Get,
            ToState = toState.Get,
            Trigger = trigger.Get,
            EventId = null,
            Kind = HandlerKind.Guard,
            Signature = sig,
            Location = Location.None
        };

        return !a.Equals(b) && a != b;
    }

    [Property]
    public bool ParsedHandler_DifferentEventId_AreNotEqual(
        NonEmptyString methodName,
        NonEmptyString fromState,
        NonEmptyString toState,
        NonEmptyString trigger,
        NonEmptyString eventId)
    {
        var sig = new MethodSignature
        {
            IsPublic = true,
            IsStatic = true,
            ReturnType = "MyState",
            Parameters = new EquatableArray<ParameterInfo>(
                System.Collections.Immutable.ImmutableArray.Create(
                    new ParameterInfo { Name = "state", TypeName = "MyState" },
                    new ParameterInfo { Name = "event", TypeName = "MyEvent" }))
        };

        var a = new ParsedHandler
        {
            MethodName = methodName.Get,
            FromState = fromState.Get,
            ToState = toState.Get,
            Trigger = trigger.Get,
            EventId = null,
            Kind = HandlerKind.Transition,
            Signature = sig,
            Location = Location.None
        };
        var b = new ParsedHandler
        {
            MethodName = methodName.Get,
            FromState = fromState.Get,
            ToState = toState.Get,
            Trigger = trigger.Get,
            EventId = eventId.Get,
            Kind = HandlerKind.Transition,
            Signature = sig,
            Location = Location.None
        };

        return !a.Equals(b) && a != b;
    }

    [Property]
    public bool ParsedHandler_EqualInstances_HaveSameHashCode(
        NonEmptyString methodName,
        NonEmptyString fromState,
        NonEmptyString toState,
        NonEmptyString trigger)
    {
        var sig = new MethodSignature
        {
            IsPublic = true,
            IsStatic = true,
            ReturnType = "MyState",
            Parameters = new EquatableArray<ParameterInfo>(
                System.Collections.Immutable.ImmutableArray.Create(
                    new ParameterInfo { Name = "state", TypeName = "MyState" },
                    new ParameterInfo { Name = "event", TypeName = "MyEvent" }))
        };

        var a = new ParsedHandler
        {
            MethodName = methodName.Get,
            FromState = fromState.Get,
            ToState = toState.Get,
            Trigger = trigger.Get,
            EventId = null,
            Kind = HandlerKind.Transition,
            Signature = sig,
            Location = Location.None
        };
        var b = new ParsedHandler
        {
            MethodName = methodName.Get,
            FromState = fromState.Get,
            ToState = toState.Get,
            Trigger = trigger.Get,
            EventId = null,
            Kind = HandlerKind.Transition,
            Signature = sig,
            Location = Location.None
        };

        return a.GetHashCode() == b.GetHashCode();
    }

    // ─── ValidatedState ────────────────────────────────────────────────────────

    [Property]
    public bool ValidatedState_IdenticalInstances_AreEqual(NonEmptyString name, bool isInitial, bool isTerminal)
    {
        var a = new ValidatedState { Name = name.Get, IsInitial = isInitial, IsTerminal = isTerminal };
        var b = new ValidatedState { Name = name.Get, IsInitial = isInitial, IsTerminal = isTerminal };

        return a.Equals(b) && b.Equals(a) && a == b;
    }

    [Property]
    public bool ValidatedState_DifferentName_AreNotEqual(NonEmptyString name1, NonEmptyString name2, bool isInitial, bool isTerminal)
    {
        if (name1.Get == name2.Get) return true;

        var a = new ValidatedState { Name = name1.Get, IsInitial = isInitial, IsTerminal = isTerminal };
        var b = new ValidatedState { Name = name2.Get, IsInitial = isInitial, IsTerminal = isTerminal };

        return !a.Equals(b) && a != b;
    }

    [Property]
    public bool ValidatedState_DifferentIsTerminal_AreNotEqual(NonEmptyString name, bool isInitial)
    {
        var a = new ValidatedState { Name = name.Get, IsInitial = isInitial, IsTerminal = true };
        var b = new ValidatedState { Name = name.Get, IsInitial = isInitial, IsTerminal = false };

        return !a.Equals(b) && a != b;
    }

    [Property]
    public bool ValidatedState_EqualInstances_HaveSameHashCode(NonEmptyString name, bool isInitial, bool isTerminal)
    {
        var a = new ValidatedState { Name = name.Get, IsInitial = isInitial, IsTerminal = isTerminal };
        var b = new ValidatedState { Name = name.Get, IsInitial = isInitial, IsTerminal = isTerminal };

        return a.GetHashCode() == b.GetHashCode();
    }

    // ─── ValidatedTransition ───────────────────────────────────────────────────

    [Property]
    public bool ValidatedTransition_IdenticalInstances_AreEqual(
        NonEmptyString fromState,
        NonEmptyString toState,
        NonEmptyString trigger,
        NonEmptyString eventId,
        NonEmptyString handlerMethodName,
        int declarationOrder)
    {
        var a = new ValidatedTransition
        {
            FromState = fromState.Get,
            ToState = toState.Get,
            Trigger = trigger.Get,
            EventId = eventId.Get,
            HandlerMethodName = handlerMethodName.Get,
            GuardMethodName = null,
            SideEffectMethodName = null,
            DeclarationOrder = declarationOrder
        };
        var b = new ValidatedTransition
        {
            FromState = fromState.Get,
            ToState = toState.Get,
            Trigger = trigger.Get,
            EventId = eventId.Get,
            HandlerMethodName = handlerMethodName.Get,
            GuardMethodName = null,
            SideEffectMethodName = null,
            DeclarationOrder = declarationOrder
        };

        return a.Equals(b) && b.Equals(a) && a == b;
    }

    [Property]
    public bool ValidatedTransition_DifferentFromState_AreNotEqual(
        NonEmptyString fromState1,
        NonEmptyString fromState2,
        NonEmptyString toState,
        NonEmptyString trigger,
        NonEmptyString eventId,
        NonEmptyString handlerMethodName,
        int declarationOrder)
    {
        if (fromState1.Get == fromState2.Get) return true;

        var a = new ValidatedTransition
        {
            FromState = fromState1.Get,
            ToState = toState.Get,
            Trigger = trigger.Get,
            EventId = eventId.Get,
            HandlerMethodName = handlerMethodName.Get,
            GuardMethodName = null,
            SideEffectMethodName = null,
            DeclarationOrder = declarationOrder
        };
        var b = new ValidatedTransition
        {
            FromState = fromState2.Get,
            ToState = toState.Get,
            Trigger = trigger.Get,
            EventId = eventId.Get,
            HandlerMethodName = handlerMethodName.Get,
            GuardMethodName = null,
            SideEffectMethodName = null,
            DeclarationOrder = declarationOrder
        };

        return !a.Equals(b) && a != b;
    }

    [Property]
    public bool ValidatedTransition_DifferentDeclarationOrder_AreNotEqual(
        NonEmptyString fromState,
        NonEmptyString toState,
        NonEmptyString trigger,
        NonEmptyString eventId,
        NonEmptyString handlerMethodName,
        int order1,
        int order2)
    {
        if (order1 == order2) return true;

        var a = new ValidatedTransition
        {
            FromState = fromState.Get,
            ToState = toState.Get,
            Trigger = trigger.Get,
            EventId = eventId.Get,
            HandlerMethodName = handlerMethodName.Get,
            GuardMethodName = null,
            SideEffectMethodName = null,
            DeclarationOrder = order1
        };
        var b = new ValidatedTransition
        {
            FromState = fromState.Get,
            ToState = toState.Get,
            Trigger = trigger.Get,
            EventId = eventId.Get,
            HandlerMethodName = handlerMethodName.Get,
            GuardMethodName = null,
            SideEffectMethodName = null,
            DeclarationOrder = order2
        };

        return !a.Equals(b) && a != b;
    }

    [Property]
    public bool ValidatedTransition_DifferentGuardMethodName_AreNotEqual(
        NonEmptyString fromState,
        NonEmptyString toState,
        NonEmptyString trigger,
        NonEmptyString eventId,
        NonEmptyString handlerMethodName,
        NonEmptyString guardName)
    {
        var a = new ValidatedTransition
        {
            FromState = fromState.Get,
            ToState = toState.Get,
            Trigger = trigger.Get,
            EventId = eventId.Get,
            HandlerMethodName = handlerMethodName.Get,
            GuardMethodName = null,
            SideEffectMethodName = null,
            DeclarationOrder = 0
        };
        var b = new ValidatedTransition
        {
            FromState = fromState.Get,
            ToState = toState.Get,
            Trigger = trigger.Get,
            EventId = eventId.Get,
            HandlerMethodName = handlerMethodName.Get,
            GuardMethodName = guardName.Get,
            SideEffectMethodName = null,
            DeclarationOrder = 0
        };

        return !a.Equals(b) && a != b;
    }

    [Property]
    public bool ValidatedTransition_EqualInstances_HaveSameHashCode(
        NonEmptyString fromState,
        NonEmptyString toState,
        NonEmptyString trigger,
        NonEmptyString eventId,
        NonEmptyString handlerMethodName,
        int declarationOrder)
    {
        var a = new ValidatedTransition
        {
            FromState = fromState.Get,
            ToState = toState.Get,
            Trigger = trigger.Get,
            EventId = eventId.Get,
            HandlerMethodName = handlerMethodName.Get,
            GuardMethodName = null,
            SideEffectMethodName = null,
            DeclarationOrder = declarationOrder
        };
        var b = new ValidatedTransition
        {
            FromState = fromState.Get,
            ToState = toState.Get,
            Trigger = trigger.Get,
            EventId = eventId.Get,
            HandlerMethodName = handlerMethodName.Get,
            GuardMethodName = null,
            SideEffectMethodName = null,
            DeclarationOrder = declarationOrder
        };

        return a.GetHashCode() == b.GetHashCode();
    }

    // ─── ParsedStateMachine ────────────────────────────────────────────────────

    [Property]
    public bool ParsedStateMachine_IdenticalInstances_AreEqual(
        NonEmptyString ns,
        NonEmptyString className,
        NonEmptyString stateTypeName,
        NonEmptyString eventTypeName)
    {
        var states = new EquatableArray<ParsedState>(
            System.Collections.Immutable.ImmutableArray.Create(
                new ParsedState { Name = "Idle", IsInitial = true, Location = Location.None }));
        var triggers = new EquatableArray<ParsedTrigger>(
            System.Collections.Immutable.ImmutableArray.Create(
                new ParsedTrigger { Name = "Start", Location = Location.None }));
        var handlers = new EquatableArray<ParsedHandler>(
            System.Collections.Immutable.ImmutableArray<ParsedHandler>.Empty);

        var a = new ParsedStateMachine
        {
            Namespace = ns.Get,
            ClassName = className.Get,
            StateTypeName = stateTypeName.Get,
            EventTypeName = eventTypeName.Get,
            States = states,
            Triggers = triggers,
            Handlers = handlers,
            Modifiers = ClassModifiers.Public | ClassModifiers.Partial | ClassModifiers.Static,
            ImplementsIDispatchableEvent = true,
            EventIdTypeName = "string",
            Location = Location.None
        };
        var b = new ParsedStateMachine
        {
            Namespace = ns.Get,
            ClassName = className.Get,
            StateTypeName = stateTypeName.Get,
            EventTypeName = eventTypeName.Get,
            States = states,
            Triggers = triggers,
            Handlers = handlers,
            Modifiers = ClassModifiers.Public | ClassModifiers.Partial | ClassModifiers.Static,
            ImplementsIDispatchableEvent = true,
            EventIdTypeName = "string",
            Location = Location.None
        };

        return a.Equals(b) && b.Equals(a) && a == b;
    }

    [Property]
    public bool ParsedStateMachine_DifferentNamespace_AreNotEqual(
        NonEmptyString ns1,
        NonEmptyString ns2,
        NonEmptyString className)
    {
        if (ns1.Get == ns2.Get) return true;

        var states = new EquatableArray<ParsedState>(
            System.Collections.Immutable.ImmutableArray.Create(
                new ParsedState { Name = "Idle", IsInitial = true, Location = Location.None }));
        var triggers = new EquatableArray<ParsedTrigger>(
            System.Collections.Immutable.ImmutableArray<ParsedTrigger>.Empty);
        var handlers = new EquatableArray<ParsedHandler>(
            System.Collections.Immutable.ImmutableArray<ParsedHandler>.Empty);

        var a = new ParsedStateMachine
        {
            Namespace = ns1.Get,
            ClassName = className.Get,
            StateTypeName = "MyState",
            EventTypeName = "MyEvent",
            States = states,
            Triggers = triggers,
            Handlers = handlers,
            Modifiers = ClassModifiers.Public | ClassModifiers.Partial | ClassModifiers.Static,
            ImplementsIDispatchableEvent = true,
            EventIdTypeName = "string",
            Location = Location.None
        };
        var b = new ParsedStateMachine
        {
            Namespace = ns2.Get,
            ClassName = className.Get,
            StateTypeName = "MyState",
            EventTypeName = "MyEvent",
            States = states,
            Triggers = triggers,
            Handlers = handlers,
            Modifiers = ClassModifiers.Public | ClassModifiers.Partial | ClassModifiers.Static,
            ImplementsIDispatchableEvent = true,
            EventIdTypeName = "string",
            Location = Location.None
        };

        return !a.Equals(b) && a != b;
    }

    [Property]
    public bool ParsedStateMachine_DifferentModifiers_AreNotEqual(NonEmptyString ns, NonEmptyString className)
    {
        var states = new EquatableArray<ParsedState>(
            System.Collections.Immutable.ImmutableArray.Create(
                new ParsedState { Name = "Idle", IsInitial = true, Location = Location.None }));
        var triggers = new EquatableArray<ParsedTrigger>(
            System.Collections.Immutable.ImmutableArray<ParsedTrigger>.Empty);
        var handlers = new EquatableArray<ParsedHandler>(
            System.Collections.Immutable.ImmutableArray<ParsedHandler>.Empty);

        var a = new ParsedStateMachine
        {
            Namespace = ns.Get,
            ClassName = className.Get,
            StateTypeName = "MyState",
            EventTypeName = "MyEvent",
            States = states,
            Triggers = triggers,
            Handlers = handlers,
            Modifiers = ClassModifiers.Public | ClassModifiers.Partial | ClassModifiers.Static,
            ImplementsIDispatchableEvent = true,
            EventIdTypeName = "string",
            Location = Location.None
        };
        var b = new ParsedStateMachine
        {
            Namespace = ns.Get,
            ClassName = className.Get,
            StateTypeName = "MyState",
            EventTypeName = "MyEvent",
            States = states,
            Triggers = triggers,
            Handlers = handlers,
            Modifiers = ClassModifiers.Public | ClassModifiers.Partial, // missing Static
            ImplementsIDispatchableEvent = true,
            EventIdTypeName = "string",
            Location = Location.None
        };

        return !a.Equals(b) && a != b;
    }


    [Property]
    public bool ParsedStateMachine_EqualInstances_HaveSameHashCode(
        NonEmptyString ns,
        NonEmptyString className,
        NonEmptyString stateTypeName,
        NonEmptyString eventTypeName)
    {
        var states = new EquatableArray<ParsedState>(
            System.Collections.Immutable.ImmutableArray.Create(
                new ParsedState { Name = "Idle", IsInitial = true, Location = Location.None }));
        var triggers = new EquatableArray<ParsedTrigger>(
            System.Collections.Immutable.ImmutableArray.Create(
                new ParsedTrigger { Name = "Start", Location = Location.None }));
        var handlers = new EquatableArray<ParsedHandler>(
            System.Collections.Immutable.ImmutableArray<ParsedHandler>.Empty);

        var a = new ParsedStateMachine
        {
            Namespace = ns.Get,
            ClassName = className.Get,
            StateTypeName = stateTypeName.Get,
            EventTypeName = eventTypeName.Get,
            States = states,
            Triggers = triggers,
            Handlers = handlers,
            Modifiers = ClassModifiers.Public | ClassModifiers.Partial | ClassModifiers.Static,
            ImplementsIDispatchableEvent = true,
            EventIdTypeName = "string",
            Location = Location.None
        };
        var b = new ParsedStateMachine
        {
            Namespace = ns.Get,
            ClassName = className.Get,
            StateTypeName = stateTypeName.Get,
            EventTypeName = eventTypeName.Get,
            States = states,
            Triggers = triggers,
            Handlers = handlers,
            Modifiers = ClassModifiers.Public | ClassModifiers.Partial | ClassModifiers.Static,
            ImplementsIDispatchableEvent = true,
            EventIdTypeName = "string",
            Location = Location.None
        };

        return a.GetHashCode() == b.GetHashCode();
    }

    // ─── ValidatedStateMachine ─────────────────────────────────────────────────

    [Property]
    public bool ValidatedStateMachine_IdenticalInstances_AreEqual(
        NonEmptyString ns,
        NonEmptyString className,
        NonEmptyString stateTypeName,
        NonEmptyString eventTypeName,
        NonEmptyString eventIdTypeName)
    {
        var initialState = new ValidatedState { Name = "Idle", IsInitial = true, IsTerminal = false };
        var states = new EquatableArray<ValidatedState>(
            System.Collections.Immutable.ImmutableArray.Create(initialState));
        var transitions = new EquatableArray<ValidatedTransition>(
            System.Collections.Immutable.ImmutableArray<ValidatedTransition>.Empty);

        var a = new ValidatedStateMachine
        {
            Namespace = ns.Get,
            ClassName = className.Get,
            StateTypeName = stateTypeName.Get,
            EventTypeName = eventTypeName.Get,
            EventIdTypeName = eventIdTypeName.Get,
            States = states,
            InitialState = initialState,
            Transitions = transitions
        };
        var b = new ValidatedStateMachine
        {
            Namespace = ns.Get,
            ClassName = className.Get,
            StateTypeName = stateTypeName.Get,
            EventTypeName = eventTypeName.Get,
            EventIdTypeName = eventIdTypeName.Get,
            States = states,
            InitialState = initialState,
            Transitions = transitions
        };

        return a.Equals(b) && b.Equals(a) && a == b;
    }

    [Property]
    public bool ValidatedStateMachine_DifferentClassName_AreNotEqual(
        NonEmptyString ns,
        NonEmptyString className1,
        NonEmptyString className2)
    {
        if (className1.Get == className2.Get) return true;

        var initialState = new ValidatedState { Name = "Idle", IsInitial = true, IsTerminal = false };
        var states = new EquatableArray<ValidatedState>(
            System.Collections.Immutable.ImmutableArray.Create(initialState));
        var transitions = new EquatableArray<ValidatedTransition>(
            System.Collections.Immutable.ImmutableArray<ValidatedTransition>.Empty);

        var a = new ValidatedStateMachine
        {
            Namespace = ns.Get,
            ClassName = className1.Get,
            StateTypeName = "MyState",
            EventTypeName = "MyEvent",
            EventIdTypeName = "string",
            States = states,
            InitialState = initialState,
            Transitions = transitions
        };
        var b = new ValidatedStateMachine
        {
            Namespace = ns.Get,
            ClassName = className2.Get,
            StateTypeName = "MyState",
            EventTypeName = "MyEvent",
            EventIdTypeName = "string",
            States = states,
            InitialState = initialState,
            Transitions = transitions
        };

        return !a.Equals(b) && a != b;
    }

    [Property]
    public bool ValidatedStateMachine_DifferentInitialState_AreNotEqual(
        NonEmptyString ns,
        NonEmptyString className,
        NonEmptyString stateName1,
        NonEmptyString stateName2)
    {
        if (stateName1.Get == stateName2.Get) return true;

        var initialState1 = new ValidatedState { Name = stateName1.Get, IsInitial = true, IsTerminal = false };
        var initialState2 = new ValidatedState { Name = stateName2.Get, IsInitial = true, IsTerminal = false };
        var states1 = new EquatableArray<ValidatedState>(
            System.Collections.Immutable.ImmutableArray.Create(initialState1));
        var states2 = new EquatableArray<ValidatedState>(
            System.Collections.Immutable.ImmutableArray.Create(initialState2));
        var transitions = new EquatableArray<ValidatedTransition>(
            System.Collections.Immutable.ImmutableArray<ValidatedTransition>.Empty);

        var a = new ValidatedStateMachine
        {
            Namespace = ns.Get,
            ClassName = className.Get,
            StateTypeName = "MyState",
            EventTypeName = "MyEvent",
            EventIdTypeName = "string",
            States = states1,
            InitialState = initialState1,
            Transitions = transitions
        };
        var b = new ValidatedStateMachine
        {
            Namespace = ns.Get,
            ClassName = className.Get,
            StateTypeName = "MyState",
            EventTypeName = "MyEvent",
            EventIdTypeName = "string",
            States = states2,
            InitialState = initialState2,
            Transitions = transitions
        };

        return !a.Equals(b) && a != b;
    }

    [Property]
    public bool ValidatedStateMachine_EqualInstances_HaveSameHashCode(
        NonEmptyString ns,
        NonEmptyString className,
        NonEmptyString stateTypeName,
        NonEmptyString eventTypeName,
        NonEmptyString eventIdTypeName)
    {
        var initialState = new ValidatedState { Name = "Idle", IsInitial = true, IsTerminal = false };
        var states = new EquatableArray<ValidatedState>(
            System.Collections.Immutable.ImmutableArray.Create(initialState));
        var transitions = new EquatableArray<ValidatedTransition>(
            System.Collections.Immutable.ImmutableArray<ValidatedTransition>.Empty);

        var a = new ValidatedStateMachine
        {
            Namespace = ns.Get,
            ClassName = className.Get,
            StateTypeName = stateTypeName.Get,
            EventTypeName = eventTypeName.Get,
            EventIdTypeName = eventIdTypeName.Get,
            States = states,
            InitialState = initialState,
            Transitions = transitions
        };
        var b = new ValidatedStateMachine
        {
            Namespace = ns.Get,
            ClassName = className.Get,
            StateTypeName = stateTypeName.Get,
            EventTypeName = eventTypeName.Get,
            EventIdTypeName = eventIdTypeName.Get,
            States = states,
            InitialState = initialState,
            Transitions = transitions
        };

        return a.GetHashCode() == b.GetHashCode();
    }
}
