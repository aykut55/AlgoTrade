using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AlgoTrade.Core.Python.DearPyGuiDataPlotter;

/// <summary>Playlist.json içindeki tek bir girdi (bkz. docs/todo.md "Offline Replay" > Option C).</summary>
public record PlaylistEntry(string BundlePath, string Label, int[] Color);

/// <summary>
/// inputs/python/offlineReplay/playlist.json'ı okuyup, N farklı SingleTrader run'ının .npz
/// bundle'ını (docs/todo.md "Yeni Özellik Fikri: Geçmiş (Offline)... Hızlı Sinyal Plot'u" —
/// Option C) tek bir "combined" bundle'a birleştirir. Yeni tip plotter (DearPyGuiDataPlotter)
/// bunu doğrudan LoadBundle ile açabiliyor. Eski tip plotter için ayrı bir yol var
/// (<see cref="PythonPlotter.PlotBundlePlaylist"/>) — o disk'e ara bir dosya yazmadan N
/// tradeData PyDict'i bellekte kurup doğrudan çizdiriyor.
/// </summary>
public static class OfflineReplayPlaylist
{
    /// <param name="playlistPath">playlist.json'ın tam yolu.</param>
    /// <param name="rootDirForRelativePaths">
    /// playlist içindeki göreli "bundle" yollarının çözüleceği kök (AppSettings.RootDir).
    /// </param>
    public static List<PlaylistEntry> Load(string playlistPath, string rootDirForRelativePaths)
    {
        if (!File.Exists(playlistPath))
            throw new FileNotFoundException($"Playlist bulunamadı: {playlistPath}", playlistPath);

        var json = JObject.Parse(File.ReadAllText(playlistPath));
        var entriesJson = json["entries"] as JArray
            ?? throw new InvalidDataException($"'{playlistPath}' içinde 'entries' dizisi yok.");

        var entries = new List<PlaylistEntry>();
        foreach (var e in entriesJson)
        {
            string bundleRaw = e.Value<string>("bundle")
                ?? throw new InvalidDataException("playlist girdisinde 'bundle' alanı yok.");
            string label = e.Value<string>("label") ?? Path.GetFileNameWithoutExtension(bundleRaw);
            int[] color = e["color"]?.ToObject<int[]>() ?? new[] { 200, 200, 200, 255 };

            string bundlePath = Path.IsPathRooted(bundleRaw)
                ? bundleRaw
                : Path.GetFullPath(Path.Combine(rootDirForRelativePaths, bundleRaw));

            entries.Add(new PlaylistEntry(bundlePath, label, color));
        }

        if (entries.Count == 0)
            throw new InvalidDataException($"'{playlistPath}' içinde hiç girdi yok.");

        return entries;
    }

    /// <summary>
    /// input.json'ı (pythonPlotter/, dearPyGuiDataPlotter/'daki ile aynı format — bkz.
    /// src/DearPyGuiDataPlotter/docs/InputConfig.md) okuyup {bundle, view} yollarını döndürür.
    /// Göreli yollar <paramref name="rootDirForRelativePaths"/>'e göre çözülür.
    /// </summary>
    public static (string bundlePath, string viewPath) LoadInputJson(
        string inputJsonPath, string rootDirForRelativePaths)
    {
        if (!File.Exists(inputJsonPath))
            throw new FileNotFoundException($"input.json bulunamadı: {inputJsonPath}", inputJsonPath);

        var json = JObject.Parse(File.ReadAllText(inputJsonPath));
        string bundleRaw = json.Value<string>("bundle")
            ?? throw new InvalidDataException($"'{inputJsonPath}' içinde 'bundle' alanı yok.");
        string viewRaw = json.Value<string>("view")
            ?? throw new InvalidDataException($"'{inputJsonPath}' içinde 'view' alanı yok.");

        string Resolve(string raw) => Path.IsPathRooted(raw)
            ? raw
            : Path.GetFullPath(Path.Combine(rootDirForRelativePaths, raw));

        return (Resolve(bundleRaw), Resolve(viewRaw));
    }

    /// <summary>
    /// input.json'ı (yoksa oluşturur, varsa üzerine yazar) — pythonPlotter/, dearPyGuiDataPlotter/'daki
    /// ile aynı formatta, ROOT_DIR-relative "bundle"/"view" yollarıyla.
    /// </summary>
    public static void WriteInputJson(string inputJsonPath, string rootRelativeBundlePath,
        string rootRelativeViewPath)
    {
        var json = new JObject
        {
            ["bundle"] = rootRelativeBundlePath.Replace('\\', '/'),
            ["view"] = rootRelativeViewPath.Replace('\\', '/'),
        };
        File.WriteAllText(inputJsonPath, json.ToString(Formatting.Indented));
    }

    /// <summary>
    /// N bundle'ı tek bir "combined" .npz/.view.json'a birleştirir — yeni tip plotter'ın
    /// LoadBundle ile doğrudan açabildiği format. OHLC referans olarak İLK entry'den alınır
    /// (hepsinin aynı sembol/timeframe üzerinde çalıştığı varsayılıyor — farklı sembol/timeframe
    /// overlay etmek şu an desteklenmiyor, bkz. docs/todo.md açık soru). Her entry'nin
    /// Signal/PnL serisi "{label} Signal"/"{label} PnL" adıyla eklenir.
    /// </summary>
    public static (string bundlePath, string viewPath) MergeToBundle(
        List<PlaylistEntry> entries, string outputDir, string fileBaseName = "combined")
    {
        if (entries == null || entries.Count == 0)
            throw new ArgumentException("entries boş olamaz.", nameof(entries));

        var referenceReader = new NpzReader(entries[0].BundlePath);
        var opens = referenceReader.ReadDoubleArray("open");
        var highs = referenceReader.ReadDoubleArray("high");
        var lows = referenceReader.ReadDoubleArray("low");
        var closes = referenceReader.ReadDoubleArray("close");
        var volumes = referenceReader.ReadDoubleArray("volume");
        var sizes = referenceReader.ReadLongArray("size");
        var timestamps = referenceReader.ReadStringArray("timestamps");
        int n = timestamps.Length;

        var writer = new NpzWriter();
        writer.AddFloatArray("open", opens);
        writer.AddFloatArray("high", highs);
        writer.AddFloatArray("low", lows);
        writer.AddFloatArray("close", closes);
        writer.AddFloatArray("volume", volumes);
        writer.AddIntArray("size", sizes);
        writer.AddStringArray("timestamps", timestamps);

        var seriesNames = new List<string>();
        var seriesRows = new List<double[]>();
        var panelSeries = new List<(string Label, string SignalName, string? PnLName, int[] Color)>();

        foreach (var entry in entries)
        {
            var reader = entry == entries[0] ? referenceReader : new NpzReader(entry.BundlePath);

            var signalSteps = reader.ReadLongArray("signal_steps");
            var signalRow = PadOrTrim(signalSteps.Select(v => (double)v).ToArray(), n);
            string signalName = $"{entry.Label} Signal";
            seriesNames.Add(signalName);
            seriesRows.Add(signalRow);

            string? pnlName = null;
            if (reader.Contains("indicator_names") && reader.Contains("indicator_values"))
            {
                var names = reader.ReadStringArray("indicator_names");
                int idx = Array.IndexOf(names, "PnL");
                if (idx >= 0)
                {
                    var matrix = reader.ReadDouble2DArray("indicator_values");
                    var row = new double[matrix.GetLength(1)];
                    for (int c = 0; c < row.Length; c++) row[c] = matrix[idx, c];

                    pnlName = $"{entry.Label} PnL";
                    seriesNames.Add(pnlName);
                    seriesRows.Add(PadOrTrim(row, n));
                }
            }

            panelSeries.Add((entry.Label, signalName, pnlName, entry.Color));
        }

        writer.AddStringArray("indicator_names", seriesNames);
        var matrixOut = new double[seriesNames.Count, n];
        for (int r = 0; r < seriesNames.Count; r++)
            for (int c = 0; c < n; c++)
                matrixOut[r, c] = seriesRows[r][c];
        writer.AddFloat2DArray("indicator_values", matrixOut);

        var meta = new Dictionary<string, object?>
        {
            ["symbol"] = $"Offline Replay Playlist ({entries.Count} run)",
            ["intraday"] = true,
        };
        writer.AddScalarString("meta_json", JsonConvert.SerializeObject(meta));

        Directory.CreateDirectory(outputDir);
        string bundlePath = Path.Combine(outputDir, fileBaseName + ".npz");
        writer.Save(bundlePath);

        string viewPath = BuildAndWriteView(outputDir, fileBaseName, panelSeries);
        return (bundlePath, viewPath);
    }

    private static double[] PadOrTrim(double[] values, int n)
    {
        if (values.Length == n) return values;

        var result = new double[n];
        int copyLen = Math.Min(values.Length, n);
        Array.Copy(values, result, copyLen);
        for (int i = copyLen; i < n; i++)
            result[i] = double.NaN;
        return result;
    }

    private static string BuildAndWriteView(string outputDir, string fileBaseName,
        List<(string Label, string SignalName, string? PnLName, int[] Color)> series)
    {
        static Dictionary<string, object?> Series(string source, string? name = null, string? label = null,
            int? dataId = null, int[]? color = null)
        {
            var s = new Dictionary<string, object?> { ["source"] = source };
            if (name != null) s["name"] = name;
            if (label != null) s["label"] = label;
            if (dataId.HasValue) s["dataId"] = dataId.Value;
            if (color != null) s["color"] = color;
            return s;
        }

        static Dictionary<string, object?> Panel(string id, string name, string caption, int height, int ySyncId,
            List<Dictionary<string, object?>> ser, string? yLabel = null, double[]? yFixedRange = null)
        {
            var p = new Dictionary<string, object?>
            {
                ["id"] = id,
                ["name"] = name,
                ["caption"] = caption,
                ["height"] = height,
                ["ySyncId"] = ySyncId,
                ["series"] = ser,
            };
            if (yLabel != null) p["yLabel"] = yLabel;
            if (yFixedRange != null) p["yFixedRange"] = yFixedRange;
            return p;
        }

        var signalSeries = series.Select(s => Series("indicator", s.SignalName, s.Label, color: s.Color)).ToList();
        var pnlSeries = series.Where(s => s.PnLName != null)
            .Select(s => Series("indicator", s.PnLName, s.Label, color: s.Color)).ToList();

        var panels = new List<Dictionary<string, object?>>
        {
            // dataId=0 ZORUNLU: TradeSignalRenderer.draw() OHLC panelinin candle serisini
            // hep dataId=0 varsayarak arıyor (bkz. TradeDataBundleConverter.BuildAndWriteView).
            Panel("ohlc", "OHLC", "OHLC", 380, 0, new() { Series("ohlc", "OHLC", dataId: 0) }),
            Panel("signals", "Signals", "Signals (Playlist)", 220, 1, signalSeries,
                yLabel: "Signals", yFixedRange: new double[] { -2, 2 }),
            Panel("pnl", "PnL", "PnL (Playlist)", 260, 2, pnlSeries, yLabel: "PnL"),
        };

        var view = new Dictionary<string, object?> { ["panels"] = panels };
        string viewPath = Path.Combine(outputDir, fileBaseName + ".view.json");
        File.WriteAllText(viewPath, JsonConvert.SerializeObject(view, Formatting.Indented));
        return viewPath;
    }
}
