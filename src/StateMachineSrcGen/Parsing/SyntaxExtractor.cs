using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StateMachineSrcGen.Parsing;

/// <summary>
/// Filters syntax nodes to find class declarations decorated with state machine attributes.
/// </summary>
internal static class SyntaxExtractor
{
    /// <summary>
    /// Known attribute names that indicate a class is a state machine definition.
    /// </summary>
    private static readonly HashSet<string> s_classAttributeNames = new()
    {
        "InitialState",
        "InitialStateAttribute",
        "TerminalState",
        "TerminalStateAttribute"
    };

    /// <summary>
    /// Known attribute names that indicate a method is a handler.
    /// </summary>
    private static readonly HashSet<string> s_methodAttributeNames = new()
    {
        "Transition",
        "TransitionAttribute",
        "Guard",
        "GuardAttribute",
        "SideEffect",
        "SideEffectAttribute",
        "OnEnter",
        "OnEnterAttribute",
        "OnTerminal",
        "OnTerminalAttribute"
    };

    /// <summary>
    /// Determines whether a class declaration has any state machine attributes
    /// (either class-level [InitialState]/[TerminalState] or method-level [Transition]/[Guard]/[SideEffect]/[OnEnter]/[OnTerminal]).
    /// </summary>
    public static bool HasStateMachineAttributes(ClassDeclarationSyntax classDeclaration)
    {
        // Check class-level attributes
        foreach (var attributeList in classDeclaration.AttributeLists)
        {
            foreach (var attribute in attributeList.Attributes)
            {
                var name = GetAttributeName(attribute);
                if (s_classAttributeNames.Contains(name))
                    return true;
            }
        }

        // Check method-level attributes
        foreach (var member in classDeclaration.Members)
        {
            if (member is MethodDeclarationSyntax method)
            {
                foreach (var attributeList in method.AttributeLists)
                {
                    foreach (var attribute in attributeList.Attributes)
                    {
                        var name = GetAttributeName(attribute);
                        if (s_methodAttributeNames.Contains(name))
                            return true;
                    }
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Gets the simple name of an attribute (without the "Attribute" suffix resolution).
    /// </summary>
    private static string GetAttributeName(AttributeSyntax attribute)
    {
        return attribute.Name switch
        {
            SimpleNameSyntax simple => simple.Identifier.Text,
            QualifiedNameSyntax qualified => qualified.Right.Identifier.Text,
            _ => string.Empty
        };
    }
}
