using System;

namespace StateMachineSrcGen;

/// <summary>Marks a method as a transition handler.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class TransitionAttribute : Attribute
{
    /// <summary>Gets the source state name for this transition.</summary>
    public string From { get; }

    /// <summary>Gets the target state name for this transition.</summary>
    public string To { get; }

    /// <summary>Gets the trigger name that activates this transition.</summary>
    public string Trigger { get; }

    /// <summary>
    /// The event ID value that this handler responds to.
    /// Used by the generated dispatch switch to route events to the correct handler.
    /// </summary>
    public object? EventId { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="TransitionAttribute"/> class.
    /// </summary>
    /// <param name="from">The source state name.</param>
    /// <param name="to">The target state name.</param>
    /// <param name="trigger">The trigger name that activates this transition.</param>
    public TransitionAttribute(string from, string to, string trigger)
    {
        From = from;
        To = to;
        Trigger = trigger;
    }
}
