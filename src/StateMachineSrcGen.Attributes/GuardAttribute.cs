using System;

namespace StateMachineSrcGen;

/// <summary>Marks a method as a guard for a transition.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class GuardAttribute : Attribute
{
    /// <summary>Gets the source state name for this guard.</summary>
    public string From { get; }

    /// <summary>Gets the target state name for this guard.</summary>
    public string To { get; }

    /// <summary>Gets the trigger name that this guard applies to.</summary>
    public string Trigger { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="GuardAttribute"/> class.
    /// </summary>
    /// <param name="from">The source state name.</param>
    /// <param name="to">The target state name.</param>
    /// <param name="trigger">The trigger name that this guard applies to.</param>
    public GuardAttribute(string from, string to, string trigger)
    {
        From = from;
        To = to;
        Trigger = trigger;
    }
}
