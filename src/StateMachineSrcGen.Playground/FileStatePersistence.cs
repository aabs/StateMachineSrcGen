using System.Text.Json;
using StateMachineSrcGen;

namespace StateMachineSrcGen.Playground;

/// <summary>
/// Persists the full OrderState object (state ID + items) as JSON to a file on disk.
/// Demonstrates how to implement IStatePersistence&lt;TState&gt; for durable storage
/// when TState is a rich object implementing IStateMachineState.
/// </summary>
public sealed class FileOrderPersistence : IStatePersistence<OrderState>
{
    private readonly string _filePath;
    private readonly OrderState _initialState;

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true
    };

    public FileOrderPersistence(string filePath, OrderState initialState)
    {
        _filePath = filePath;
        _initialState = initialState;
    }

    public Task<OrderState> LoadAsync()
    {
        if (!File.Exists(_filePath))
            return Task.FromResult(_initialState);

        var json = File.ReadAllText(_filePath);
        var state = JsonSerializer.Deserialize<OrderState>(json, s_jsonOptions);
        return Task.FromResult(state ?? _initialState);
    }

    public Task SaveAsync(OrderState state)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(state, s_jsonOptions);
        File.WriteAllText(_filePath, json);
        return Task.CompletedTask;
    }
}
