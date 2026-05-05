using System.Text;

namespace StateMachineSrcGen.Generation;

/// <summary>
/// Generates default NoOpLock class.
/// </summary>
internal static class LockEmitter
{
    /// <summary>
    /// Emits the default NoOpLock nested class.
    /// </summary>
    public static string Emit(ValidatedStateMachine input)
    {
        var sb = new StringBuilder();

        sb.AppendLine("        /// <summary>Default no-op lock implementation that always succeeds.</summary>");
        sb.AppendLine($"        private sealed class NoOpLock : StateMachineSrcGen.IStateLock<{input.StateTypeName}>");
        sb.AppendLine("        {");
        sb.AppendLine("            /// <summary>Always acquires the lock successfully.</summary>");
        sb.AppendLine("            public System.Threading.Tasks.Task<bool> AcquireAsync()");
        sb.AppendLine("            {");
        sb.AppendLine("                return System.Threading.Tasks.Task.FromResult(true);");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            /// <summary>Releases the lock (no-op).</summary>");
        sb.AppendLine("            public System.Threading.Tasks.Task ReleaseAsync()");
        sb.AppendLine("            {");
        sb.AppendLine("                return System.Threading.Tasks.Task.CompletedTask;");
        sb.AppendLine("            }");
        sb.AppendLine("        }");

        return sb.ToString();
    }
}
