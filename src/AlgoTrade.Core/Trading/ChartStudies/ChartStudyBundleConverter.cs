using AlgoTrade.Core.Python.DearPyGuiDataPlotter;
using AlgoTrade.Core.Trading.Indicators;
using Newtonsoft.Json;

namespace AlgoTrade.Core.Trading.ChartStudies;

/// <summary>ChartStudy için stratejiden bağımsız ham OHLC bundle/view üretir.</summary>
public sealed class ChartStudyBundleConverter
{
    public (string bundlePath, string viewPath) Convert(
        IReadOnlyList<StockData> data, string outputDir, string symbol, string period,
        string startupScript, string fileBaseName = "chart_study")
    {
        if (data is null || data.Count == 0)
            throw new ArgumentException("ChartStudy verisi boş olamaz.", nameof(data));

        int count = data.Count;
        double[] closes = data.Select(x => x.Close).ToArray();
        using var indicators = new IndicatorManager(data.ToList());
        var series = new List<(string Name, double[] Values)>();
        foreach (int emaPeriod in new[] { 5, 8, 13, 21, 50, 100, 200 })
            series.Add(($"EMA {emaPeriod}", indicators.MA.EMA(closes, emaPeriod)));

        var macd = indicators.Momentum.MACD(closes, 12, 26, 9);
        series.Add(("MACD", macd.MACD));
        series.Add(("MACD Signal", macd.Signal));
        series.Add(("MACD Histogram", macd.Histogram));
        foreach (int rsiPeriod in new[] { 7, 14, 21 })
            series.Add(($"RSI {rsiPeriod}", indicators.Momentum.RSI(closes, rsiPeriod).Values));
        var stochastic = indicators.Momentum.Stochastic(14, 3);
        series.Add(("Stochastic %K", stochastic.K));
        series.Add(("Stochastic %D", stochastic.D));
        foreach (int momentumPeriod in new[] { 10, 20 })
        {
            var values = Enumerable.Repeat(double.NaN, count).ToArray();
            for (int i = momentumPeriod; i < count; i++)
                values[i] = closes[i] - closes[i - momentumPeriod];
            series.Add(($"Momentum {momentumPeriod}", values));
        }

        var writer = new NpzWriter();
        writer.AddFloatArray("open", data.Select(x => x.Open).ToArray());
        writer.AddFloatArray("high", data.Select(x => x.High).ToArray());
        writer.AddFloatArray("low", data.Select(x => x.Low).ToArray());
        writer.AddFloatArray("close", data.Select(x => x.Close).ToArray());
        writer.AddFloatArray("volume", data.Select(x => (double)x.Volume).ToArray());
        writer.AddIntArray("size", data.Select(x => (long)x.Size).ToArray());
        writer.AddStringArray("timestamps", data.Select(x => x.DateTime.ToString("o")).ToArray());
        writer.AddIntArray("signal_codes", new long[count]);
        writer.AddIntArray("signal_steps", new long[count]);
        writer.AddStringArray("indicator_names", series.Select(item => item.Name).ToArray());
        var indicatorMatrix = new double[series.Count, count];
        for (int row = 0; row < series.Count; row++)
            for (int column = 0; column < count; column++)
                indicatorMatrix[row, column] = column < series[row].Values.Length
                    ? series[row].Values[column]
                    : double.NaN;
        writer.AddFloat2DArray("indicator_values", indicatorMatrix);
        writer.AddScalarString("meta_json", JsonConvert.SerializeObject(new
        {
            symbol = string.IsNullOrWhiteSpace(symbol) ? "OHLC" : symbol,
            periyot = string.IsNullOrWhiteSpace(period) ? "N/A" : period,
            intraday = true,
            mode = "chart_study",
            barCount = count,
            startDateTime = data[0].DateTime.ToString("o"),
            endDateTime = data[^1].DateTime.ToString("o")
        }));

        Directory.CreateDirectory(outputDir);
        string bundlePath = Path.Combine(outputDir, fileBaseName + ".npz");
        writer.Save(bundlePath);

        var view = new
        {
            mode = "chart_study",
            startupScript,
            runStartupScript = true,
            panels = new object[]
            {
                new
                {
                    id = "ohlc", name = "OHLC + EMA", caption = "OHLC + EMA 5/8/13/21/50/100/200",
                    height = 430, ySyncId = 0,
                    series = new object[]
                    {
                        new { source = "ohlc", name = symbol, label = symbol, dataId = 0 },
                        IndicatorSeries("EMA 5", 1), IndicatorSeries("EMA 8", 2),
                        IndicatorSeries("EMA 13", 3), IndicatorSeries("EMA 21", 4),
                        IndicatorSeries("EMA 50", 5), IndicatorSeries("EMA 100", 6),
                        IndicatorSeries("EMA 200", 7)
                    }
                },
                Panel("macd", "MACD", 220, 1,
                    new[] { "MACD", "MACD Signal", "MACD Histogram" }, levels: new[] { 0.0 }),
                Panel("rsi", "RSI 7 / 14 / 21", 220, 2,
                    new[] { "RSI 7", "RSI 14", "RSI 21" }, new[] { 0.0, 100.0 }, new[] { 30.0, 70.0 }),
                Panel("stochastic", "Stochastic", 220, 3,
                    new[] { "Stochastic %K", "Stochastic %D" }, new[] { 0.0, 100.0 }, new[] { 20.0, 80.0 }),
                Panel("momentum", "Momentum", 220, 4,
                    new[] { "Momentum 10", "Momentum 20" }, levels: new[] { 0.0 })
            }
        };
        string viewPath = Path.Combine(outputDir, fileBaseName + ".view.json");
        File.WriteAllText(viewPath, JsonConvert.SerializeObject(view, Formatting.Indented));
        return (bundlePath, viewPath);
    }

    private static object IndicatorSeries(string name, int dataId) =>
        new { source = "indicator", name, label = name, dataId };

    private static object Panel(
        string id, string caption, int height, int ySyncId, string[] seriesNames,
        double[]? fixedRange = null, double[]? levels = null) => new
        {
            id,
            name = caption,
            caption,
            height,
            ySyncId,
            yFixedRange = fixedRange,
            series = seriesNames.Select((name, index) => IndicatorSeries(name, index + 1)).ToArray(),
            levels = (levels ?? Array.Empty<double>()).Select(value => new
            {
                orientation = "h",
                value,
                label = value.ToString("G")
            }).ToArray()
        };
}
