namespace StateMachineSrcGen;

/// <summary>
/// Represents the outcome of a state machine transition attempt.
/// </summary>
public enum TransitionOutcome
{
    /// <summary>The transition completed successfully.</summary>
    Success,

    /// <summary>A guard rejected the transition.</summary>
    GuardRejected,

    /// <summary>No matching transition was found for the current state and event.</summary>
    NoTransition
}

/// <summary>
/// Result of a state machine transition attempt, carrying the outcome
/// and the resulting state on success.
/// </summary>
/// <typeparam name="TState">The state type produced by the transition.</typeparam>
public readonly struct TransitionResult<TState>
{
    /// <summary>Gets the outcome of the transition attempt.</summary>
    public TransitionOutcome Outcome { get; }

    /// <summary>
    /// Gets the resulting state after a successful transition, or default if the transition did not succeed.
    /// </summary>
    public TState? State { get; }

    private TransitionResult(TransitionOutcome outcome, TState? state)
    {
        Outcome = outcome;
        State = state;
    }

    /// <summary>
    /// Creates a successful transition result carrying the new state.
    /// </summary>
    /// <param name="newState">The state produced by the transition.</param>
    /// <returns>A result indicating success with the new state.</returns>
    public static TransitionResult<TState> Succeeded(TState newState)
        => new(TransitionOutcome.Success, newState);

    /// <summary>
    /// Gets a result indicating that a guard rejected the transition.
    /// </summary>
    public static TransitionResult<TState> GuardRejected
        => new(TransitionOutcome.GuardRejected, default);

    /// <summary>
    /// Gets a result indicating that no matching transition was found.
    /// </summary>
    public static TransitionResult<TState> NoTransition
        => new(TransitionOutcome.NoTransition, default);

    /// <summary>
    /// Gets a value indicating whether the transition succeeded.
    /// </summary>
    public bool IsSuccess => Outcome == TransitionOutcome.Success;
}
