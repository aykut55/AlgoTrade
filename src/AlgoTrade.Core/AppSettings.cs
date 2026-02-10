namespace AlgoTrade.Core;

/// <summary>
/// Uygulama genelinde kullanılan yol ve ayar bilgileri.
/// </summary>
public static class AppSettings
{
    private static readonly string _baseDir =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    public static string InputsDir => Path.Combine(_baseDir, "inputs");
    public static string OutputsDir => Path.Combine(_baseDir, "outputs");
    public static string LogsDir => Path.Combine(OutputsDir, "logs");

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(InputsDir);
        Directory.CreateDirectory(OutputsDir);
        Directory.CreateDirectory(LogsDir);
    }
}
