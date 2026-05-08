using System;

namespace StateMachineSrcGen;

/// <summary>Designates a terminal/final state of the state machine.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class TerminalStateAttribute : Attribute
{
    /// <summary>Gets the state ID enum value (cast to int) representing a terminal state.</summary>
    public int State { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="TerminalStateAttribute"/> class.
    /// </summary>
    /// <param name="state">The state ID enum value cast to int (e.g., <c>(int)StateId.Cancelled</c>).</param>
    public TerminalStateAttribute(int state) => State = state;
}
