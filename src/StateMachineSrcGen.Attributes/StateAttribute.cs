using System;

namespace StateMachineSrcGen;

/// <summary>Declares a state in the state machine.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class StateAttribute : Attribute
{
    /// <summary>Gets the name of the state.</summary>
    public string Name { get; }

    /// <summary>Gets or sets a value indicating whether this state is the initial state.</summary>
    public bool IsInitial { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="StateAttribute"/> class.
    /// </summary>
    /// <param name="name">The name of the state.</param>
    public StateAttribute(string name)
    {
        Name = name;
    }
}
