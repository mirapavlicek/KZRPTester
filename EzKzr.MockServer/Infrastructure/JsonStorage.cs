using System.Text.Json;
using System.Text.Json.Serialization;

namespace EzKzr.MockServer.Infrastructure;

public sealed class JsonStorage
{
    private readonly string _root;
    private readonly JsonSerializerOptions _opt;
    private readonly object _lock = new();

    public JsonStorage(string contentRoot)
    {
        _root = Path.Combine(contentRoot, "data");
        Directory.CreateDirectory(_root);
        _opt = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter() }
        };
    }

    public List<T> LoadList<T>(string name, Func<List<T>> seed)
    {
        var path = Path.Combine(_root, $"{name}.json");
        if (!File.Exists(path))
        {
            var d = seed();
            SaveList(name, d);
            return d;
        }
        using var fs = File.OpenRead(path);
        return JsonSerializer.Deserialize<List<T>>(fs, _opt) ?? new();
    }

    public void SaveList<T>(string name, IEnumerable<T> data)
    {
        var path = Path.Combine(_root, $"{name}.json");
        lock (_lock)
        {
            using var fs = File.Create(path);
            JsonSerializer.Serialize(fs, data, _opt);
        }
    }
}
