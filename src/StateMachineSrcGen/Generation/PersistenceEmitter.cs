using System.Text;

namespace StateMachineSrcGen.Generation;

/// <summary>
/// Generates default InMemoryPersistence class.
/// </summary>
internal static class PersistenceEmitter
{
    /// <summary>
    /// Emits the default InMemoryPersistence nested class.
    /// </summary>
    public static string Emit(ValidatedStateMachine input)
    {
        var sb = new StringBuilder();

        sb.AppendLine("        /// <summary>Default in-memory persistence implementation.</summary>");
        sb.AppendLine($"        private sealed class InMemoryPersistence : StateMachineSrcGen.IStatePersistence<{input.StateTypeName}>");
        sb.AppendLine("        {");
        sb.AppendLine($"            private {input.StateTypeName}? _state;");
        sb.AppendLine();
        sb.AppendLine("            /// <summary>Loads the current state from memory.</summary>");
        sb.AppendLine($"            public System.Threading.Tasks.Task<{input.StateTypeName}> LoadAsync()");
        sb.AppendLine("            {");
        sb.AppendLine($"                return System.Threading.Tasks.Task.FromResult(_state!);");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            /// <summary>Saves the specified state to memory.</summary>");
        sb.AppendLine($"            public System.Threading.Tasks.Task SaveAsync({input.StateTypeName} state)");
        sb.AppendLine("            {");
        sb.AppendLine("                _state = state;");
        sb.AppendLine("                return System.Threading.Tasks.Task.CompletedTask;");
        sb.AppendLine("            }");
        sb.AppendLine("        }");

        return sb.ToString();
    }
}
