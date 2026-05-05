using System.Threading.Tasks;

namespace StateMachineSrcGen;

/// <summary>Persistence provider interface.</summary>
public interface IStatePersistence<TState>
{
    /// <summary>
    /// Loads the current state from the persistence store.
    /// </summary>
    /// <returns>The current state object.</returns>
    Task<TState> LoadAsync();

    /// <summary>
    /// Saves the specified state to the persistence store.
    /// </summary>
    /// <param name="state">The state object to persist.</param>
    Task SaveAsync(TState state);
}
