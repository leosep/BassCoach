using System.Text.Json;

namespace BassCoach.Core.Storage;

/// <summary>Reads and writes one JSON document atomically. A missing or corrupt file yields a fresh instance.</summary>
public sealed class JsonFileStore<T> where T : class, new()
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    private readonly string _path;

    public JsonFileStore(string path) => _path = path;

    public string Path => _path;

    public T Load()
    {
        try
        {
            if (!File.Exists(_path)) return new T();
            return JsonSerializer.Deserialize<T>(File.ReadAllText(_path), Options) ?? new T();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return new T();
        }
    }

    public void Save(T value)
    {
        string? directory = System.IO.Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        string temp = _path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(value, Options));
        File.Move(temp, _path, overwrite: true);
    }
}
