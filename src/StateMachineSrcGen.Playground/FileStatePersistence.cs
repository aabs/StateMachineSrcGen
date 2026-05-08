using System.Text.Json;
using StateMachineSrcGen;

namespace StateMachineSrcGen.Playground;

/// <summary>
/// Persists the order state machine's status string to a JSON file on disk,
/// alongside the full order model (items, etc.).
///
/// The state machine only tracks a string status value, but this persistence
/// layer wraps it in a richer OrderState model so domain data survives across
/// transitions.
/// </summary>
public sealed class FileOrderPersistence : IStatePersistence<string>
{
    private readonly string _filePath;
    private readonly List<OrderItem> _items;
    private readonly string _initialStatus;

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true
    };

    public FileOrderPersistence(string filePath, string initialStatus, List<OrderItem> items)
    {
        _filePath = filePath;
        _initialStatus = initialStatus;
        _items = items;
    }

    public Task<string> LoadAsync()
    {
        if (!File.Exists(_filePath))
            return Task.FromResult(_initialStatus);

        var json = File.ReadAllText(_filePath);
        var order = JsonSerializer.Deserialize<OrderState>(json, s_jsonOptions);
        return Task.FromResult(order?.Status ?? _initialStatus);
    }

    public Task SaveAsync(string state)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        // Persist the full order model with the updated status
        var order = new OrderState(state, _items);
        var json = JsonSerializer.Serialize(order, s_jsonOptions);
        File.WriteAllText(_filePath, json);
        return Task.CompletedTask;
    }
}
