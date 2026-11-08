using System.Text.Json;

namespace AlgoTrade.Core.Python;

/// <summary>
/// Bir trader bundle'inin yanina, o calismaya ait rapor dosyalarini ve
/// GUI'lerin okuyabilecegi indeks JSON'unu yerlestirir.
/// </summary>
public static class BundleReportPackager
{
    private static readonly string[] ReportPatterns =
    {
        "*Statistics*.txt", "*Statistics*.csv",
        "*Performans*.txt", "*Performans*.csv",
        "*TraderLists*.txt", "*TraderLists*.csv"
    };

    private static readonly string[] ConfigNames =
    {
        "AppConfig.json", "StrategyConfig.txt", "QueryConfig.txt",
        "OptimizationConfig.txt", "EquityCurveFilterConfig.txt",
        "StatisticsExporterConfig.json"
    };

    public static string Create(string bundlePath, string logsDir, string configsDir,
                                string traderType)
    {
        string bundleDir = Path.GetDirectoryName(Path.GetFullPath(bundlePath))!;
        string bundleName = Path.GetFileNameWithoutExtension(bundlePath);
        string reportsDir = Path.Combine(bundleDir, bundleName + ".reports");
        string indexPath = Path.Combine(bundleDir, bundleName + ".reports.json");

        if (Directory.Exists(reportsDir))
            Directory.Delete(reportsDir, recursive: true);
        Directory.CreateDirectory(reportsDir);

        var entries = new List<BundleReportEntry>();
        CopyReports(logsDir, reportsDir, entries);
        CopyConfigs(configsDir, reportsDir, entries);

        var index = new BundleReportIndex
        {
            Bundle = Path.GetFileName(bundlePath),
            TraderType = traderType,
            GeneratedAt = DateTimeOffset.Now,
            Files = entries
        };
        File.WriteAllText(indexPath, JsonSerializer.Serialize(index, new JsonSerializerOptions
        {
            WriteIndented = true
        }));
        return indexPath;
    }

    private static void CopyReports(string sourceDir, string reportsDir,
                                    List<BundleReportEntry> entries)
    {
        if (!Directory.Exists(sourceDir)) return;
        foreach (string pattern in ReportPatterns)
        {
            foreach (string source in Directory.GetFiles(sourceDir, pattern, SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(sourceDir, source);
                string destination = Path.Combine(reportsDir, "reports", relative);
                CopyOne(source, destination);
                entries.Add(new BundleReportEntry
                {
                    Name = Path.GetFileNameWithoutExtension(source),
                    Type = Classify(source),
                    Format = Path.GetExtension(source).TrimStart('.').ToLowerInvariant(),
                    Path = ToPortablePath(Path.GetRelativePath(Path.GetDirectoryName(reportsDir)!, destination))
                });
            }
        }
    }

    private static void CopyConfigs(string configsDir, string reportsDir,
                                    List<BundleReportEntry> entries)
    {
        foreach (string name in ConfigNames)
        {
            string source = name == "AppConfig.json"
                ? Path.Combine(configsDir, "AppConfig", name)
                : Path.Combine(configsDir, name);
            if (!File.Exists(source)) continue;
            string destination = Path.Combine(reportsDir, "configs", name);
            CopyOne(source, destination);
            entries.Add(new BundleReportEntry
            {
                Name = Path.GetFileNameWithoutExtension(name),
                Type = "config",
                Format = Path.GetExtension(name).TrimStart('.').ToLowerInvariant(),
                Path = ToPortablePath(Path.GetRelativePath(Path.GetDirectoryName(reportsDir)!, destination))
            });
        }
    }

    private static void CopyOne(string source, string destination)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Copy(source, destination, overwrite: true);
    }

    private static string Classify(string path)
    {
        string name = Path.GetFileName(path);
        if (name.Contains("Statistics", StringComparison.OrdinalIgnoreCase)) return "statistics";
        if (name.Contains("Performans", StringComparison.OrdinalIgnoreCase)) return "performance";
        return "trade-list";
    }

    private static string ToPortablePath(string path) => path.Replace('\\', '/');
}

public sealed class BundleReportIndex
{
    public string Bundle { get; set; } = "";
    public string TraderType { get; set; } = "";
    public DateTimeOffset GeneratedAt { get; set; }
    public List<BundleReportEntry> Files { get; set; } = new();
}

public sealed class BundleReportEntry
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string Format { get; set; } = "";
    public string Path { get; set; } = "";
}
