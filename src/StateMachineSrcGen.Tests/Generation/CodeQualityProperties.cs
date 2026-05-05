// Feature: state-machine-source-generator, Property 18: No reflection in generated code
// Feature: state-machine-source-generator, Property 19: XML documentation on public members
// Feature: state-machine-source-generator, Property 20: Partial class emission
// **Validates: Requirements 7.2, 7.3, 7.5**

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.CodeAnalysis;
using StateMachineSrcGen;
using StateMachineSrcGen.Generation;

namespace StateMachineSrcGen.Tests.Generation;

/// <summary>
/// Property 18: No reflection in generated code
/// For any valid ValidatedStateMachine, the generated source text shall not contain
/// references to System.Reflection, the dynamic keyword, or Type.GetMethod/Activator.CreateInstance patterns.
///
/// Property 19: XML documentation on public members
/// For any valid ValidatedStateMachine, all public members in the generated source
/// shall be preceded by XML documentation comments (///).
///
/// Property 20: Partial class emission
/// For any valid ValidatedStateMachine, the generated class declaration shall include
/// the partial keyword.
/// </summary>
public class CodeQualityProperties
{
    [Property]
    public bool GeneratedCode_ContainsNoReflection_ForAnyValidInput(
        NonEmptyString classRaw, NonEmptyString nsRaw)
    {
        var className = ToIdentifier(classRaw);
        var ns = ToIdentifier(nsRaw);

        var input = GenerationTestHelper.CreateValidStateMachine(className: className, ns: ns);
        var (source, _) = GenerationPipeline.Generate(input);

        if (source == null)
            return false;

        // Check for reflection-related patterns
        var hasReflection = source.Contains("System.Reflection") ||
                           source.Contains("dynamic ") ||
                           source.Contains("Type.GetMethod") ||
                           source.Contains("Activator.CreateInstance") ||
                           source.Contains("MethodInfo") ||
                           source.Contains("PropertyInfo");

        return !hasReflection;
    }

    [Property]
    public bool GeneratedCode_ContainsNoReflection_ForComplexMachine(
        NonEmptyString classRaw, NonEmptyString nsRaw)
    {
        var className = ToIdentifier(classRaw);
        var ns = ToIdentifier(nsRaw);

        var input = GenerationTestHelper.CreateComplexStateMachine(className: className, ns: ns);
        var (source, _) = GenerationPipeline.Generate(input);

        if (source == null)
            return false;

        var hasReflection = source.Contains("System.Reflection") ||
                           source.Contains("dynamic ") ||
                           source.Contains("Type.GetMethod") ||
                           source.Contains("Activator.CreateInstance");

        return !hasReflection;
    }

    [Property]
    public bool GeneratedCode_HasXmlDocOnPublicMembers_ForAnyValidInput(
        NonEmptyString classRaw, NonEmptyString nsRaw)
    {
        var className = ToIdentifier(classRaw);
        var ns = ToIdentifier(nsRaw);

        var input = GenerationTestHelper.CreateValidStateMachine(className: className, ns: ns);
        var (source, _) = GenerationPipeline.Generate(input);

        if (source == null)
            return false;

        // Check that every public member declaration is preceded by /// comment
        var lines = source.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            var trimmed = lines[i].TrimStart();
            if (IsPublicMemberDeclaration(trimmed))
            {
                // Look backwards for XML doc comment
                var hasDoc = false;
                for (int j = i - 1; j >= 0; j--)
                {
                    var prevTrimmed = lines[j].TrimStart();
                    if (prevTrimmed.StartsWith("///"))
                    {
                        hasDoc = true;
                        break;
                    }
                    if (string.IsNullOrWhiteSpace(prevTrimmed) || prevTrimmed.StartsWith("//"))
                        continue;
                    break;
                }

                if (!hasDoc)
                    return false;
            }
        }

        return true;
    }

    [Property]
    public bool GeneratedCode_UsesPartialClass_ForAnyValidInput(
        NonEmptyString classRaw, NonEmptyString nsRaw)
    {
        var className = ToIdentifier(classRaw);
        var ns = ToIdentifier(nsRaw);

        var input = GenerationTestHelper.CreateValidStateMachine(className: className, ns: ns);
        var (source, _) = GenerationPipeline.Generate(input);

        if (source == null)
            return false;

        // Check that the class declaration includes 'partial'
        return source.Contains("partial class");
    }

    [Property]
    public bool GeneratedCode_UsesPartialClass_ForComplexMachine(
        NonEmptyString classRaw, NonEmptyString nsRaw)
    {
        var className = ToIdentifier(classRaw);
        var ns = ToIdentifier(nsRaw);

        var input = GenerationTestHelper.CreateComplexStateMachine(className: className, ns: ns);
        var (source, _) = GenerationPipeline.Generate(input);

        if (source == null)
            return false;

        return source.Contains("partial class");
    }

    // ─── Helpers ────────────────────────────────────────────────────────────────

    private static bool IsPublicMemberDeclaration(string line)
    {
        // Match public method, property, class, or field declarations
        return line.StartsWith("public static partial class ") ||
               line.StartsWith("public static async ") ||
               line.StartsWith("public static ");
    }

    private static string ToIdentifier(NonEmptyString raw)
    {
        var filtered = new string(raw.Get.Where(char.IsLetterOrDigit).ToArray());
        if (string.IsNullOrEmpty(filtered) || !char.IsLetter(filtered[0]))
            return "X" + filtered;
        return filtered;
    }
}
