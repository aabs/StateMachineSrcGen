using System;

namespace StateMachineSrcGen;

/// <summary>
/// Interface that a state type must implement to participate in state machine transitions.
/// The state ID is used by generated dispatch logic to compare the current state against
/// declared state names. The full state object is what gets persisted and passed to handlers.
/// </summary>
/// <typeparam name="TStateId">
/// The type of the state identifier used for transition comparisons.
/// Must implement <see cref="IEquatable{T}"/> for reliable equality checks.
/// </typeparam>
public interface IStateMachineState<TStateId>
    where TStateId : IEquatable<TStateId>
{
    /// <summary>
    /// Returns the state identifier used by the generated dispatch logic
    /// to determine which transitions are valid from the current state.
    /// </summary>
    /// <returns>The state identifier for transition routing.</returns>
    TStateId GetStateId();
}
