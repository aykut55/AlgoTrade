using System.Text.Json;

internal sealed class StartupSequence
{
    public string Operation { get; set; } = "";
    public bool ReadData { get; set; }
    public string RunMode { get; set; } = "TradeOnly";
    public DateTime SavedAtUtc { get; set; } = DateTime.UtcNow;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static StartupSequence? Load(string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            return JsonSerializer.Deserialize<StartupSequence>(File.ReadAllText(path), JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        SavedAtUtc = DateTime.UtcNow;
        File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOptions));
    }
}
