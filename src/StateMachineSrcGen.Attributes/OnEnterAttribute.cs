using System;

namespace StateMachineSrcGen;

/// <summary>Marks a method as a state-entry callback.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class OnEnterAttribute : Attribute
{
    /// <summary>Gets the target state ID, or -1 for catch-all.</summary>
    public int State { get; }

    /// <summary>Gets a value indicating whether this is a catch-all entry callback.</summary>
    public bool IsCatchAll { get; }

    /// <summary>
    /// Initializes a targeted entry callback for a specific state.
    /// </summary>
    /// <param name="state">The state ID enum value cast to int (e.g., <c>(int)StateId.Confirmed</c>).</param>
    public OnEnterAttribute(int state)
    {
        State = state;
        IsCatchAll = false;
    }

    /// <summary>
    /// Initializes a catch-all entry callback invoked on every state entry.
    /// </summary>
    public OnEnterAttribute()
    {
        State = -1;
        IsCatchAll = true;
    }
}
