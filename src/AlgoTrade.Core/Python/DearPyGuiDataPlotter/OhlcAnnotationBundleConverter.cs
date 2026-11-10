using Newtonsoft.Json;
using AlgoTrade.Core.AppConfig;

namespace AlgoTrade.Core.Python.DearPyGuiDataPlotter;

/// <summary>
/// Ham StockData listesini dip/tepe etiketleme akışı için yalnız OHLC içeren
/// Dear ImGui bundle + view çiftine dönüştürür.
/// </summary>
public sealed class OhlcAnnotationBundleConverter
{
    public (string bundlePath, string viewPath) Convert(
        IReadOnlyList<StockData> data,
        string outputDir,
        string symbol,
        string period,
        SwingAnnotationConfig config,
        string? sessionDirectory = null,
        IReadOnlyList<OhlcZigZagSeries>? zigZagSeries = null,
        bool showManualMarkers = true,
        bool showZigZagMarkers = true,
        string fileBaseName = "swing_annotation_bundle")
    {
        if (data is null || data.Count == 0)
            throw new ArgumentException("OHLC verisi boş olamaz.", nameof(data));
        ArgumentNullException.ThrowIfNull(config);

        int count = data.Count;
        var opens = new double[count];
        var highs = new double[count];
        var lows = new double[count];
        var closes = new double[count];
        var volumes = new double[count];
        var sizes = new long[count];
        var timestamps = new string[count];
        var noSignals = new long[count];

        for (int i = 0; i < count; i++)
        {
            StockData bar = data[i];
            opens[i] = bar.Open;
            highs[i] = bar.High;
            lows[i] = bar.Low;
            closes[i] = bar.Close;
            volumes[i] = bar.Volume;
            sizes[i] = bar.Size;
            timestamps[i] = bar.DateTime.ToString("o");
        }

        var metadata = new Dictionary<string, object?>
        {
            ["symbol"] = string.IsNullOrWhiteSpace(symbol) ? "OHLC" : symbol,
            ["periyot"] = string.IsNullOrWhiteSpace(period) ? "N/A" : period,
            ["intraday"] = true,
            ["mode"] = "swing_annotation",
            ["barCount"] = count,
            ["startDateTime"] = data[0].DateTime.ToString("o"),
            ["endDateTime"] = data[count - 1].DateTime.ToString("o"),
            ["windowWidthBars"] = Math.Min(config.Window.InitialWidthBars, count),
            ["navigationStepBars"] = config.Window.NavigationStepBars,
        };

        var writer = new NpzWriter();
        writer.AddFloatArray("open", opens);
        writer.AddFloatArray("high", highs);
        writer.AddFloatArray("low", lows);
        writer.AddFloatArray("close", closes);
        if (config.Rendering.LoadVolume) writer.AddFloatArray("volume", volumes);
        if (config.Rendering.LoadSize) writer.AddIntArray("size", sizes);
        writer.AddStringArray("timestamps", timestamps);
        // PreparedData sözleşmesi bu alanları okuyabildiği için etiketleme modunda
        // boş sinyal dizileri açıkça yazılır; view içinde sinyal paneli bulunmaz.
        writer.AddIntArray("signal_codes", noSignals);
        writer.AddIntArray("signal_steps", noSignals);
        if (zigZagSeries is { Count: > 0 })
        {
            writer.AddStringArray("indicator_names", zigZagSeries.Select(z => z.Name).ToArray());
            writer.AddFloat2DArray("indicator_values", BuildMatrix(zigZagSeries, count, z => z.LineValues));
            writer.AddStringArray("zigzag_profile_names", zigZagSeries.Select(z => z.Name).ToArray());
            writer.AddIntArray("zigzag_show_markers", zigZagSeries.Select(
                z => z.ShowPivotMarkers ? 1L : 0L).ToArray());
            writer.AddIntArray("zigzag_visible", zigZagSeries.Select(
                z => z.Visible ? 1L : 0L).ToArray());
            writer.AddFloat2DArray("zigzag_pivots", BuildMatrix(
                zigZagSeries, count, z => z.Pivots.Select(value => (double)value).ToArray()));
            writer.AddFloat2DArray("zigzag_pivot_prices", BuildMatrix(zigZagSeries, count, z => z.PivotPrices));
        }
        writer.AddScalarString("meta_json", JsonConvert.SerializeObject(metadata));

        Directory.CreateDirectory(outputDir);
        string bundlePath = Path.Combine(outputDir, fileBaseName + ".npz");
        writer.Save(bundlePath);

        var view = new
        {
            mode = "swing_annotation",
            session = new
            {
                directory = sessionDirectory,
                symbol = string.IsNullOrWhiteSpace(symbol) ? "OHLC" : symbol,
                period = string.IsNullOrWhiteSpace(period) ? "N/A" : period,
                schemaVersion = 1,
            },
            window = new
            {
                initialViewMode = config.Window.InitialViewMode,
                initialWidthBars = Math.Min(config.Window.InitialWidthBars, count),
                navigationStepBars = Math.Min(config.Window.NavigationStepBars, count),
                initialOffsetBars = Math.Min(config.Window.InitialOffsetBars, Math.Max(0, count - 1)),
                minimumWidthBars = Math.Min(config.Window.MinimumWidthBars, count),
                maximumWidthBars = Math.Min(config.Window.MaximumWidthBars, count),
                clampLastWindowToData = config.Window.ClampLastWindowToData,
                autoSaveOnWindowChange = config.Window.AutoSaveOnWindowChange,
                presets = config.Window.Presets.Where(v => v <= count).ToArray(),
                totalBars = count,
            },
            rendering = new
            {
                maxCandlesWithoutAggregation = config.Rendering.MaxCandlesWithoutAggregation,
                showManualMarkers,
                showZigZagMarkers,
            },
            panels = new object[]
            {
                new
                {
                    id = "ohlc",
                    name = "OHLC",
                    caption = "OHLC Dip/Tepe Etiketleme",
                    height = 720,
                    ySyncId = 0,
                    series = BuildViewSeries(symbol, zigZagSeries)
                }
            }
        };

        string viewPath = Path.Combine(outputDir, fileBaseName + ".view.json");
        File.WriteAllText(viewPath, JsonConvert.SerializeObject(view, Formatting.Indented));
        return (bundlePath, viewPath);
    }

    private static object[] BuildViewSeries(string symbol, IReadOnlyList<OhlcZigZagSeries>? series)
    {
        var result = new List<object>
        {
            new { source = "ohlc", name = string.IsNullOrWhiteSpace(symbol) ? "OHLC" : symbol,
                  label = string.IsNullOrWhiteSpace(symbol) ? "OHLC" : symbol, dataId = 0 }
        };
        if (series is null) return result.ToArray();
        int dataId = 1;
        for (int i = 0; i < series.Count; i++)
        {
            OhlcZigZagSeries item = series[i];
            if (!item.Visible) continue;
            result.Add(new { source = "indicator", name = item.Name, label = item.Name,
                             dataId = dataId++, color = item.Color });
        }
        return result.ToArray();
    }

    private static double[,] BuildMatrix(
        IReadOnlyList<OhlcZigZagSeries> series, int count,
        Func<OhlcZigZagSeries, IReadOnlyList<double>> selector)
    {
        var matrix = new double[series.Count, count];
        for (int row = 0; row < series.Count; row++)
        {
            IReadOnlyList<double> values = selector(series[row]);
            for (int column = 0; column < count; column++)
                matrix[row, column] = column < values.Count ? values[column] : double.NaN;
        }
        return matrix;
    }
}

public sealed record OhlcZigZagSeries(
    string Name,
    double Deviation,
    double[] LineValues,
    int[] Pivots,
    double[] PivotPrices,
    int[] Color,
    bool ShowPivotMarkers,
    bool Visible);
