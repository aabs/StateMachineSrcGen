using System;

namespace StateMachineSrcGen;

/// <summary>
/// Represents a parameter in a handler method signature.
/// </summary>
public readonly record struct ParameterInfo : IEquatable<ParameterInfo>
{
    /// <summary>Gets the parameter name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the fully qualified type name of the parameter.</summary>
    public required string TypeName { get; init; }
}
