using System;

namespace StateMachineSrcGen;

/// <summary>Declares a trigger (event type) in the state machine.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class TriggerAttribute : Attribute
{
    /// <summary>Gets the name of the trigger.</summary>
    public string Name { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="TriggerAttribute"/> class.
    /// </summary>
    /// <param name="name">The name of the trigger.</param>
    public TriggerAttribute(string name)
    {
        Name = name;
    }
}
