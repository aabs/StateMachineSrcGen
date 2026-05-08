using System;

namespace StateMachineSrcGen;

/// <summary>Marks a method as a side effect for a transition.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class SideEffectAttribute : Attribute
{
    /// <summary>Gets the source state enum value for this side effect.</summary>
    public int From { get; }

    /// <summary>Gets the target state enum value for this side effect.</summary>
    public int To { get; }

    /// <summary>Gets the trigger enum value that this side effect applies to.</summary>
    public int Trigger { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SideEffectAttribute"/> class.
    /// </summary>
    /// <param name="from">The source state enum value (cast from state ID enum).</param>
    /// <param name="to">The target state enum value (cast from state ID enum).</param>
    /// <param name="trigger">The trigger enum value (cast from event ID enum).</param>
    public SideEffectAttribute(int from, int to, int trigger)
    {
        From = from;
        To = to;
        Trigger = trigger;
    }
}
