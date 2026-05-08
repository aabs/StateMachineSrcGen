using System;

namespace StateMachineSrcGen;

/// <summary>Marks a method as a guard for a transition.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class GuardAttribute : Attribute
{
    /// <summary>Gets the source state enum value for this guard.</summary>
    public int From { get; }

    /// <summary>Gets the target state enum value for this guard.</summary>
    public int To { get; }

    /// <summary>Gets the trigger enum value that this guard applies to.</summary>
    public int Trigger { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="GuardAttribute"/> class.
    /// </summary>
    /// <param name="from">The source state enum value (cast from state ID enum).</param>
    /// <param name="to">The target state enum value (cast from state ID enum).</param>
    /// <param name="trigger">The trigger enum value (cast from event ID enum).</param>
    public GuardAttribute(int from, int to, int trigger)
    {
        From = from;
        To = to;
        Trigger = trigger;
    }
}
