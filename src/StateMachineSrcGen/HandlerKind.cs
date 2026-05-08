namespace StateMachineSrcGen;

/// <summary>
/// Classifies the kind of handler method in a state machine definition.
/// </summary>
public enum HandlerKind
{
    /// <summary>A transition handler that produces a new state.</summary>
    Transition,

    /// <summary>A guard that determines whether a transition should proceed.</summary>
    Guard,

    /// <summary>A side effect that runs after a successful transition.</summary>
    SideEffect,

    /// <summary>A state-entry callback invoked when entering a state.</summary>
    EntryCallback,

    /// <summary>A cleanup handler invoked when reaching a terminal state.</summary>
    Cleanup
}
