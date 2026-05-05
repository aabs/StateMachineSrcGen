using System;

namespace StateMachineSrcGen;

/// <summary>
/// Represents the signature of a handler method.
/// </summary>
public readonly record struct MethodSignature : IEquatable<MethodSignature>
{
    /// <summary>Gets whether the method is public.</summary>
    public required bool IsPublic { get; init; }

    /// <summary>Gets whether the method is static.</summary>
    public required bool IsStatic { get; init; }

    /// <summary>Gets the return type of the method.</summary>
    public required string ReturnType { get; init; }

    /// <summary>Gets the parameters of the method.</summary>
    public required EquatableArray<ParameterInfo> Parameters { get; init; }
}
