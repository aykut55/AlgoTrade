using AlgoTrade.Core.Trading;
using AlgoTrade.Core.Trading.Core;
using Newtonsoft.Json;

namespace AlgoTrade.Core.Python.DearPyGuiDataPlotter;

/// <summary>
/// SingleTrader sonuçlarını DearPyGuiDataPlotter'ın beklediği .npz bundle +
/// .view.json çiftine (bkz. scripts/default.py PreparedData/
/// stage2LoadPreparedData/stage3FillPanelsFromView) dönüştürür.
///
/// Üretilen panel seti (bkz. src/panels.jpg - imgui_bundle referans ekranı):
/// OHLC, Signals (AL/SAT/FLAT), PnL, PnL %, Return+Net Return, Return %+Net
/// Return %, ve stratejinin kendi indikatörleri (örn. MOST+EXMOV) için ayrı
/// bir panel.
/// </summary>
public class TradeDataBundleConverter
{
    /// <param name="trader">Finalize() çağrılmış SingleTrader instance'ı.</param>
    /// <param name="outputDir">.npz/.view.json'ın yazılacağı klasör (örn. DearPyGuiDataPlotter/inputs).</param>
    /// <param name="fileBaseName">Uzantısız dosya adı (varsayılan: her seferinde aynı isim, üzerine yazılır).</param>
    /// <returns>Yazılan .npz ve .view.json dosyalarının tam yolları.</returns>
    public (string bundlePath, string viewPath) ConvertSingleTrader(SingleTrader trader,
        string outputDir, string fileBaseName = "latest_bundle")
    {
        if (trader == null)
            throw new ArgumentNullException(nameof(trader));
        if (trader.Data == null || trader.Data.Count == 0)
            throw new InvalidOperationException("trader.Data boş. Finalize() sonrası çağırın.");

        Lists lists = trader.lists ?? throw new ArgumentException("trader.lists null.", nameof(trader));

        int n = trader.Data.Count;
        var opens = trader.Data.Select(d => d.Open).ToList();
        var highs = trader.Data.Select(d => d.High).ToList();
        var lows = trader.Data.Select(d => d.Low).ToList();
        var closes = trader.GetClosePrices();
        var volumes = trader.Data.Select(d => (double)d.Volume).ToList();
        var sizes = trader.Data.Select(d => (long)d.Size).ToList();
        var timestamps = trader.Data.Select(d => d.DateTime.ToString("o")).ToList();

        var seriesNames = new List<string>();
        var seriesRows = new List<double[]>();

        void AddSeries(string name, List<double>? values)
        {
            if (values == null || values.Count == 0) return;
            seriesNames.Add(name);
            seriesRows.Add(PadOrTrim(values.ToArray(), n));
        }

        AddSeries("PnL", lists.KarZararFiyatList);
        AddSeries("PnL %", lists.KarZararFiyatYuzdeList);
        AddSeries("Return", lists.GetiriFiyatList);
        AddSeries("Net Return", lists.GetiriFiyatNetList);
        AddSeries("Return %", lists.GetiriFiyatYuzdeList);
        AddSeries("Net Return %", lists.GetiriFiyatYuzdeNetList);

        var strategyIndicatorNames = new List<string>();
        var strategyIndicators = trader.Strategy?.GetPlotIndicators();
        if (strategyIndicators != null)
        {
            foreach (var (indicatorName, values) in strategyIndicators)
            {
                if (values == null || values.Length == 0) continue;
                strategyIndicatorNames.Add(indicatorName);
                seriesNames.Add(indicatorName);
                seriesRows.Add(PadOrTrim(values, n));
            }
        }

        // signal_codes: TradeSignalRenderer icin SEYREK (sadece degisim barlarinda) event kodu.
        // signal_steps: ayri "Signals" paneli icin YOGUN (her barda tekrar eden) durum kodu.
        var signalCodes = BuildSignalCodes(lists.SinyalList, n);
        var signalSteps = lists.SinyalList.Take(n).Select(v => (long)v).ToList();

        var meta = new Dictionary<string, object?>
        {
            ["symbol"] = trader.SymbolName ?? "AlgoTrade",
            ["intraday"] = true,
        };

        var writer = new NpzWriter();
        writer.AddFloatArray("open", opens);
        writer.AddFloatArray("high", highs);
        writer.AddFloatArray("low", lows);
        writer.AddFloatArray("close", closes);
        writer.AddFloatArray("volume", volumes);
        writer.AddIntArray("size", sizes);
        writer.AddStringArray("timestamps", timestamps);
        writer.AddIntArray("signal_codes", signalCodes);
        writer.AddIntArray("signal_steps", signalSteps);
        writer.AddScalarString("meta_json", JsonConvert.SerializeObject(meta));

        if (seriesNames.Count > 0)
        {
            writer.AddStringArray("indicator_names", seriesNames);
            var matrix = new double[seriesNames.Count, n];
            for (int r = 0; r < seriesNames.Count; r++)
                for (int c = 0; c < n; c++)
                    matrix[r, c] = seriesRows[r][c];
            writer.AddFloat2DArray("indicator_values", matrix);
        }

        Directory.CreateDirectory(outputDir);
        string bundlePath = Path.Combine(outputDir, fileBaseName + ".npz");
        writer.Save(bundlePath);

        string viewPath = BuildAndWriteView(outputDir, fileBaseName, trader.SymbolName, strategyIndicatorNames);
        return (bundlePath, viewPath);
    }

    /// <summary>
    /// AlgoTrade'in SinyalList'i (SingleTrader.cs: SonYon=="A"→1.0, "S"→-1.0, "F"→0.0)
    /// HER barda o anki durumu tekrar eder (state). DearPyGuiDataPlotter'ın
    /// signal_codes'u ise EVENT bekliyor (bkz. default.py SIGNAL_CODE_TO_TEXT:
    /// 0=sinyal yok, 1=AL, -1=SAT, 2=FLAT) - TradeSignalRenderer her "run"u
    /// bir sinyalin ateşlendiği bar ile başlatıyor, dense veri verilirse her
    /// bar ayrı bir run olur (yanlış + çok yavaş). Bu yüzden burada sadece
    /// DEĞİŞİM barlarında kod yazıyoruz, diğerlerinde 0 (sinyal yok) bırakıyoruz.
    /// </summary>
    private static long[] BuildSignalCodes(List<double> sinyalList, int n)
    {
        var codes = new long[n];
        double? previous = null;

        for (int i = 0; i < n && i < sinyalList.Count; i++)
        {
            double current = sinyalList[i];
            if (previous == null || current != previous.Value)
            {
                codes[i] = current switch
                {
                    1.0 => 1,   // AL
                    -1.0 => -1, // SAT
                    0.0 => 2,   // FLAT
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

    /// <summary>
    /// src/panels.jpg'deki 7 panelli layout'u üreten view.json'ı yazar:
    /// OHLC, Signals, PnL, PnL %, Return+Net Return, Return %+Net Return %,
    /// ve (varsa) stratejinin kendi indikatörleri (örn. MOST+EXMOV).
    /// </summary>
    private static string BuildAndWriteView(string outputDir, string fileBaseName, string? symbol,
        List<string> strategyIndicatorNames)
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

        // RGBA (0-255) - view.json'daki seriesSpec["color"] üzerinden Panel.addData(color=...)'a gider.
        // cyan: src/panels.jpg'deki (imgui_bundle referans) Signals çizgisinin gerçek rengi -
        // ilk denemede magenta yazılmıştı ama referans görüntüde açık camgöbeği/cyan kullanılıyor.
        int[] cyan = new[] { 60, 200, 255, 255 };
        int[] yellow = new[] { 255, 255, 0, 255 };

        static Dictionary<string, object?> Panel(string id, string name, string caption, int height, int ySyncId,
            List<Dictionary<string, object?>> series, string? yLabel = null, double[]? yFixedRange = null)
        {
            var p = new Dictionary<string, object?>
            {
                ["id"] = id,
                ["name"] = name,
                ["caption"] = caption,
                ["height"] = height,
                ["ySyncId"] = ySyncId,
                ["series"] = series,
            };
            if (yLabel != null) p["yLabel"] = yLabel;
            if (yFixedRange != null) p["yFixedRange"] = yFixedRange;
            return p;
        }

        var panels = new List<Dictionary<string, object?>>
        {
            // dataId=0 ZORUNLU: TradeSignalRenderer.draw() OHLC panelinin candle serisini
            // hep dataId=0 varsayarak arıyor (bkz. Panel.setCandleData varsayılanı).
            Panel("ohlc", "OHLC", "OHLC", 380, 0,
                new() { Series("ohlc", symbol ?? "OHLC", dataId: 0) }),
            Panel("signals", "Signals", "Signals (AL/SAT/FLAT)", 200, 1,
                new() { Series("signalsteps", "Signal Step", "Signals", color: cyan) },
                yLabel: "Signals", yFixedRange: new double[] { -2, 2 }),
            Panel("pnl", "PnL Price", "PnL", 220, 2,
                new() { Series("indicator", "PnL", "PnL", color: yellow) }, yLabel: "PnL Price"),
            Panel("pnlPct", "PnL Price %", "PnL %", 220, 3,
                new() { Series("indicator", "PnL %", "PnL %", color: yellow) }, yLabel: "PnL Price %"),
            Panel("returnCombo", "Return", "Return / Net Return", 220, 4,
                new() { Series("indicator", "Return", "Return"), Series("indicator", "Net Return", "Net Return") }, yLabel: "Return"),
            Panel("returnPctCombo", "Return %", "Return % / Net Return %", 220, 5,
                new() { Series("indicator", "Return %", "Return %"), Series("indicator", "Net Return %", "Net Return %") }, yLabel: "Return %"),
        };

        if (strategyIndicatorNames.Count > 0)
        {
            var strategySeries = strategyIndicatorNames.Select(n => Series("indicator", n, n)).ToList();
            panels.Add(Panel("strategyIndicators", "Strategy Indicators",
                string.Join(" / ", strategyIndicatorNames), 260, 6, strategySeries, yLabel: "Indicators"));
        }

        var view = new Dictionary<string, object?> { ["panels"] = panels };
        string viewPath = Path.Combine(outputDir, fileBaseName + ".view.json");
        File.WriteAllText(viewPath, JsonConvert.SerializeObject(view, Formatting.Indented));
        return viewPath;
    }
}
