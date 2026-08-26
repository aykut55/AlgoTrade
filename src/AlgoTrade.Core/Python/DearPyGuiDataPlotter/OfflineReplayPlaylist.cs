using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AlgoTrade.Core.Python.DearPyGuiDataPlotter;

/// <summary>Playlist.json içindeki tek bir girdi (bkz. docs/todo.md "Offline Replay" > Option C).</summary>
public record PlaylistEntry(string BundlePath, string Label, int[] Color);

/// <summary>
/// combined.npz'den okunan, TEK bir run'a ait ham veri — EditOfflineReplay.csx'in "trader[]"
/// dizisindeki her eleman bu tip. Signal/PnL dizileri doğrudan erişilebilir (kullanıcı kendi
/// script'inde üzerinde hesaplama/dönüştürme yapabilir — <see cref="ViewPanelBuilder.AddSeries"/>
/// SONUCU dizinin KENDİSİNİ alır, isimle referans değil — bu yüzden dönüştürülmüş veri de
/// sorunsuz çalışır), Color/Label ise view.json üretirken kullanılıyor.
/// </summary>
public record ReplaySource(string Label, int[] Color, double[] Signal, double[]? PnL);

/// <summary>
/// EditOfflineReplay.csx'in kullanıcı-düzenlenebilir bölümünde kullanılan, tek bir panelin
/// içeriğini elle kuran yardımcı sınıf — <see cref="OfflineReplayPlaylist.WriteCustomBundle"/>'a
/// verilir. OHLC paneli otomatik eklendiği için burada tekrar eklemeye gerek yok.
///
/// ÖNEMLİ: Add* metodları serinin verisini (double[]) DOĞRUDAN TAŞIR — sadece bir isim referansı
/// DEĞİL. Yani trader[i].Signal'i olduğu gibi ya da üzerinde hesaplama yaptıktan SONRA
/// (örn. trader[i].Signal.Select(v => v * 2).ToArray()) ekleyebilirsiniz; WriteCustomBundle bu
/// diziyi YENİ bir .npz'ye yazar, plotter'ın göremediği "hayalet" seri sorunu oluşmaz.
/// </summary>
public class ViewPanelBuilder
{
    public string Id { get; }
    public string Name { get; }
    public string Caption { get; }
    public int Height { get; set; } = 220;
    public string? YLabel { get; set; }
    public double[]? YFixedRange { get; set; }

    private readonly List<(string SeriesName, string Label, int[] Color, double[] Data)> _series = new();
    internal IReadOnlyList<(string SeriesName, string Label, int[] Color, double[] Data)> SeriesList => _series;

    public ViewPanelBuilder(string id, string? name = null, string? caption = null, int height = 220)
    {
        Id = id;
        Name = name ?? id;
        Caption = caption ?? Name;
        Height = height;
    }

    /// <summary>trader[i]'nin Signal dizisini (olduğu gibi) bu panele ekler.</summary>
    public ViewPanelBuilder AddSignal(ReplaySource source)
        => AddSeries($"{source.Label} Signal", source.Label, source.Color, source.Signal);

    /// <summary>trader[i]'nin PnL dizisini (olduğu gibi) bu panele ekler (PnL yoksa sessizce atlar).</summary>
    public ViewPanelBuilder AddPnL(ReplaySource source)
    {
        if (source.PnL != null)
            AddSeries($"{source.Label} PnL", source.Label, source.Color, source.PnL);
        return this;
    }

    /// <summary>
    /// Herhangi bir diziyi (trader[]'dan hesaplanmış/dönüştürülmüş olabilir) elle, kendi
    /// isim/etiket/rengiyle ekler — ileri düzey/özel seri kullanımı için.
    /// </summary>
    public ViewPanelBuilder AddSeries(string seriesName, string label, int[] color, double[] data)
    {
        _series.Add((seriesName, label, color, data));
        return this;
    }
}

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
    /// combined.npz'i (MergeToBundle'ın ürettiği) okuyup, playlist.json'daki her entry için bir
    /// <see cref="ReplaySource"/> döndürür — EditOfflineReplay.csx'in "trader[]" dizisi bu.
    /// combined.npz'e HİÇ yazmaz, sadece okur.
    /// </summary>
    /// <param name="combinedBundlePath">combined.npz'in tam yolu.</param>
    /// <param name="playlistEntries">Etiket/renk sırası için playlist.json'dan Load(...) ile okunan liste.</param>
    public static List<ReplaySource> ReadSources(string combinedBundlePath, List<PlaylistEntry> playlistEntries)
    {
        var reader = new NpzReader(combinedBundlePath);
        var names = reader.ReadStringArray("indicator_names");
        var matrix = reader.ReadDouble2DArray("indicator_values");
        int n = matrix.GetLength(1);

        double[]? RowFor(string name)
        {
            int idx = Array.IndexOf(names, name);
            if (idx < 0) return null;
            var row = new double[n];
            for (int c = 0; c < n; c++) row[c] = matrix[idx, c];
            return row;
        }

        var sources = new List<ReplaySource>();
        foreach (var entry in playlistEntries)
        {
            var signal = RowFor($"{entry.Label} Signal")
                ?? throw new InvalidDataException(
                    $"combined.npz içinde '{entry.Label} Signal' serisi yok — playlist.json ile combined.npz uyuşmuyor olabilir, MergeOfflineReplayPlaylist.csx'i tekrar çalıştırın.");
            var pnl = RowFor($"{entry.Label} PnL");
            sources.Add(new ReplaySource(entry.Label, entry.Color, signal, pnl));
        }
        return sources;
    }

    /// <summary>
    /// EditOfflineReplay.csx'in kullanıcı-düzenlenebilir bölümünde kurulan panelleri
    /// (<see cref="ViewPanelBuilder"/> listesi, gerçek veri dizileriyle birlikte) YENİ bir
    /// .npz + .view.json çiftine yazar — combined.npz/combined.view.json'a (MergeOfflineReplayPlaylist.csx'in
    /// ürettiği "tam/varsayılan" görünüm) ASLA dokunmaz. OHLC/timestamps
    /// <paramref name="sourceCombinedBundlePath"/>'ten (değişmeden) kopyalanır; panellerdeki her
    /// seri KENDİ verisiyle (olduğu gibi ya da kullanıcı tarafından hesaplanmış/dönüştürülmüş
    /// olabilir — <see cref="ViewPanelBuilder"/>'a bkz.) yeni npz'ye yazılır, böylece plotter'ın
    /// "isim var ama veri yok" sorunu yaşaması mümkün değildir. OHLC paneli (dataId=0) otomatik
    /// en başa eklenir, kullanıcının ayrıca eklemesi gerekmez.
    /// </summary>
    /// <param name="sourceCombinedBundlePath">OHLC/timestamps'in kopyalanacağı combined.npz'in tam yolu.</param>
    /// <param name="panels">Kullanıcının ViewPanelBuilder ile kurduğu panel listesi (OHLC hariç).</param>
    /// <param name="outputDir">.npz/.view.json'ın yazılacağı klasör (AppSettings.OfflineReplayDir).</param>
    /// <param name="fileBaseName">Uzantısız dosya adı — combined ile ÇAKIŞMAMALI.</param>
    /// <param name="ohlcSignal">
    /// OHLC panelindeki AL/SAT işaretlerini (TradeSignalRenderer) neyin belirleyeceği:
    /// (1) verilmezse (null, varsayılan) VE <paramref name="includeOhlcSignal"/> true ise:
    /// kaynak combined.npz'deki signal_codes/signal_steps OLDUĞU GİBİ kopyalanır. (2) bir dizi
    /// verilirse (örn. trader[3].Signal, ya da hesaplanmış/bileşke bir sinyal): AL/SAT işaretleri
    /// BUNDAN üretilir. NOT: signal_codes/steps alanları npz'de HER DURUMDA yazılır (2026-08-26'da
    /// bu alanların eksikliği X ekseni senkron sorununa yol açmıştı) — "gösterme" isteği
    /// <paramref name="includeOhlcSignal"/>=false ile, alanı SİLEREK değil, hep-FLAT (anlamsız)
    /// bir sinyal yazarak karşılanır.
    /// </param>
    /// <param name="includeOhlcSignal">
    /// false verilirse OHLC panelinde AL/SAT ANLAMLI olarak gösterilmez — <paramref name="ohlcSignal"/>
    /// yok sayılır, bunun yerine hep-FLAT bir sinyal yazılır (alan npz'de var olmaya devam eder,
    /// sync fix'i bozulmaz). Varsayılan true.
    /// </param>
    /// <returns>Yazılan .npz ve .view.json'ın tam yolları.</returns>
    public static (string bundlePath, string viewPath) WriteCustomBundle(string sourceCombinedBundlePath,
        List<ViewPanelBuilder> panels, string outputDir, string fileBaseName,
        double[]? ohlcSignal = null, bool includeOhlcSignal = true)
    {
        if (panels == null || panels.Count == 0)
            throw new ArgumentException("panels boş olamaz — en az bir panel kurup ekleyin.", nameof(panels));
        if (fileBaseName.Equals("combined", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException(
                "fileBaseName 'combined' olamaz — MergeOfflineReplayPlaylist.csx'in ürettiği " +
                "combined.npz/combined.view.json'ın üzerine yazılmasını önlemek için farklı bir isim seçin.",
                nameof(fileBaseName));

        var sourceReader = new NpzReader(sourceCombinedBundlePath);
        var opens = sourceReader.ReadDoubleArray("open");
        var highs = sourceReader.ReadDoubleArray("high");
        var lows = sourceReader.ReadDoubleArray("low");
        var closes = sourceReader.ReadDoubleArray("close");
        var volumes = sourceReader.ReadDoubleArray("volume");
        var sizes = sourceReader.ReadLongArray("size");
        var timestamps = sourceReader.ReadStringArray("timestamps");
        int n = timestamps.Length;

        var writer = new NpzWriter();
        writer.AddFloatArray("open", opens);
        writer.AddFloatArray("high", highs);
        writer.AddFloatArray("low", lows);
        writer.AddFloatArray("close", closes);
        writer.AddFloatArray("volume", volumes);
        writer.AddIntArray("size", sizes);
        writer.AddStringArray("timestamps", timestamps);

        if (!includeOhlcSignal)
        {
            // "AL/SAT gösterme" isteniyor - ama alanları npz'den TAMAMEN çıkarmak senkron fix'ini
            // (2026-08-26, bkz. MergeToBundle) bozma riski tasiyor. Bunun yerine hep-FLAT (0)
            // bir sinyal yaziliyor - alan HER ZAMAN var, sadece anlamli bir AL/SAT üretmiyor
            // (BuildSignalCodes ilk barda bir kere FLAT kodu üretir, onun disinda sessiz kalir).
            var flat = new long[n];
            writer.AddIntArray("signal_codes", BuildSignalCodes(flat, n));
            writer.AddIntArray("signal_steps", flat);
        }
        else if (ohlcSignal != null)
        {
            // Kullanıcının EditOfflineReplay.csx'te verdiği özel/hesaplanmış sinyal.
            var steps = PadOrTrim(ohlcSignal, n).Select(v => (long)v).ToArray();
            writer.AddIntArray("signal_codes", BuildSignalCodes(steps, n));
            writer.AddIntArray("signal_steps", steps);
        }
        else
        {
            // Varsayılan: kaynak combined.npz'den olduğu gibi kopyala (senkron fix'i BOZMAZ).
            if (sourceReader.Contains("signal_codes"))
                writer.AddIntArray("signal_codes", sourceReader.ReadLongArray("signal_codes"));
            if (sourceReader.Contains("signal_steps"))
                writer.AddIntArray("signal_steps", sourceReader.ReadLongArray("signal_steps"));
        }

        var seriesNames = new List<string>();
        var seriesRows = new List<double[]>();
        foreach (var p in panels)
            foreach (var s in p.SeriesList)
            {
                seriesNames.Add(s.SeriesName);
                seriesRows.Add(PadOrTrim(s.Data, n));
            }

        if (seriesNames.Count > 0)
        {
            writer.AddStringArray("indicator_names", seriesNames);
            var matrixOut = new double[seriesNames.Count, n];
            for (int r = 0; r < seriesNames.Count; r++)
                for (int c = 0; c < n; c++)
                    matrixOut[r, c] = seriesRows[r][c];
            writer.AddFloat2DArray("indicator_values", matrixOut);
        }

        var meta = new Dictionary<string, object?>
        {
            ["symbol"] = $"Offline Replay - Custom ({fileBaseName})",
            ["intraday"] = true,
        };
        writer.AddScalarString("meta_json", JsonConvert.SerializeObject(meta));

        Directory.CreateDirectory(outputDir);
        string bundlePath = Path.Combine(outputDir, fileBaseName + ".npz");
        writer.Save(bundlePath);

        var panelDicts = new List<Dictionary<string, object?>>
        {
            // dataId=0 ZORUNLU: TradeSignalRenderer.draw() OHLC panelinin candle serisini
            // hep dataId=0 varsayarak arıyor.
            Panel("ohlc", "OHLC", "OHLC", 380, 0, new() { Series("ohlc", "OHLC", dataId: 0) }),
        };

        int ySyncId = 1;
        foreach (var p in panels)
        {
            var seriesDicts = p.SeriesList.Select(s => Series("indicator", s.SeriesName, s.Label, color: s.Color)).ToList();
            panelDicts.Add(Panel(p.Id, p.Name, p.Caption, p.Height, ySyncId++, seriesDicts, p.YLabel, p.YFixedRange));
        }

        var view = new Dictionary<string, object?> { ["panels"] = panelDicts };
        string viewPath = Path.Combine(outputDir, fileBaseName + ".view.json");
        File.WriteAllText(viewPath, JsonConvert.SerializeObject(view, Formatting.Indented));

        return (bundlePath, viewPath);
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
    /// <param name="useMajorityConsensusSignal">
    /// true (varsayılan): OHLC panelindeki AL/SAT işaretleri tüm entry'lerin bar-bar ÇOĞUNLUK
    /// OYUNDAN (kaç tanesi AL/SAT/FLAT demiş, en çok oyu alan kazanır — eşitlikte
    /// AL &gt; SAT &gt; FLAT önceliği var) üretilen bir "bileşke" sinyalden gelir —
    /// MultipleTrader.BuildConsensusSignal'daki "Majority" moduyla aynı fikir.
    /// false: OHLC panelinde İLK entry'nin sinyali gösterilir.
    /// </param>
    public static (string bundlePath, string viewPath) MergeToBundle(
        List<PlaylistEntry> entries, string outputDir, string fileBaseName = "combined",
        bool useMajorityConsensusSignal = true)
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
        long[]? referenceSignalSteps = null;
        var allSignalStepsPadded = new List<long[]>(); // bileske/consensus icin, asagidaki YORUMLU ornege bkz.

        foreach (var entry in entries)
        {
            var reader = entry == entries[0] ? referenceReader : new NpzReader(entry.BundlePath);

            var signalSteps = reader.ReadLongArray("signal_steps");
            if (entry == entries[0])
                referenceSignalSteps = signalSteps;
            var signalRow = PadOrTrim(signalSteps.Select(v => (double)v).ToArray(), n);
            allSignalStepsPadded.Add(signalRow.Select(v => (long)v).ToArray());
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

        // "Gerçek" (TradeDataBundleConverter'ın ürettiği) bundle'larda hep var olan
        // signal_codes/signal_steps alanları — combined.npz'de EKSİKTİ (2026-08-26'da bulundu,
        // TradeSignalRenderer/OHLC paneli bunları kullanıyor olabilir). Varsayılan (useMajorityConsensusSignal
        // =false): referans (ilk) entry'nin sinyali kullanılır (OHLC de zaten referans entry'den
        // geliyor, tutarlı). true ise tüm entry'lerin bar-bar ÇOĞUNLUK OYUNDAN bir "bileşke"
        // sinyal üretilir (MultipleTrader.BuildConsensusSignal'daki "Majority" moduyla aynı fikir).
        if (useMajorityConsensusSignal)
        {
            var majoritySignal = new long[n];
            for (int bar = 0; bar < n; bar++)
            {
                int alCount = 0, satCount = 0, flatCount = 0;
                foreach (var steps in allSignalStepsPadded)
                {
                    if (steps[bar] == 1) alCount++;
                    else if (steps[bar] == -1) satCount++;
                    else flatCount++;
                }
                majoritySignal[bar] = (alCount >= satCount && alCount >= flatCount) ? 1
                    : (satCount >= alCount && satCount >= flatCount) ? -1
                    : 0;
            }
            referenceSignalSteps = majoritySignal;
        }

        if (referenceSignalSteps != null)
        {
            var refSteps = PadOrTrim(referenceSignalSteps.Select(v => (double)v).ToArray(), n)
                .Select(v => (long)v).ToArray();
            writer.AddIntArray("signal_codes", BuildSignalCodes(refSteps, n));
            writer.AddIntArray("signal_steps", refSteps);
        }

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

    /// <summary>
    /// TradeDataBundleConverter.BuildSignalCodes ile AYNI mantık: sinyal SEYREK (sadece
    /// değişim barlarında) event koduna çevrilir — TradeSignalRenderer'ın beklediği format
    /// (1=AL, -1=SAT, 2=FLAT, 0=değişim yok).
    /// </summary>
    private static long[] BuildSignalCodes(long[] signalSteps, int n)
    {
        var codes = new long[n];
        long? previous = null;

        for (int i = 0; i < n && i < signalSteps.Length; i++)
        {
            long current = signalSteps[i];
            if (previous == null || current != previous.Value)
            {
                codes[i] = current switch
                {
                    1 => 1,   // AL
                    -1 => -1, // SAT
                    0 => 2,   // FLAT
                    _ => 0,
                };
            }
            previous = current;
        }

        return codes;
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

    private static Dictionary<string, object?> Series(string source, string? name = null, string? label = null,
        int? dataId = null, int[]? color = null)
    {
        var s = new Dictionary<string, object?> { ["source"] = source };
        if (name != null) s["name"] = name;
        if (label != null) s["label"] = label;
        if (dataId.HasValue) s["dataId"] = dataId.Value;
        if (color != null) s["color"] = color;
        return s;
    }

    private static Dictionary<string, object?> Panel(string id, string name, string caption, int height, int ySyncId,
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

    private static string BuildAndWriteView(string outputDir, string fileBaseName,
        List<(string Label, string SignalName, string? PnLName, int[] Color)> series)
    {
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
