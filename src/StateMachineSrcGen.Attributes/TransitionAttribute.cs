using System;

namespace StateMachineSrcGen;

/// <summary>Marks a method as a transition handler.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class TransitionAttribute : Attribute
{
    /// <summary>Gets the source state enum value for this transition.</summary>
    public int From { get; }

    /// <summary>Gets the target state enum value for this transition.</summary>
    public int To { get; }

    /// <summary>Gets the trigger enum value that activates this transition.</summary>
    public int Trigger { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="TransitionAttribute"/> class.
    /// </summary>
    /// <param name="from">The source state enum value (cast from state ID enum).</param>
    /// <param name="to">The target state enum value (cast from state ID enum).</param>
    /// <param name="trigger">The trigger enum value (cast from event ID enum).</param>
    public TransitionAttribute(int from, int to, int trigger)
    {
        From = from;
        To = to;
        Trigger = trigger;
    }
}
