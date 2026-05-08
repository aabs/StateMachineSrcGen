using StateMachineSrcGen;

namespace StateMachineSrcGen.Playground;

/// <summary>
/// Configuration partial — wires custom persistence into the generated state machine.
/// The source generator produces _persistence, _lock, and HandleAsync automatically.
/// This partial class provides the UsePersistence helper to swap the default
/// in-memory persistence for a custom implementation.
/// </summary>
public static partial class OrderMachine
{
    /// <summary>
    /// Configures the state machine to use the specified persistence provider.
    /// Call this before any HandleAsync invocations.
    /// </summary>
    public static void UsePersistence(IStatePersistence<OrderState> persistence)
    {
        _persistence = persistence;
    }
}
