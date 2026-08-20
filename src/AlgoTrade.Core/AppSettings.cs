namespace AlgoTrade.Core;

/// <summary>
/// Uygulama genelinde kullanılan yol ve ayar bilgileri.
/// </summary>
public static class AppSettings
{
    private static readonly string _baseDir =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    /// <summary>Proje (repo) kökü — AlgoTrade.sln'in bulunduğu dizin.</summary>
    public static string RootDir          => _baseDir;
    public static string InputsDir        => Path.Combine(_baseDir, "inputs");
    public static string ConfigsDir       => Path.Combine(InputsDir, "configs");
    public static string ScriptsDir       => Path.Combine(InputsDir, "scripts");
    public static string PythonScriptsDir => Path.Combine(InputsDir, "python");
    public static string DearPyGuiDataPlotterDir => Path.Combine(_baseDir, "src", "DearPyGuiDataPlotter");
    public static string OutputsDir       => Path.Combine(_baseDir, "outputs");
    public static string LogsDir          => Path.Combine(OutputsDir, "logs");
    public static string OptLogsDir       => Path.Combine(OutputsDir, "opt");
    public static string ScanLogsDir      => Path.Combine(OutputsDir, "scan");

    /// <summary>
    /// Tüm Python alt-projelerinin ortak kullandığı, proje kökündeki tek .venv.
    /// setupPythonEnvs.bat tarafından oluşturulur/kurulur.
    /// </summary>
    public static string VenvDir => Path.Combine(_baseDir, ".venv");

    /// <summary>
    /// VenvDir/pyvenv.cfg dosyasından ("home" + "version") venv'in dayandığı
    /// CPython kurulumunun embeddable DLL yolunu (pythonXY.dll) türetir.
    /// Böylece pythonnet'in hangi Python sürümünü embed edeceği, venv'i hangi
    /// sürümle kurduysak ona otomatik uyar (hardcoded sürüm listesi tutmaya gerek kalmaz).
    /// Venv yoksa veya cfg okunamıyorsa null döner.
    /// </summary>
    public static string? ResolvePythonDll()
    {
        string cfgPath = Path.Combine(VenvDir, "pyvenv.cfg");
        if (!File.Exists(cfgPath)) return null;

        string? home = null;
        string? version = null;
        foreach (var line in File.ReadAllLines(cfgPath))
        {
            var parts = line.Split('=', 2);
            if (parts.Length != 2) continue;

            var key   = parts[0].Trim();
            var value = parts[1].Trim();

            if (key == "home") home = value;
            else if (key == "version") version = value;
        }

        if (string.IsNullOrEmpty(home) || string.IsNullOrEmpty(version)) return null;

        var v = version.Split('.');
        if (v.Length < 2) return null;

        string dllPath = Path.Combine(home, $"python{v[0]}{v[1]}.dll");
        return File.Exists(dllPath) ? dllPath : null;
    }

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(InputsDir);
        Directory.CreateDirectory(ConfigsDir);
        Directory.CreateDirectory(ScriptsDir);
        Directory.CreateDirectory(PythonScriptsDir);
        Directory.CreateDirectory(OutputsDir);
        Directory.CreateDirectory(LogsDir);
        Directory.CreateDirectory(OptLogsDir);
        Directory.CreateDirectory(ScanLogsDir);
    }
}
