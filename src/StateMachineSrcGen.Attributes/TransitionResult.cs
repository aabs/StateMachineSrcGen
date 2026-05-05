namespace StateMachineSrcGen;

/// <summary>Result of a transition attempt.</summary>
public enum TransitionResult
{
    /// <summary>The transition completed successfully.</summary>
    Success,

    /// <summary>No matching transition was found for the current state and event.</summary>
    NotHandled,

    /// <summary>The lock could not be acquired, preventing the transition.</summary>
    LockFailed
}
