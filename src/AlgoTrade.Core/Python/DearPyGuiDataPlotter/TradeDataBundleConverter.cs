// SHOW_CHILD_TRADERS_IN_NEW_PLOTTER: açmak için aşağıdaki satırın başındaki "//"yi kaldırın.
// Açarsanız ConvertMultipleTrader, child trader'ları Return/Return % panellerine de overlay
// olarak ekler (bkz. docs/yapilacak.md, src/PythonPlotter/multiple_data_plotter.py:ShowChildsData
// ile aynı felsefe). #define using'lerden önce olmak zorunda (C# kuralı), bu yüzden dosyanın
// en başında duruyor. Varsayılan: kapalı (sadece mainTrader).
//#define SHOW_CHILD_TRADERS_IN_NEW_PLOTTER

using AlgoTrade.Core.Trading;
using AlgoTrade.Core.Trading.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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
        => ConvertCore(trader, outputDir, fileBaseName, childTraders: new List<SingleTrader>(),
            includeChildReturnOverlays: false, includeRunMetrics: true);

    /// <summary>
    /// MultipleTrader'ın mainTrader'ını (bkz. <see cref="MultipleTrader.GetMainTrader"/>)
    /// ConvertSingleTrader'ın yaptığının aynısıyla bundle'a çevirir. Eski tip (pythonnet)
    /// MultipleDataPlotter (src/PythonPlotter/multiple_data_plotter.py) ile BİREBİR aynı davranış:
    /// Signals / PnL Price / PnL % panelleri her ZAMAN tüm trader'ları (main + tüm child'lar)
    /// overlay olarak gösterir — koşulsuz. SADECE Return / Return % panelleri
    /// src/PythonPlotter/multiple_data_plotter.py:ShowChildsData sabitiyle AYNI felsefedeki
    /// SHOW_CHILD_TRADERS_IN_NEW_PLOTTER preprocessor sembolüyle kontrol edilir (varsayılan
    /// kapalı — sadece mainTrader) — açmak için .csproj'daki &lt;DefineConstants&gt;'a ekleyin
    /// (bkz. docs/yapilacak.md).
    /// </summary>
    /// <param name="multipleTrader">RunMultipleTraderWithProgressAsync() sonrası MultipleTrader instance'ı.</param>
    /// <param name="outputDir">.npz/.view.json'ın yazılacağı klasör (örn. DearPyGuiDataPlotter/inputs).</param>
    /// <param name="fileBaseName">Uzantısız dosya adı (varsayılan: her seferinde aynı isim, üzerine yazılır).</param>
    /// <returns>Yazılan .npz ve .view.json dosyalarının tam yolları.</returns>
    public (string bundlePath, string viewPath) ConvertMultipleTrader(MultipleTrader multipleTrader,
        string outputDir, string fileBaseName = "latest_bundle")
    {
        if (multipleTrader == null)
            throw new ArgumentNullException(nameof(multipleTrader));

#if SHOW_CHILD_TRADERS_IN_NEW_PLOTTER
        bool includeChildReturnOverlays = true;
#else
        bool includeChildReturnOverlays = false;
#endif

        return ConvertCore(multipleTrader.GetMainTrader(), outputDir, fileBaseName,
            multipleTrader.Traders, includeChildReturnOverlays);
    }

    public (string bundlePath, string viewPath) ConvertSingleTrader(SingleTrader trader,
        string outputDir, string fileBaseName, IReadOnlyDictionary<string, double[]> strategyIndicators)
        => ConvertCore(trader, outputDir, fileBaseName, new List<SingleTrader>(),
            includeChildReturnOverlays: false, includeRunMetrics: true, strategyIndicatorOverride: strategyIndicators);

    /// <summary>
    /// "Opt Sonuçları Overlay" menü maddesi için: JSON'dan okunan N bağımsız koşumun her birinin
    /// Return/Net Return eğrisini, kullanıcının verdiği etiketle (childLabels) TEK bir bundle'a
    /// overlay olarak yazar. <paramref name="mainTraderIndex"/>'teki run'ın OHLC/Signals/PnL/
    /// metrik-grid verisi ana panelleri besler (bkz. plan dokümanı §2.5 "ana sinyal kaynağı" ile
    /// AYNI run olmalı - aksi halde metrik grid ile OHLC'deki sinyal birbirini tutmaz); TÜM
    /// overlayTraders (mainTraderIndex dahil) kendi etiketiyle Return panelinde ayrı renkte
    /// çizilir (mainTraderIndex'teki koşum için mainTrader'ın kendi "Return"/"Net Return"
    /// çizgisiyle üst üste binen, etiketsiz bir fazladan çizgi oluşur - kozmetik bir tekrar,
    /// veri hatası değil). Bkz. docs/opt-sonuclari-overlay-plot-plani.md §3.3-3.4.
    /// </summary>
    /// <param name="mainTraderIndex">
    /// Hangi overlayTraders elemanının OHLC/PnL/metrik-grid kaynağı olacağı (varsayılan 0).
    /// Çağıran taraf, "ana sinyal kaynağı" seçimiyle (composite/belirli id) AYNI run'ı vermeli.
    /// </param>
    /// <param name="mainSignalOverride">
    /// Verilirse (bkz. OptOverlaySignalCompositor.Compute) OHLC/Signals panelindeki "ana" çizgi
    /// mainTraderIndex'teki run'ın kendi sinyali DEĞİL, bu bileşke dizisi olur - ama metrik grid/
    /// PnL panelleri YİNE mainTraderIndex'teki run'dan beslenir (composite'in kendi P&L'i yok).
    /// </param>
    public (string bundlePath, string viewPath) ConvertOptOverlayRuns(
        List<SingleTrader> overlayTraders, IReadOnlyList<string> overlayLabels,
        string outputDir, string fileBaseName = "opt_overlay_bundle",
        double[]? mainSignalOverride = null, int mainTraderIndex = 0)
    {
        if (overlayTraders == null || overlayTraders.Count == 0)
            throw new ArgumentException("overlayTraders boş olamaz.", nameof(overlayTraders));
        if (overlayLabels == null || overlayLabels.Count != overlayTraders.Count)
            throw new ArgumentException("overlayLabels sayısı overlayTraders ile eşleşmeli.", nameof(overlayLabels));
        if (mainTraderIndex < 0 || mainTraderIndex >= overlayTraders.Count)
            mainTraderIndex = 0;

        return ConvertCore(overlayTraders[mainTraderIndex], outputDir, fileBaseName, overlayTraders,
            includeChildReturnOverlays: true, includeRunMetrics: true, childLabels: overlayLabels,
            mainSignalOverride: mainSignalOverride);
    }

    private static (string bundlePath, string viewPath) ConvertCore(SingleTrader trader,
        string outputDir, string fileBaseName, List<SingleTrader> childTraders,
        bool includeChildReturnOverlays, bool includeRunMetrics = false,
        IReadOnlyDictionary<string, double[]>? strategyIndicatorOverride = null,
        IReadOnlyList<string>? childLabels = null,
        double[]? mainSignalOverride = null)
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
        // Eski tip plotter'ın (PythonPlotter.ExtractTraderData/ExtractBundleData) beklediği ama
        // önceden bundle'a yazılmayan 3 seri (bkz. docs/todo.md "Kapatılması gereken küçük
        // boşluklar" — 2026-08-25 eklendi).
        AddSeries("Balance", lists.BakiyeFiyatList);
        AddSeries("Commission", lists.KomisyonFiyatList);
        AddSeries("Net Balance", lists.BakiyeFiyatNetList);

        var strategyIndicatorNames = new List<string>();
        var strategyIndicators = strategyIndicatorOverride ?? trader.Strategy?.GetPlotIndicators();
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

        // Child trader overlay'leri (sadece ConvertMultipleTrader'dan gelir, childTraders SingleTrader
        // için hep boş). Eski tip (multiple_data_plotter.py) ile birebir aynı davranış:
        // Signal/PnL/PnL % HER ZAMAN eklenir (koşulsuz); Return/Net Return/Return %/Net Return %
        // sadece includeChildReturnOverlays (SHOW_CHILD_TRADERS_IN_NEW_PLOTTER) açıkken eklenir.
        // Hepsi generic "indicator" serisi olarak bundle'a yazılır, panel eşlemesi BuildAndWriteView'da.
        var childOverlays = new List<ChildOverlaySeries>();
        for (int c = 0; c < childTraders.Count; c++)
        {
            Lists? childLists = childTraders[c].lists;
            if (childLists == null) continue;

            string label = (childLabels != null && c < childLabels.Count) ? childLabels[c] : $"Child {c + 1}";
            string signalName = $"{label} Signal";
            string pnlName = $"{label} PnL";
            string pnlPctName = $"{label} PnL %";
            string returnName = $"{label} Return";
            string netReturnName = $"{label} Net Return";
            string returnPctName = $"{label} Return %";
            string netReturnPctName = $"{label} Net Return %";

            AddSeries(signalName, childLists.SinyalList);
            AddSeries(pnlName, childLists.KarZararFiyatList);
            AddSeries(pnlPctName, childLists.KarZararFiyatYuzdeList);
            if (includeChildReturnOverlays)
            {
                AddSeries(returnName, childLists.GetiriFiyatList);
                AddSeries(netReturnName, childLists.GetiriFiyatNetList);
                AddSeries(returnPctName, childLists.GetiriFiyatYuzdeList);
                AddSeries(netReturnPctName, childLists.GetiriFiyatYuzdeNetList);
            }

            childOverlays.Add(new ChildOverlaySeries(label, signalName, pnlName, pnlPctName,
                returnName, netReturnName, returnPctName, netReturnPctName, ChildColor(c + 1)));
        }

        // signal_codes: TradeSignalRenderer icin SEYREK (sadece degisim barlarinda) event kodu.
        // signal_steps: ayri "Signals" paneli icin YOGUN (her barda tekrar eden) durum kodu.
        // mainSignalOverride (opsiyonel - bkz. ConvertOptOverlayRuns/OptOverlaySignalCompositor):
        // verilmişse trader.lists.SinyalList YERİNE bu dizi kullanılır - OHLC/Signals panelindeki
        // "ana" çizgi, mainTrader'ın kendi sinyali değil, N run'dan türetilen bir bileşke olur.
        IReadOnlyList<double> mainSinyalSource = mainSignalOverride != null ? mainSignalOverride : lists.SinyalList;
        var signalCodes = BuildSignalCodes(mainSinyalSource, n);
        var signalSteps = mainSinyalSource.Take(n).Select(v => (long)v).ToList();

        var meta = new Dictionary<string, object?>
        {
            ["symbol"] = trader.SymbolName ?? "AlgoTrade",
            ["periyot"] = trader.SymbolPeriod ?? "1H",
            ["intraday"] = true,
        };

        if (includeRunMetrics && trader.statistics is { } stats)
        {
            meta["run_metrics"] = new Dictionary<string, object?>
            {
                ["GetiriFiyat"] = stats.GetiriFiyat,
                ["GetiriFiyatNet"] = stats.GetiriFiyatNet,
                ["GetiriFiyatYuzde"] = stats.GetiriFiyatYuzde,
                ["GetiriFiyatYuzdeNet"] = stats.GetiriFiyatYuzdeNet,
                ["KomisyonFiyat"] = stats.KomisyonFiyat,
                ["KomisyonBrutKarPayiYuzde"] = stats.KomisyonBrutKarPayiYuzde,
                ["MinBakiyeFiyat"] = stats.MinBakiyeFiyat,
                ["MaxBakiyeFiyat"] = stats.MaxBakiyeFiyat,
                ["MinBakiyeFiyatNet"] = stats.MinBakiyeFiyatNet,
                ["MaxBakiyeFiyatNet"] = stats.MaxBakiyeFiyatNet,
                ["ProfitFactor"] = double.IsFinite(stats.ProfitFactor) ? stats.ProfitFactor : null,
                ["IslemSayisi"] = stats.IslemSayisi,
                ["KarliIslemOrani"] = stats.KarliIslemOrani,
                ["GetiriMaxDD"] = stats.GetiriMaxDD,
                ["GetiriMaxDDNet"] = stats.GetiriMaxDDNet,
                ["ProfitFactorNet"] = double.IsFinite(stats.ProfitFactorNet) ? stats.ProfitFactorNet : null,
                ["ScoreFiyatNet"] = double.IsFinite(stats.ScoreFiyatNet) ? stats.ScoreFiyatNet : null,
                ["ScoreFiyat"] = double.IsFinite(stats.ScoreFiyat) ? stats.ScoreFiyat : null,
                ["KazandiranIslemSayisi"] = stats.KazandiranIslemSayisi,
                ["KaybettirenIslemSayisi"] = stats.KaybettirenIslemSayisi,
                ["NotrIslemSayisi"] = stats.NotrIslemSayisi,
            };
        }

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

        string viewPath = BuildAndWriteView(outputDir, fileBaseName, trader.SymbolName, strategyIndicatorNames,
            childOverlays, includeChildReturnOverlays, includeRunMetrics);
        return (bundlePath, viewPath);
    }

    /// <summary>src/PythonPlotter/multiple_data_plotter.py:_TRADER_COLORS ile aynı palet (0-255 RGBA'ya çevrilmiş, index 0=mainTrader/beyaz).</summary>
    private static readonly int[][] TraderColors =
    {
        new[] { 255, 255, 255, 255 }, // mainTrader — beyaz (şu an kullanılmıyor, index paritesi için duruyor)
        new[] { 51, 204, 255, 255 },  // child 1 — cyan
        new[] { 255, 204, 0, 255 },   // child 2 — sarı
        new[] { 76, 255, 76, 255 },   // child 3 — yeşil
        new[] { 255, 102, 102, 255 }, // child 4 — kırmızı
        new[] { 255, 128, 0, 255 },   // child 5 — turuncu
        new[] { 178, 102, 255, 255 }, // child 6 — mor
        new[] { 0, 255, 204, 255 },   // child 7 — teal
        new[] { 255, 51, 204, 255 },  // child 8 — pembe
    };

    private static int[] ChildColor(int traderColorIndex) => TraderColors[traderColorIndex % TraderColors.Length];

    /// <summary>Bir child trader'ın Signals/PnL/PnL %/Return/Return % panellerine eklenecek overlay serileri (bkz. ConvertMultipleTrader).</summary>
    private sealed record ChildOverlaySeries(string Label, string SignalName, string PnLName, string PnLPctName,
        string ReturnName, string NetReturnName, string ReturnPctName, string NetReturnPctName, int[] Color);

    /// <summary>
    /// AlgoTrade'in SinyalList'i (SingleTrader.cs: SonYon=="A"→1.0, "S"→-1.0, "F"→0.0)
    /// HER barda o anki durumu tekrar eder (state). DearPyGuiDataPlotter'ın
    /// signal_codes'u ise EVENT bekliyor (bkz. default.py SIGNAL_CODE_TO_TEXT:
    /// 0=sinyal yok, 1=AL, -1=SAT, 2=FLAT) - TradeSignalRenderer her "run"u
    /// bir sinyalin ateşlendiği bar ile başlatıyor, dense veri verilirse her
    /// bar ayrı bir run olur (yanlış + çok yavaş). Bu yüzden burada sadece
    /// DEĞİŞİM barlarında kod yazıyoruz, diğerlerinde 0 (sinyal yok) bırakıyoruz.
    /// </summary>
    private static long[] BuildSignalCodes(IReadOnlyList<double> sinyalList, int n)
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
        List<string> strategyIndicatorNames, List<ChildOverlaySeries> childOverlays,
        bool includeChildReturnOverlays, bool includeMetricGrid)
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

        // Child overlay'lerde gross (Return/Return %) soluk, net (Net Return/Net Return %) tam
        // renkte çizilir — src/PythonPlotter/multiple_data_plotter.py'deki dim/color ayrımıyla aynı.
        static int[] Dim(int[] color) => new[] { color[0] / 2, color[1] / 2, color[2] / 2, 178 };

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
            // % Change: OHLC'nin hemen altında, "PnL Price" panelinin AYNI iskeleti
            // (eski adı Pnl2) - id/name/caption/ySyncId farklı. Seri kaynağı
            // "windowpercent": statik bir indicator array'i DEĞİL, default.py'de
            // Range Measure A/B / aktif separator'a göre HER FRAME yeniden
            // hesaplanan canlı bir % serisi (bkz. default.py stage3FillPanelsFromView
            // + updatePctChangePanel hook'u).
            Panel("pctChange", "% Change", "% Change", 220, 7,
                new() { Series("windowpercent", "Change %", "Change %", color: yellow) }, yLabel: "% Change"),
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

        // multiple_data_plotter.py ile birebir aynı davranış:
        // Signals/PnL/PnL % → child overlay'leri HER ZAMAN eklenir (koşulsuz).
        // NOT: index'ler "pctChange" panels[1]'e eklendigi icin +1 kaydi.
        var signalsPanelSeries = panels[2]["series"] as List<Dictionary<string, object?>>;
        var pnlPanelSeries = panels[3]["series"] as List<Dictionary<string, object?>>;
        var pnlPctPanelSeries = panels[4]["series"] as List<Dictionary<string, object?>>;
        foreach (var child in childOverlays)
        {
            signalsPanelSeries!.Add(Series("indicator", child.SignalName, $"{child.Label} Signal", color: child.Color));
            pnlPanelSeries!.Add(Series("indicator", child.PnLName, $"{child.Label} PnL", color: child.Color));
            pnlPctPanelSeries!.Add(Series("indicator", child.PnLPctName, $"{child.Label} PnL %", color: child.Color));
        }

        // Return/Return % → child overlay'leri SADECE includeChildReturnOverlays
        // (SHOW_CHILD_TRADERS_IN_NEW_PLOTTER) açıkken eklenir — multiple_data_plotter.py:ShowChildsData ile aynı.
        if (includeChildReturnOverlays)
        {
            var returnPanelSeries = panels[5]["series"] as List<Dictionary<string, object?>>;
            var returnPctPanelSeries = panels[6]["series"] as List<Dictionary<string, object?>>;
            foreach (var child in childOverlays)
            {
                int[] dim = Dim(child.Color);
                returnPanelSeries!.Add(Series("indicator", child.ReturnName, $"{child.Label} Gross", color: dim));
                returnPanelSeries.Add(Series("indicator", child.NetReturnName, $"{child.Label} Net", color: child.Color));
                returnPctPanelSeries!.Add(Series("indicator", child.ReturnPctName, $"{child.Label} Gross %", color: dim));
                returnPctPanelSeries.Add(Series("indicator", child.NetReturnPctName, $"{child.Label} Net %", color: child.Color));
            }
        }

        if (strategyIndicatorNames.Count > 0)
        {
            var strategySeries = strategyIndicatorNames.Select(n => Series("indicator", n, n)).ToList();
            panels.Add(Panel("strategyIndicators", "Strategy Indicators",
                string.Join(" / ", strategyIndicatorNames), 260, 6, strategySeries, yLabel: "Indicators"));
        }

        var view = new Dictionary<string, object?> { ["panels"] = panels };
        if (includeMetricGrid)
        {
            string metricGridPath = Path.Combine(AppSettings.ConfigsDir, "OhlcMetricCells.json");
            try
            {
                if (File.Exists(metricGridPath))
                    view["ohlcMetricGrid"] = JObject.Parse(File.ReadAllText(metricGridPath));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                Console.WriteLine($"OHLC metric grid: {ex.Message}");
            }
        }
        string viewPath = Path.Combine(outputDir, fileBaseName + ".view.json");
        File.WriteAllText(viewPath, JsonConvert.SerializeObject(view, Formatting.Indented));
        return viewPath;
    }
}
