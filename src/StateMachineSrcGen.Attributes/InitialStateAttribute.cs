using System;

namespace StateMachineSrcGen;

/// <summary>Designates the initial state of the state machine.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class InitialStateAttribute : Attribute
{
    /// <summary>Gets the state ID enum value (cast to int) representing the initial state.</summary>
    public int State { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="InitialStateAttribute"/> class.
    /// </summary>
    /// <param name="state">The state ID enum value cast to int (e.g., <c>(int)StateId.Pending</c>).</param>
    public InitialStateAttribute(int state) => State = state;
}
