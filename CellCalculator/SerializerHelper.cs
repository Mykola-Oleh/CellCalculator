using System.Text.Json;

public static class SerializerHelper
{
    private static JsonSerializerOptions options = new JsonSerializerOptions
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static async Task SaveAsync<T>(T obj, string path)
    {
        var json = JsonSerializer.Serialize(obj, options);
        await File.WriteAllTextAsync(path, json);
    }

    public static async Task<T?> LoadAsync<T>(string path) where T : class
    {
        if (!File.Exists(path)) return null;
        var json = await File.ReadAllTextAsync(path);
        return JsonSerializer.Deserialize<T>(json, options);
    }
}
