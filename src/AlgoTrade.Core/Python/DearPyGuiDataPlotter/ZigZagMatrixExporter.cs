using System.Globalization;
using System.Text;
using AlgoTrade.Core.AppConfig;

namespace AlgoTrade.Core.Python.DearPyGuiDataPlotter;

/// <summary>Writes the C#-calculated ZigZag profiles as a bar-by-bar matrix.</summary>
public sealed class ZigZagMatrixExporter
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public (string? csvPath, string? txtPath) Export(
        IReadOnlyList<StockData> data,
        IReadOnlyList<OhlcZigZagSeries> profiles,
        string outputDirectory,
        string symbol,
        string period,
        ZigZagExportConfig config)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(profiles);
        ArgumentNullException.ThrowIfNull(config);
        Directory.CreateDirectory(outputDirectory);

        int[] ignoredLastPivot = profiles
            .Select(profile => config.IncludeUnconfirmedLastPivot
                ? -1
                : Array.FindLastIndex(profile.Pivots, value => value != 0))
            .ToArray();

        string? csvPath = config.WriteCsv
            ? Path.Combine(outputDirectory, "ZigZagPivotLists.csv") : null;
        string? txtPath = config.WriteTxt
            ? Path.Combine(outputDirectory, "ZigZagPivotLists.txt") : null;

        if (csvPath is not null)
            WriteCsv(csvPath, data, profiles, ignoredLastPivot);
        if (txtPath is not null)
            WriteTxt(txtPath, data, profiles, ignoredLastPivot, symbol, period);
        return (csvPath, txtPath);
    }

    private static void WriteCsv(
        string targetPath, IReadOnlyList<StockData> data,
        IReadOnlyList<OhlcZigZagSeries> profiles, int[] ignoredLastPivot)
    {
        WriteAtomic(targetPath, writer =>
        {
            writer.Write("BarNo;Date;Time;Open;High;Low;Close;Volume;Size;Result;Score");
            foreach (OhlcZigZagSeries profile in profiles)
                writer.Write($";{CsvEscape(profile.Name)}");
            writer.WriteLine();

            for (int barIndex = 0; barIndex < data.Count; barIndex++)
            {
                StockData bar = data[barIndex];
                int troughCount = 0, peakCount = 0, score = 0;
                var values = new int[profiles.Count];
                for (int profileIndex = 0; profileIndex < profiles.Count; profileIndex++)
                {
                    int value = barIndex == ignoredLastPivot[profileIndex]
                        ? 0 : profiles[profileIndex].Pivots[barIndex];
                    values[profileIndex] = value;
                    if (value < 0) troughCount++;
                    else if (value > 0) peakCount++;
                    score += value;
                }

                writer.Write(barIndex.ToString(Invariant));
                writer.Write($";{bar.DateTime:yyyy.MM.dd};{bar.DateTime:HH:mm:ss}");
                writer.Write($";{Number(bar.Open)};{Number(bar.High)};{Number(bar.Low)};{Number(bar.Close)}");
                writer.Write($";{Number(bar.Volume)};{bar.Size.ToString(Invariant)}");
                writer.Write($";{BuildResult(troughCount, peakCount)};{score.ToString(Invariant)}");
                foreach (int value in values) writer.Write($";{value.ToString(Invariant)}");
                writer.WriteLine();
            }
        }, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }

    private static void WriteTxt(
        string targetPath, IReadOnlyList<StockData> data,
        IReadOnlyList<OhlcZigZagSeries> profiles, int[] ignoredLastPivot,
        string symbol, string period)
    {
        WriteAtomic(targetPath, writer =>
        {
            writer.WriteLine($"ZIGZAG BAR-BY-BAR PIVOT DATA - {symbol} ({period})");
            writer.WriteLine($"Generated: {DateTime.Now:yyyy.MM.dd HH:mm:ss}");
            if (data.Count > 0)
                writer.WriteLine($"BarCount: {data.Count} | {data[0].DateTime:yyyy.MM.dd HH:mm:ss} — {data[^1].DateTime:yyyy.MM.dd HH:mm:ss}");
            string header = BuildTxtHeader(profiles);
            writer.WriteLine(new string('=', header.Length));
            writer.WriteLine(header);
            writer.WriteLine(new string('-', header.Length));

            for (int barIndex = 0; barIndex < data.Count; barIndex++)
            {
                StockData bar = data[barIndex];
                int troughCount = 0, peakCount = 0, score = 0;
                var values = new int[profiles.Count];
                for (int profileIndex = 0; profileIndex < profiles.Count; profileIndex++)
                {
                    int value = barIndex == ignoredLastPivot[profileIndex]
                        ? 0 : profiles[profileIndex].Pivots[barIndex];
                    values[profileIndex] = value;
                    if (value < 0) troughCount++;
                    else if (value > 0) peakCount++;
                    score += value;
                }

                writer.Write($"{barIndex,9} | {bar.DateTime:yyyy.MM.dd} | {bar.DateTime:HH:mm:ss} | ");
                writer.Write($"{bar.Open,12:0.########} | {bar.High,12:0.########} | {bar.Low,12:0.########} | {bar.Close,12:0.########} | ");
                writer.Write($"{bar.Volume,14:0.########} | {bar.Size,10} | {BuildResult(troughCount, peakCount),9} | {score,5}");
                foreach (int value in values) writer.Write($" | {value,10}");
                writer.WriteLine();
            }
        }, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string BuildTxtHeader(IReadOnlyList<OhlcZigZagSeries> profiles)
    {
        var builder = new StringBuilder(
            "    BarNo |       Date |     Time |         Open |         High |          Low |        Close |         Volume |       Size |    Result | Score");
        foreach (OhlcZigZagSeries profile in profiles)
        {
            string name = profile.Name.StartsWith("ZigZag ", StringComparison.OrdinalIgnoreCase)
                ? "ZZ " + profile.Name[7..]
                : profile.Name;
            if (name.Length > 10) name = name[..10];
            builder.Append($" | {name,10}");
        }
        return builder.ToString();
    }

    private static string BuildResult(int troughCount, int peakCount)
    {
        if (troughCount == 0 && peakCount == 0) return "";
        if (troughCount == 0) return $"{peakCount}T";
        if (peakCount == 0) return $"{troughCount}D";
        return $"{troughCount}D,{peakCount}T";
    }

    private static string Number(double value) => value.ToString("0.#################", Invariant);
    private static string CsvEscape(string value) => value.Contains(';') || value.Contains('"')
        ? $"\"{value.Replace("\"", "\"\"")}\"" : value;

    private static void WriteAtomic(string targetPath, Action<StreamWriter> write, Encoding encoding)
    {
        string temporaryPath = targetPath + ".tmp";
        using (var writer = new StreamWriter(temporaryPath, false, encoding, bufferSize: 1024 * 1024))
            write(writer);

        Exception? lastError = null;
        for (int attempt = 1; attempt <= 10; attempt++)
        {
            try
            {
                // Windows'ta Notepad/preview gibi okuyucular hedef dosyayi acik
                // tutarken rename/delete engellenebilir. Hedef paylasimli modda
                // bastan yazilarak bu kilit turu tolere edilir.
                // File.Delete'ten once iki stream de kapanmis olmali. "using var"
                // kullanilirsa scope sonuna kadar acik kalir ve exporter kendi
                // temporary dosyasini kilitlemis olur.
                using (var source = new FileStream(
                           temporaryPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var target = new FileStream(
                           targetPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite,
                           bufferSize: 1024 * 1024, FileOptions.SequentialScan))
                {
                    source.CopyTo(target, 1024 * 1024);
                    target.Flush(flushToDisk: true);
                }
                File.Delete(temporaryPath);
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                lastError = ex;
                if (attempt < 10) Thread.Sleep(200);
            }
        }

        throw new IOException(
            $"ZigZag export target could not be replaced after retries: {targetPath}. " +
            $"Completed temporary file was preserved at: {temporaryPath}", lastError);
    }
}
