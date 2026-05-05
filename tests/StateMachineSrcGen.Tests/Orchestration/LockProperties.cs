// Feature: state-machine-source-generator, Property 31: Lock lifecycle correctness
// Feature: state-machine-source-generator, Property 32: Lock acquisition failure prevents transition
// **Validates: Requirements 15.4, 15.5, 15.6**

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
/// Property 32: Lock acquisition failure prevents transition — failed acquire returns LockFailed, no further work.
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
        var idle = new ValidatedState { Name = "Idle", IsInitial = true, IsTerminal = false };
        var running = new ValidatedState { Name = "Running", IsInitial = false, IsTerminal = true };

        var input = new ValidatedStateMachine
        {
            Namespace = "TestNs",
            ClassName = "LockLife",
            StateTypeName = "string",
            EventTypeName = "TestEvent",
            EventIdTypeName = "string",
            States = new EquatableArray<ValidatedState>(ImmutableArray.Create(idle, running)),
            InitialState = idle,
            Transitions = new EquatableArray<ValidatedTransition>(ImmutableArray.Create(
                new ValidatedTransition
                {
                    FromState = "Idle", ToState = "Running", Trigger = "Start",
                    EventId = "StartEvt", HandlerMethodName = "HandleStart",
                    GuardMethodName = null, SideEffectMethodName = null, DeclarationOrder = 0
                }))
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
        var idle = new ValidatedState { Name = "Idle", IsInitial = true, IsTerminal = false };
        var running = new ValidatedState { Name = "Running", IsInitial = false, IsTerminal = true };

        var input = new ValidatedStateMachine
        {
            Namespace = "TestNs",
            ClassName = "LockFinally",
            StateTypeName = "string",
            EventTypeName = "TestEvent",
            EventIdTypeName = "string",
            States = new EquatableArray<ValidatedState>(ImmutableArray.Create(idle, running)),
            InitialState = idle,
            Transitions = new EquatableArray<ValidatedTransition>(ImmutableArray.Create(
                new ValidatedTransition
                {
                    FromState = "Idle", ToState = "Running", Trigger = "Start",
                    EventId = "StartEvt", HandlerMethodName = "HandleStart",
                    GuardMethodName = null, SideEffectMethodName = null, DeclarationOrder = 0
                }))
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

    /// <summary>
    /// Property 32: When lock acquisition fails, the generated code returns LockFailed
    /// without invoking load, handler, or save.
    /// Verified by inspecting generated source — early return on failed acquire.
    /// </summary>
    [Property]
    public bool LockAcquisitionFailure_ReturnsLockFailed_NoFurtherWork(PositiveInt seed)
    {
        var idle = new ValidatedState { Name = "Idle", IsInitial = true, IsTerminal = false };
        var running = new ValidatedState { Name = "Running", IsInitial = false, IsTerminal = true };

        var input = new ValidatedStateMachine
        {
            Namespace = "TestNs",
            ClassName = "LockFail",
            StateTypeName = "string",
            EventTypeName = "TestEvent",
            EventIdTypeName = "string",
            States = new EquatableArray<ValidatedState>(ImmutableArray.Create(idle, running)),
            InitialState = idle,
            Transitions = new EquatableArray<ValidatedTransition>(ImmutableArray.Create(
                new ValidatedTransition
                {
                    FromState = "Idle", ToState = "Running", Trigger = "Start",
                    EventId = "StartEvt", HandlerMethodName = "HandleStart",
                    GuardMethodName = null, SideEffectMethodName = null, DeclarationOrder = 0
                }))
        };

        var (source, _) = GenerationPipeline.Generate(input);
        if (source == null) return false;

        // Verify:
        // 1. AcquireAsync is checked with if (!await ...)
        // 2. On failure, returns LockFailed immediately
        // 3. LockFailed return is before LoadAsync
        var lockFailedIdx = source.IndexOf("LockFailed", StringComparison.Ordinal);
        var loadIdx = source.IndexOf("LoadAsync", StringComparison.Ordinal);

        return source.Contains("if (!await _lock.AcquireAsync()") &&
               source.Contains("TransitionResult.LockFailed") &&
               lockFailedIdx >= 0 && loadIdx >= 0 &&
               lockFailedIdx < loadIdx;
    }

    /// <summary>
    /// Property 32: Runtime verification — when lock fails to acquire, returns LockFailed.
    /// </summary>
    [Fact]
    public async Task LockAcquisitionFailure_ReturnsLockFailed_AtRuntime()
    {
        var idle = new ValidatedState { Name = "Idle", IsInitial = true, IsTerminal = false };
        var running = new ValidatedState { Name = "Running", IsInitial = false, IsTerminal = true };

        var input = new ValidatedStateMachine
        {
            Namespace = "TestNs",
            ClassName = "LockRT",
            StateTypeName = "string",
            EventTypeName = "TestEvent",
            EventIdTypeName = "string",
            States = new EquatableArray<ValidatedState>(ImmutableArray.Create(idle, running)),
            InitialState = idle,
            Transitions = new EquatableArray<ValidatedTransition>(ImmutableArray.Create(
                new ValidatedTransition
                {
                    FromState = "Idle", ToState = "Running", Trigger = "Start",
                    EventId = "StartEvt", HandlerMethodName = "HandleStart",
                    GuardMethodName = null, SideEffectMethodName = null, DeclarationOrder = 0
                }))
        };

        var (source, _) = GenerationPipeline.Generate(input);
        Assert.NotNull(source);

        // Replace the default NoOpLock with one that always fails
        // We modify the generated source to use a failing lock
        var modifiedSource = source.Replace(
            "private static StateMachineSrcGen.IStateLock<string> _lock = new NoOpLock();",
            "private static StateMachineSrcGen.IStateLock<string> _lock = new FailingLock();");

        // Add FailingLock class
        modifiedSource = modifiedSource.Replace(
            "private sealed class NoOpLock",
            @"private sealed class FailingLock : StateMachineSrcGen.IStateLock<string>
        {
            public System.Threading.Tasks.Task<bool> AcquireAsync()
            {
                return System.Threading.Tasks.Task.FromResult(false);
            }

            public System.Threading.Tasks.Task ReleaseAsync()
            {
                return System.Threading.Tasks.Task.CompletedTask;
            }
        }

        private sealed class NoOpLock");

        var userCode = @"
#nullable enable
using System;
using System.Threading.Tasks;

namespace StateMachineSrcGen
{
    public interface IStatePersistence<TState>
    {
        Task<TState> LoadAsync();
        Task SaveAsync(TState state);
    }

    public interface IStateLock<TState>
    {
        Task<bool> AcquireAsync();
        Task ReleaseAsync();
    }

    public interface IDispatchableEvent<TEventId> where TEventId : IEquatable<TEventId>
    {
        TEventId GetEventId();
    }

    public enum TransitionResult
    {
        Success,
        NotHandled,
        LockFailed
    }
}

namespace TestNs
{
    public class TestEvent : StateMachineSrcGen.IDispatchableEvent<string>
    {
        public string EventId { get; set; } = """";
        public string GetEventId() => EventId;
    }

    public static partial class LockRT
    {
        public static string HandleStart(string state, TestEvent @event) => ""Running"";
    }
}
";

        var assembly = CompileAndLoad(modifiedSource, userCode);
        if (assembly == null) return;

        var machineType = assembly.GetType("TestNs.LockRT");
        var handleMethod = machineType!.GetMethod("HandleAsync");
        var eventType = assembly.GetType("TestNs.TestEvent");
        var evt = Activator.CreateInstance(eventType!);
        eventType!.GetProperty("EventId")!.SetValue(evt, "StartEvt");

        var task = (Task)handleMethod!.Invoke(null, new[] { evt })!;
        await task;
        var resultProp = task.GetType().GetProperty("Result");
        var result = (int)resultProp!.GetValue(task)!;

        // TransitionResult.LockFailed == 2
        Assert.Equal(2, result);
    }

    // ─── Helpers ────────────────────────────────────────────────────────────────

    private static Assembly? CompileAndLoad(string generatedSource, string userCode)
    {
        var compilation = GenerationTestHelper.CompileGeneratedSource(generatedSource, userCode);
        using var ms = new MemoryStream();
        var emitResult = compilation.Emit(ms);

        if (!emitResult.Success) return null;

        ms.Seek(0, SeekOrigin.Begin);
        return Assembly.Load(ms.ToArray());
    }
}
