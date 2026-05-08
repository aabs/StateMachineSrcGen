using System;

namespace StateMachineSrcGen;

/// <summary>Marks a method as the terminal-state cleanup handler.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class OnTerminalAttribute : Attribute { }
