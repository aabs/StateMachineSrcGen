namespace StateMachineSrcGen.Playground;

/// <summary>
/// Configuration partial — wires custom persistence into the generated state machine.
/// Because the generated code declares _persistence as a private static field in the
/// same partial class, we can reassign it here.
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
