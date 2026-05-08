using System;
using Microsoft.CodeAnalysis;

namespace StateMachineSrcGen;

/// <summary>
/// Represents a parsed cleanup handler.
/// </summary>
public readonly record struct ParsedCleanupHandler : IEquatable<ParsedCleanupHandler>
{
    /// <summary>Gets the name of the cleanup handler method.</summary>
    public required string MethodName { get; init; }

    /// <summary>Gets the method signature of the cleanup handler.</summary>
    public required MethodSignature Signature { get; init; }

    /// <summary>Gets the source location of the cleanup handler declaration.</summary>
    public required Location Location { get; init; }
}
