using StateMachineSrcGen;

namespace StateMachineSrcGen.Playground;

public record OrderEvent(string Action) : IDispatchableEvent<string>
{
    public string GetEventId() => Action;
}
