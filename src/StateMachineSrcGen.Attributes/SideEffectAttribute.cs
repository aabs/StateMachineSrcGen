using System;

namespace StateMachineSrcGen;

/// <summary>Marks a method as a side effect for a transition.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class SideEffectAttribute : Attribute
{
    /// <summary>Gets the source state name for this side effect.</summary>
    public string From { get; }

    /// <summary>Gets the target state name for this side effect.</summary>
    public string To { get; }

    /// <summary>Gets the trigger name that this side effect applies to.</summary>
    public string Trigger { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SideEffectAttribute"/> class.
    /// </summary>
    /// <param name="from">The source state name.</param>
    /// <param name="to">The target state name.</param>
    /// <param name="trigger">The trigger name that this side effect applies to.</param>
    public SideEffectAttribute(string from, string to, string trigger)
    {
        From = from;
        To = to;
        Trigger = trigger;
    }
}
