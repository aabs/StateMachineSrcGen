// Feature: generic-state-machine-api, Property 31: Lock lifecycle correctness
// **Validates: Requirements 7.5**

using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using StateMachineSrcGen;
using StateMachineSrcGen.Generation;
using StateMachineSrcGen.Tests.Generation;
using Xunit;

namespace StateMachineSrcGen.Tests.Orchestration;

/// <summary>
/// Property 31: Lock lifecycle correctness — lock acquired before operations, released after (or on failure).
/// The new design acquires the lock unconditionally and releases in a finally block.
/// </summary>
public class LockProperties
{
    /// <summary>
    /// Property 31: Lock is acquired before any persistence or handler operations,
    /// and released after all operations complete (or upon any failure).
    /// Verified by inspecting generated source structure.
    /// </summary>
    [Property]
    public bool LockLifecycle_AcquiredBeforeOps_ReleasedAfter(PositiveInt seed)
    {
        var idle = new ValidatedState { Name = "Idle", EnumValue = 0, IsInitial = true, IsTerminal = false };
        var running = new ValidatedState { Name = "Running", EnumValue = 1, IsInitial = false, IsTerminal = true };

        var input = new ValidatedStateMachine
        {
            Namespace = "TestNs",
            ClassName = "LockLife",
            StateTypeName = "TestState",
            EventTypeName = "TestEvent",
            StateIdEnumTypeName = "TestStateId",
            EventIdEnumTypeName = "TestEventId",
            States = new EquatableArray<ValidatedState>(ImmutableArray.Create(idle, running)),
            InitialState = idle,
            Transitions = new EquatableArray<ValidatedTransition>(ImmutableArray.Create(
                new ValidatedTransition
                {
                    FromState = "Idle", ToState = "Running", Trigger = "Start",
                    EventId = "Start", HandlerMethodName = "HandleStart",
                    GuardMethodName = null, SideEffectMethodName = null,
                    FromStateEnumValue = 0, ToStateEnumValue = 1, TriggerEnumValue = 0,
                    IsTerminal = true, DeclarationOrder = 0
                })),
            EntryCallbacks = new EquatableArray<ValidatedEntryCallback>(ImmutableArray<ValidatedEntryCallback>.Empty),
            CleanupHandlerMethodName = null
        };

        var (source, _) = GenerationPipeline.Generate(input);
        if (source == null) return false;

        // Verify:
        // 1. AcquireAsync is called first
        // 2. LoadAsync, handler, SaveAsync are inside try block (after acquire)
        // 3. ReleaseAsync is in finally block
        var acquireIdx = source.IndexOf("AcquireAsync", StringComparison.Ordinal);
        var tryIdx = source.IndexOf("try", acquireIdx, StringComparison.Ordinal);
        var loadIdx = source.IndexOf("LoadAsync", StringComparison.Ordinal);
        var finallyIdx = source.IndexOf("finally", StringComparison.Ordinal);
        var releaseIdx = source.IndexOf("ReleaseAsync", StringComparison.Ordinal);

        return acquireIdx >= 0 && tryIdx >= 0 && loadIdx >= 0 &&
               finallyIdx >= 0 && releaseIdx >= 0 &&
               acquireIdx < tryIdx &&
               tryIdx < loadIdx &&
               finallyIdx < releaseIdx;
    }

    /// <summary>
    /// Property 31: Lock release happens even when operations fail (try/finally pattern).
    /// </summary>
    [Property]
    public bool LockLifecycle_ReleasedOnFailure_TryFinallyPattern(PositiveInt seed)
    {
        var idle = new ValidatedState { Name = "Idle", EnumValue = 0, IsInitial = true, IsTerminal = false };
        var running = new ValidatedState { Name = "Running", EnumValue = 1, IsInitial = false, IsTerminal = true };

        var input = new ValidatedStateMachine
        {
            Namespace = "TestNs",
            ClassName = "LockFinally",
            StateTypeName = "TestState",
            EventTypeName = "TestEvent",
            StateIdEnumTypeName = "TestStateId",
            EventIdEnumTypeName = "TestEventId",
            States = new EquatableArray<ValidatedState>(ImmutableArray.Create(idle, running)),
            InitialState = idle,
            Transitions = new EquatableArray<ValidatedTransition>(ImmutableArray.Create(
                new ValidatedTransition
                {
                    FromState = "Idle", ToState = "Running", Trigger = "Start",
                    EventId = "Start", HandlerMethodName = "HandleStart",
                    GuardMethodName = null, SideEffectMethodName = null,
                    FromStateEnumValue = 0, ToStateEnumValue = 1, TriggerEnumValue = 0,
                    IsTerminal = true, DeclarationOrder = 0
                })),
            EntryCallbacks = new EquatableArray<ValidatedEntryCallback>(ImmutableArray<ValidatedEntryCallback>.Empty),
            CleanupHandlerMethodName = null
        };

        var (source, _) = GenerationPipeline.Generate(input);
        if (source == null) return false;

        // The generated code must use try/finally to ensure lock release
        var hasTry = source.Contains("try");
        var hasFinally = source.Contains("finally");
        var hasRelease = source.Contains("ReleaseAsync");

        // ReleaseAsync must be inside the finally block
        var finallyIdx = source.IndexOf("finally", StringComparison.Ordinal);
        var releaseIdx = source.IndexOf("ReleaseAsync", StringComparison.Ordinal);

        return hasTry && hasFinally && hasRelease && finallyIdx < releaseIdx;
    }
}
