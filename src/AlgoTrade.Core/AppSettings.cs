namespace AlgoTrade.Core;

/// <summary>
/// Uygulama genelinde kullanılan yol ve ayar bilgileri.
/// </summary>
public static class AppSettings
{
    private static readonly string _baseDir =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    public static string InputsDir   => Path.Combine(_baseDir, "inputs");
    public static string ConfigsDir  => Path.Combine(InputsDir, "configs");
    public static string ScriptsDir  => Path.Combine(InputsDir, "scripts");
    public static string OutputsDir  => Path.Combine(_baseDir, "outputs");
    public static string LogsDir     => Path.Combine(OutputsDir, "logs");
    public static string OptLogsDir  => Path.Combine(OutputsDir, "opt");

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(InputsDir);
        Directory.CreateDirectory(ConfigsDir);
        Directory.CreateDirectory(ScriptsDir);
        Directory.CreateDirectory(OutputsDir);
        Directory.CreateDirectory(LogsDir);
        Directory.CreateDirectory(OptLogsDir);
    }
}
