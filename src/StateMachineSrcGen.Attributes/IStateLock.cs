using System.Threading.Tasks;

namespace StateMachineSrcGen;

/// <summary>Lock provider interface.</summary>
public interface IStateLock<TState>
{
    /// <summary>
    /// Attempts to acquire the lock for the state transition.
    /// </summary>
    /// <returns><c>true</c> if the lock was successfully acquired; otherwise, <c>false</c>.</returns>
    Task<bool> AcquireAsync();

    /// <summary>
    /// Releases the lock after the state transition completes or fails.
    /// </summary>
    Task ReleaseAsync();
}
