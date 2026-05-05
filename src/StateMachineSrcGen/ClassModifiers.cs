using System;

namespace StateMachineSrcGen;

/// <summary>
/// Flags representing the modifiers on a state machine class declaration.
/// </summary>
[Flags]
public enum ClassModifiers
{
    /// <summary>No modifiers.</summary>
    None = 0,

    /// <summary>The class is public.</summary>
    Public = 1,

    /// <summary>The class is partial.</summary>
    Partial = 2,

    /// <summary>The class is static.</summary>
    Static = 4
}
