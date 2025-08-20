namespace EzKzr.MockServer.Infrastructure;

using System.Text.Json;

internal static class JsonStorage
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    internal static List<T> LoadList<T>(string dir, string fileName, Func<List<T>> seed)
    {
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, fileName);
        if (File.Exists(path))
        {
            try
            {
                var json = File.ReadAllText(path);
                var res = JsonSerializer.Deserialize<List<T>>(json, JsonOpts);
                if (res is not null) return res;
            }
            catch { }
        }
        var seeded = seed();
        File.WriteAllText(path, JsonSerializer.Serialize(seeded, JsonOpts));
        return seeded;
    }

    internal static void SaveList<T>(string dir, string fileName, IEnumerable<T> list)
    {
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, fileName);
        File.WriteAllText(path, JsonSerializer.Serialize(list, JsonOpts));
    }
}
