using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using StatisticsModel = AlgoTrade.Core.Trading.Statistics.Statistics;

namespace AlgoTrade.Core.Trading.Utils;

public class StatisticsExporter
{
    private const string Separator = "#SEPARATOR#";
    private readonly StatisticsModel _statistics;

    public StatisticsExporter(StatisticsModel statistics)
    {
        _statistics = statistics ?? throw new ArgumentNullException(nameof(statistics));
    }

    public void SaveToTxt(string filePath)
    {
        _statistics.AssignToMapForExport();
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"TRADING STATISTICS REPORT - {_statistics.SistemName} ({_statistics.GrafikSembol})");
        sb.AppendLine($"Generated: {DateTime.Now:yyyy.MM.dd HH:mm:ss}");
        sb.AppendLine("================================================================================");
        sb.AppendLine($"{"Property Name".PadRight(40)} : Value");
        sb.AppendLine("--------------------------------------------------------------------------------");

        foreach (var kvp in _statistics.StatisticsMap)
        {
            if (kvp.Key.StartsWith(Separator))
                sb.AppendLine();
            else
                sb.AppendLine($"{kvp.Key.PadRight(40)} : {kvp.Value}");
        }

        sb.AppendLine("================================================================================");
        WriteAllTextShared(filePath, sb.ToString());
    }

    public void SaveToCsv(string filePath)
    {
        _statistics.AssignToMapForExport();
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("Key;Value");

        foreach (var kvp in _statistics.StatisticsMap)
        {
            if (!kvp.Key.StartsWith(Separator))
                sb.AppendLine($"{kvp.Key};{kvp.Value}");
        }

        WriteAllTextShared(filePath, sb.ToString());
    }

    public void SaveListsToTxt(string filePath)
    {
        var trader = _statistics.TraderForExport;
        if (trader == null || trader.Data == null || trader.Data.Count == 0)
            return;

        using StreamWriter writer = CreateSharedWriter(filePath);
        writer.WriteLine($"BAR-BY-BAR TRADING DATA (ALL) - {_statistics.SistemName} ({_statistics.GrafikSembol})");
        writer.WriteLine($"Generated: {DateTime.Now:yyyy.MM.dd HH:mm:ss}");
        writer.WriteLine("".PadRight(500, '='));

        writer.WriteLine(
            $"{"BarNo",7} | " +
            $"{"Date",10} | " +
            $"{"Time",8} | " +
            $"{"Open",10} | " +
            $"{"High",10} | " +
            $"{"Low",10} | " +
            $"{"Close",10} | " +
            $"{"Volume",10} | " +
            $"{"Yon",3} | " +
            $"{"Seviye",10} | " +
            $"{"Sinyal",6} | " +
            $"{"TrdEnbl",7} | " +
            $"{"PozKpEnbl",9} | " +
            $"{"KzPuan",10} | " +
            $"{"KzFiyat",10} | " +
            $"{"KzPuan%",10} | " +
            $"{"KzFiyat%",10} | " +
            $"{"KarAl",10} | " +
            $"{"ZararKes",10} | " +
            $"{"IzStop",10} | " +
            $"{"Islem",6} | " +
            $"{"Alis",6} | " +
            $"{"Satis",6} | " +
            $"{"Flat",6} | " +
            $"{"Pass",6} | " +
            $"{"Kontrat",8} | " +
            $"{"VarAded",8} | " +
            $"{"VarAdedM",9} | " +
            $"{"SnVarAd",8} | " +
            $"{"SnVarAdM",9} | " +
            $"{"KomVAded",9} | " +
            $"{"KomVAdM",9} | " +
            $"{"KomIslem",9} | " +
            $"{"KomFiyat",10} | " +
            $"{"KarBar",7} | " +
            $"{"ZarBar",7} | " +
            $"{"BakPuan",12} | " +
            $"{"BakFiyat",12} | " +
            $"{"GetPuan",12} | " +
            $"{"GetFiyat",12} | " +
            $"{"GetPuan%",10} | " +
            $"{"GetFiyat%",10} | " +
            $"{"BakPuanN",12} | " +
            $"{"BakFiyatN",12} | " +
            $"{"GetPuanN",12} | " +
            $"{"GetFiyatN",12} | " +
            $"{"GetPuan%N",10} | " +
            $"{"GetFiyat%N",10} | " +
            $"{"EmirKmt",7} | " +
            $"{"EmirSts",7}"
        );
        writer.WriteLine("".PadRight(500, '-'));

        for (int i = 0; i < trader.Data.Count; i++)
        {
            var bar = trader.Data[i];
            writer.WriteLine(
                $"{i,7} | " +
                $"{bar.Date:yyyy.MM.dd} | " +
                $"{bar.DateTime:HH:mm:ss} | " +
                $"{bar.Open,10:F2} | " +
                $"{bar.High,10:F2} | " +
                $"{bar.Low,10:F2} | " +
                $"{bar.Close,10:F2} | " +
                $"{bar.Volume,10:F0} | " +
                $"{trader.lists.YonList[i],3} | " +
                $"{trader.lists.SeviyeList[i],10:F2} | " +
                $"{trader.lists.SinyalList[i],6:F1} | " +
                $"{trader.lists.IsTradeEnabledList[i],7} | " +
                $"{trader.lists.IsPozKapatEnabledList[i],9} | " +
                $"{trader.lists.KarZararPuanList[i],10:F2} | " +
                $"{trader.lists.KarZararFiyatList[i],10:F2} | " +
                $"{trader.lists.KarZararPuanYuzdeList[i],10:F2} | " +
                $"{trader.lists.KarZararFiyatYuzdeList[i],10:F2} | " +
                $"{trader.lists.KarAlList[i],10:F2} | " +
                $"{trader.lists.ZararKesList[i],10:F2} | " +
                $"{trader.lists.IzleyenStopList[i],10:F2} | " +
                $"{trader.lists.IslemSayisiList[i],6} | " +
                $"{trader.lists.AlisSayisiList[i],6} | " +
                $"{trader.lists.SatisSayisiList[i],6} | " +
                $"{trader.lists.FlatSayisiList[i],6} | " +
                $"{trader.lists.PassSayisiList[i],6} | " +
                $"{trader.lists.KontratSayisiList[i],8:F2} | " +
                $"{trader.lists.VarlikAdedSayisiList[i],8:F2} | " +
                $"{trader.lists.VarlikAdedSayisiMicroList[i],9:F4} | " +
                $"{trader.lists.SonVarlikAdedSayisiList[i],8:F2} | " +
                $"{trader.lists.SonVarlikAdedSayisiMicroList[i],9:F4} | " +
                $"{trader.lists.KomisyonVarlikAdedSayisiList[i],9:F2} | " +
                $"{trader.lists.KomisyonVarlikAdedSayisiMicroList[i],9:F4} | " +
                $"{trader.lists.KomisyonIslemSayisiList[i],9} | " +
                $"{trader.lists.KomisyonFiyatList[i],10:F2} | " +
                $"{trader.lists.KardaBarSayisiList[i],7} | " +
                $"{trader.lists.ZarardaBarSayisiList[i],7} | " +
                $"{trader.lists.BakiyePuanList[i],12:F2} | " +
                $"{trader.lists.BakiyeFiyatList[i],12:F2} | " +
                $"{trader.lists.GetiriPuanList[i],12:F2} | " +
                $"{trader.lists.GetiriFiyatList[i],12:F2} | " +
                $"{trader.lists.GetiriPuanYuzdeList[i],10:F2} | " +
                $"{trader.lists.GetiriFiyatYuzdeList[i],10:F2} | " +
                $"{trader.lists.BakiyePuanNetList[i],12:F2} | " +
                $"{trader.lists.BakiyeFiyatNetList[i],12:F2} | " +
                $"{trader.lists.GetiriPuanNetList[i],12:F2} | " +
                $"{trader.lists.GetiriFiyatNetList[i],12:F2} | " +
                $"{trader.lists.GetiriPuanYuzdeNetList[i],10:F2} | " +
                $"{trader.lists.GetiriFiyatYuzdeNetList[i],10:F2} | " +
                $"{trader.lists.EmirKomutList[i],7:F0} | " +
                $"{trader.lists.EmirStatusList[i],7:F0}"
            );
        }

        writer.WriteLine("".PadRight(500, '='));
    }

    public void SaveListsToTxtFromConfig(string filePath, string configPath = "inputs/StatisticsExporterConfig.json")
    {
        var trader = _statistics.TraderForExport;
        if (trader == null || trader.Data == null || trader.Data.Count == 0)
            return;

        var resolvedConfigPath = ResolveConfigPath(configPath);
        if (resolvedConfigPath == null)
            throw new FileNotFoundException("Statistics exporter config not found.", configPath);

        var json = File.ReadAllText(resolvedConfigPath);
        var cfg = JsonSerializer.Deserialize<StatisticsExporterConfig>(json);
        if (cfg?.listOutputs?.full?.columns == null || cfg.listOutputs.full.columns.Count == 0)
            throw new InvalidOperationException("Statistics exporter config does not contain any columns.");

        var columns = cfg.listOutputs.full.columns.FindAll(c => c.enabled);
        if (columns.Count == 0)
            throw new InvalidOperationException("Statistics exporter config has no enabled columns.");

        using StreamWriter writer = CreateSharedWriter(filePath);
        writer.WriteLine($"BAR-BY-BAR TRADING DATA (ALL) - {_statistics.SistemName} ({_statistics.GrafikSembol})");
        writer.WriteLine($"Generated: {DateTime.Now:yyyy.MM.dd HH:mm:ss}");
        writer.WriteLine("".PadRight(700, '='));

        var headerCells = new List<string>(columns.Count);
        foreach (var col in columns)
        {
            int width = col.width > 0 ? col.width : 10;
            headerCells.Add((col.header ?? col.field ?? "").PadLeft(width));
        }
        writer.WriteLine(string.Join(" | ", headerCells));
        writer.WriteLine("".PadRight(700, '-'));

        for (int i = 0; i < trader.Data.Count; i++)
        {
            var rowCells = new List<string>(columns.Count);
            foreach (var col in columns)
            {
                int width = col.width > 0 ? col.width : 10;
                string value = GetColumnValueByField(i, col.field ?? "");
                bool leftAlign = IsLeftAlignedField(col.field ?? "");
                rowCells.Add(leftAlign ? value.PadRight(width) : value.PadLeft(width));
            }
            writer.WriteLine(string.Join(" | ", rowCells));
        }

        writer.WriteLine("".PadRight(500, '='));
    }

    public void SaveListsToCsvFromConfig(string filePath, string configPath = "inputs/StatisticsExporterConfig.json")
    {
        var trader = _statistics.TraderForExport;
        if (trader == null || trader.Data == null || trader.Data.Count == 0)
            return;

        var resolvedConfigPath = ResolveConfigPath(configPath);
        if (resolvedConfigPath == null)
            throw new FileNotFoundException("Statistics exporter config not found.", configPath);

        var json = File.ReadAllText(resolvedConfigPath);
        var cfg = JsonSerializer.Deserialize<StatisticsExporterConfig>(json);
        if (cfg?.listOutputs?.full?.columns == null || cfg.listOutputs.full.columns.Count == 0)
            throw new InvalidOperationException("Statistics exporter config does not contain any columns.");

        var columns = cfg.listOutputs.full.columns.FindAll(c => c.enabled);
        if (columns.Count == 0)
            throw new InvalidOperationException("Statistics exporter config has no enabled columns.");

        using StreamWriter writer = CreateSharedWriter(filePath);
        writer.WriteLine(string.Join(";", columns.ConvertAll(c => c.header ?? c.field ?? "")));

        for (int i = 0; i < trader.Data.Count; i++)
        {
            var values = new List<string>(columns.Count);
            foreach (var col in columns)
            {
                values.Add(GetColumnValueByField(i, col.field ?? ""));
            }
            writer.WriteLine(string.Join(";", values));
        }
    }

    public void SaveListsToCsv(string filePath)
    {
        var trader = _statistics.TraderForExport;
        if (trader == null || trader.Data == null || trader.Data.Count == 0)
            return;

        using StreamWriter writer = CreateSharedWriter(filePath);
        writer.WriteLine(
            "BarNo;Date;Time;Open;High;Low;Close;Volume;" +
            "Yon;Seviye;Sinyal;" +
            "KarZararPuan;KarZararFiyat;KarZararPuanYuzde;KarZararFiyatYuzde;" +
            "KarAl;ZararKes;IzleyenStop;" +
            "IslemSayisi;AlisSayisi;SatisSayisi;FlatSayisi;PassSayisi;" +
            "KontratSayisi;VarlikAdedSayisi;VarlikAdedSayisiMicro;SonVarlikAdedSayisi;SonVarlikAdedSayisiMicro;KomisyonVarlikAdedSayisi;KomisyonVarlikAdedSayisiMicro;KomisyonIslemSayisi;KomisyonFiyat;" +
            "KardaBarSayisi;ZarardaBarSayisi;" +
            "BakiyePuan;BakiyeFiyat;GetiriPuan;GetiriFiyat;GetiriPuanYuzde;GetiriFiyatYuzde;" +
            "BakiyePuanNet;BakiyeFiyatNet;GetiriPuanNet;GetiriFiyatNet;GetiriPuanYuzdeNet;GetiriFiyatYuzdeNet;" +
            "EmirKomut;EmirStatus;" +
            "IsTradeEnabled;IsPozKapatEnabled"
        );

        for (int i = 0; i < trader.Data.Count; i++)
        {
            var bar = trader.Data[i];
            writer.WriteLine(
                $"{i};" +
                $"{bar.Date:yyyy.MM.dd};" +
                $"{bar.DateTime:HH:mm:ss};" +
                $"{bar.Open:F2};" +
                $"{bar.High:F2};" +
                $"{bar.Low:F2};" +
                $"{bar.Close:F2};" +
                $"{bar.Volume:F0};" +
                $"{trader.lists.YonList[i]};" +
                $"{trader.lists.SeviyeList[i]:F2};" +
                $"{trader.lists.SinyalList[i]:F1};" +
                $"{trader.lists.KarZararPuanList[i]:F2};" +
                $"{trader.lists.KarZararFiyatList[i]:F2};" +
                $"{trader.lists.KarZararPuanYuzdeList[i]:F2};" +
                $"{trader.lists.KarZararFiyatYuzdeList[i]:F2};" +
                $"{trader.lists.KarAlList[i]:F2};" +
                $"{trader.lists.ZararKesList[i]:F2};" +
                $"{trader.lists.IzleyenStopList[i]:F2};" +
                $"{trader.lists.IslemSayisiList[i]};" +
                $"{trader.lists.AlisSayisiList[i]};" +
                $"{trader.lists.SatisSayisiList[i]};" +
                $"{trader.lists.FlatSayisiList[i]};" +
                $"{trader.lists.PassSayisiList[i]};" +
                $"{trader.lists.KontratSayisiList[i]:F2};" +
                $"{trader.lists.VarlikAdedSayisiList[i]:F2};" +
                $"{trader.lists.VarlikAdedSayisiMicroList[i]:F4};" +
                $"{trader.lists.SonVarlikAdedSayisiList[i]:F2};" +
                $"{trader.lists.SonVarlikAdedSayisiMicroList[i]:F4};" +
                $"{trader.lists.KomisyonVarlikAdedSayisiList[i]:F2};" +
                $"{trader.lists.KomisyonVarlikAdedSayisiMicroList[i]:F4};" +
                $"{trader.lists.KomisyonIslemSayisiList[i]};" +
                $"{trader.lists.KomisyonFiyatList[i]:F2};" +
                $"{trader.lists.KardaBarSayisiList[i]};" +
                $"{trader.lists.ZarardaBarSayisiList[i]};" +
                $"{trader.lists.BakiyePuanList[i]:F2};" +
                $"{trader.lists.BakiyeFiyatList[i]:F2};" +
                $"{trader.lists.GetiriPuanList[i]:F2};" +
                $"{trader.lists.GetiriFiyatList[i]:F2};" +
                $"{trader.lists.GetiriPuanYuzdeList[i]:F2};" +
                $"{trader.lists.GetiriFiyatYuzdeList[i]:F2};" +
                $"{trader.lists.BakiyePuanNetList[i]:F2};" +
                $"{trader.lists.BakiyeFiyatNetList[i]:F2};" +
                $"{trader.lists.GetiriPuanNetList[i]:F2};" +
                $"{trader.lists.GetiriFiyatNetList[i]:F2};" +
                $"{trader.lists.GetiriPuanYuzdeNetList[i]:F2};" +
                $"{trader.lists.GetiriFiyatYuzdeNetList[i]:F2};" +
                $"{trader.lists.EmirKomutList[i]:F0};" +
                $"{trader.lists.EmirStatusList[i]:F0};" +
                $"{trader.lists.IsTradeEnabledList[i]};" +
                $"{trader.lists.IsPozKapatEnabledList[i]}"
            );
        }
    }

    public void SaveToTxtMinimal(string filePath)
    {
        _statistics.AssignToMapMinimalForExport();
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"TRADING STATISTICS REPORT (MINIMAL) - {_statistics.SistemName} ({_statistics.GrafikSembol})");
        sb.AppendLine($"Generated: {DateTime.Now:yyyy.MM.dd HH:mm:ss}");
        sb.AppendLine("================================================================================");
        sb.AppendLine($"{"Property Name".PadRight(40)} : Value");
        sb.AppendLine("--------------------------------------------------------------------------------");

        foreach (var kvp in _statistics.StatisticsMapMinimal)
        {
            if (kvp.Key.StartsWith(Separator))
                sb.AppendLine();
            else
                sb.AppendLine($"{kvp.Key.PadRight(40)} : {kvp.Value}");
        }

        sb.AppendLine("================================================================================");
        WriteAllTextShared(filePath, sb.ToString());
    }

    public void SaveToCsvMinimal(string filePath)
    {
        _statistics.AssignToMapMinimalForExport();
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("Key;Value");

        foreach (var kvp in _statistics.StatisticsMapMinimal)
        {
            if (!kvp.Key.StartsWith(Separator))
                sb.AppendLine($"{kvp.Key};{kvp.Value}");
        }

        WriteAllTextShared(filePath, sb.ToString());
    }

    public void SaveListsToTxtMinimal(string filePath)
    {
        var trader = _statistics.TraderForExport;
        if (trader == null || trader.Data == null || trader.Data.Count == 0)
            return;

        using StreamWriter writer = CreateSharedWriter(filePath);
        writer.WriteLine($"BAR-BY-BAR TRADING DATA - {_statistics.SistemName} ({_statistics.GrafikSembol})");
        writer.WriteLine($"Generated: {DateTime.Now:yyyy.MM.dd HH:mm:ss}");
        writer.WriteLine("".PadRight(200, '='));

        writer.WriteLine(
            $"{"BarNo",7} | " +
            $"{"Date",10} | " +
            $"{"Time",8} | " +
            $"{"Open",10} | " +
            $"{"High",10} | " +
            $"{"Low",10} | " +
            $"{"Close",10} | " +
            $"{"Volume",10} | " +
            $"{"Yon",3} | " +
            $"{"Seviye",10} | " +
            $"{"Sinyal",6} | " +
            $"{"KarZarar",10} | " +
            $"{"Bakiye",12} | " +
            $"{"Getiri",12} | " +
            $"{"Komisyon",10} | " +
            $"{"BakiyeNet",12} | " +
            $"{"GetiriNet",12} | " +
            $"{"IslemSay",8} | " +
            $"{"EmirKmt",7} | " +
            $"{"EmirSts",7} | " +
            $"{"TrdEnbl",7} | " +
            $"{"PozKpEnbl",9}"
        );
        writer.WriteLine("".PadRight(200, '-'));

        for (int i = 0; i < trader.Data.Count; i++)
        {
            var bar = trader.Data[i];
            writer.WriteLine(
                $"{i,7} | " +
                $"{bar.Date:yyyy.MM.dd} | " +
                $"{bar.DateTime:HH:mm:ss} | " +
                $"{bar.Open,10:F2} | " +
                $"{bar.High,10:F2} | " +
                $"{bar.Low,10:F2} | " +
                $"{bar.Close,10:F2} | " +
                $"{bar.Volume,10:F0} | " +
                $"{trader.lists.YonList[i],3} | " +
                $"{trader.lists.SeviyeList[i],10:F2} | " +
                $"{trader.lists.SinyalList[i],6:F1} | " +
                $"{trader.lists.KarZararFiyatList[i],10:F2} | " +
                $"{trader.lists.BakiyeFiyatList[i],12:F2} | " +
                $"{trader.lists.GetiriFiyatList[i],12:F2} | " +
                $"{trader.lists.KomisyonFiyatList[i],10:F2} | " +
                $"{trader.lists.BakiyeFiyatNetList[i],12:F2} | " +
                $"{trader.lists.GetiriFiyatNetList[i],12:F2} | " +
                $"{trader.lists.IslemSayisiList[i],8} | " +
                $"{trader.lists.EmirKomutList[i],7} | " +
                $"{trader.lists.EmirStatusList[i],7} | " +
                $"{trader.lists.IsTradeEnabledList[i],7} | " +
                $"{trader.lists.IsPozKapatEnabledList[i],9}"
            );
        }

        writer.WriteLine("".PadRight(200, '='));
    }

    public void SaveListsToCsvMinimal(string filePath)
    {
        var trader = _statistics.TraderForExport;
        if (trader == null || trader.Data == null || trader.Data.Count == 0)
            return;

        using StreamWriter writer = CreateSharedWriter(filePath);
        writer.WriteLine(
            "BarNo;Date;Time;Open;High;Low;Close;Volume;" +
            "Yon;Seviye;Sinyal;" +
            "KarZarar;Bakiye;Getiri;Komisyon;BakiyeNet;GetiriNet;" +
            "IslemSayisi;EmirKomut;EmirStatus;" +
            "IsTradeEnabled;IsPozKapatEnabled"
        );

        for (int i = 0; i < trader.Data.Count; i++)
        {
            var bar = trader.Data[i];
            writer.WriteLine(
                $"{i};" +
                $"{bar.Date:yyyy.MM.dd};" +
                $"{bar.DateTime:HH:mm:ss};" +
                $"{bar.Open:F2};" +
                $"{bar.High:F2};" +
                $"{bar.Low:F2};" +
                $"{bar.Close:F2};" +
                $"{bar.Volume:F0};" +
                $"{trader.lists.YonList[i]};" +
                $"{trader.lists.SeviyeList[i]:F2};" +
                $"{trader.lists.SinyalList[i]:F1};" +
                $"{trader.lists.KarZararFiyatList[i]:F2};" +
                $"{trader.lists.BakiyeFiyatList[i]:F2};" +
                $"{trader.lists.GetiriFiyatList[i]:F2};" +
                $"{trader.lists.KomisyonFiyatList[i]:F2};" +
                $"{trader.lists.BakiyeFiyatNetList[i]:F2};" +
                $"{trader.lists.GetiriFiyatNetList[i]:F2};" +
                $"{trader.lists.IslemSayisiList[i]};" +
                $"{trader.lists.EmirKomutList[i]:F0};" +
                $"{trader.lists.EmirStatusList[i]:F0};" +
                $"{trader.lists.IsTradeEnabledList[i]};" +
                $"{trader.lists.IsPozKapatEnabledList[i]}"
            );
        }
    }

    public void SaveToTxtFormatted(string filePath)
    {
        _statistics.AssignToMapForExport();
        var sb = new StringBuilder();
        sb.AppendLine("================================================================================");
        sb.AppendLine("                    SINGLE TRADER RUN RESULTS - DETAILED REPORT");
        sb.AppendLine($"                    Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine("================================================================================");
        sb.AppendLine();

        foreach (var kvp in _statistics.StatisticsMap)
        {
            if (kvp.Key.StartsWith(Separator))
            {
                sb.AppendLine();
                continue;
            }

            sb.AppendLine($"{kvp.Key.PadRight(40)} : {kvp.Value}");
        }

        sb.AppendLine();
        sb.AppendLine("================================================================================");
        sb.AppendLine("                              END OF REPORT");
        sb.AppendLine("================================================================================");
        WriteAllTextShared(filePath, sb.ToString());
    }

    public void SaveToTxtMinimalFormatted(string filePath)
    {
        _statistics.AssignToMapMinimalForExport();
        var sb = new StringBuilder();
        sb.AppendLine("================================================================================");
        sb.AppendLine("                  SINGLE TRADER RUN RESULTS - MINIMAL REPORT");
        sb.AppendLine($"                    Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine("================================================================================");
        sb.AppendLine();

        foreach (var kvp in _statistics.StatisticsMapMinimal)
        {
            if (kvp.Key.StartsWith(Separator))
            {
                sb.AppendLine();
                continue;
            }

            sb.AppendLine($"{kvp.Key.PadRight(40)} : {kvp.Value}");
        }

        sb.AppendLine();
        sb.AppendLine("================================================================================");
        sb.AppendLine("                              END OF REPORT");
        sb.AppendLine("================================================================================");
        WriteAllTextShared(filePath, sb.ToString());
    }

    public void AppendToOptimizationCsv(string filePath, bool writeHeader = false, bool useMinimal = false)
    {
        if (useMinimal)
        {
            var summary = _statistics.GetOptimizationSummaryMinimal();
            if (writeHeader)
            {
                WriteAllTextShared(filePath, StatisticsModel.OptimizationSummaryMinimal.GetCsvHeader() + Environment.NewLine);
            }

            AppendAllTextShared(filePath, summary.ToCsvRow() + Environment.NewLine);
            return;
        }

        var fullSummary = _statistics.GetOptimizationSummary();
        if (writeHeader)
        {
            WriteAllTextShared(filePath, StatisticsModel.OptimizationSummary.GetCsvHeader() + Environment.NewLine);
        }

        AppendAllTextShared(filePath, fullSummary.ToCsvRow() + Environment.NewLine);
    }

    public void AppendToOptimizationTxt(string filePath, bool writeHeader = false, bool useMinimal = false)
    {
        if (useMinimal)
        {
            var summary = _statistics.GetOptimizationSummaryMinimal();
            if (writeHeader)
            {
                var sb = new StringBuilder();
                sb.AppendLine($"OPTIMIZATION RESULTS - {DateTime.Now:yyyy.MM.dd HH:mm:ss}");
                sb.AppendLine(StatisticsModel.OptimizationSummaryMinimal.GetTxtSeparator());
                sb.AppendLine(StatisticsModel.OptimizationSummaryMinimal.GetTxtHeader());
                sb.AppendLine(StatisticsModel.OptimizationSummaryMinimal.GetTxtSeparator());
                WriteAllTextShared(filePath, sb.ToString());
            }

            AppendAllTextShared(filePath, summary.ToTxtRow() + Environment.NewLine);
            return;
        }

        var fullSummary = _statistics.GetOptimizationSummary();
        if (writeHeader)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"OPTIMIZATION RESULTS - {DateTime.Now:yyyy.MM.dd HH:mm:ss}");
            sb.AppendLine(StatisticsModel.OptimizationSummary.GetTxtSeparator());
            sb.AppendLine(StatisticsModel.OptimizationSummary.GetTxtHeader());
            sb.AppendLine(StatisticsModel.OptimizationSummary.GetTxtSeparator());
            WriteAllTextShared(filePath, sb.ToString());
        }

        AppendAllTextShared(filePath, fullSummary.ToTxtRow() + Environment.NewLine);
    }

    private static void WriteAllTextShared(string filePath, string content)
    {
        using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
        using var writer = new StreamWriter(fs, Encoding.UTF8);
        writer.Write(content);
    }

    private static StreamWriter CreateSharedWriter(string filePath)
    {
        var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
        return new StreamWriter(fs, Encoding.UTF8);
    }

    private static void AppendAllTextShared(string filePath, string content)
    {
        using var fs = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        using var writer = new StreamWriter(fs, Encoding.UTF8);
        writer.Write(content);
    }

    private static string? ResolveConfigPath(string configPath)
    {
        if (Path.IsPathRooted(configPath) && File.Exists(configPath))
            return configPath;

        // 1) Relative to current working directory
        var cwdCandidate = Path.GetFullPath(configPath, Directory.GetCurrentDirectory());
        if (File.Exists(cwdCandidate))
            return cwdCandidate;

        // 2) Relative to app base directory
        var baseDir = AppContext.BaseDirectory;
        var baseCandidate = Path.GetFullPath(configPath, baseDir);
        if (File.Exists(baseCandidate))
            return baseCandidate;

        // 3) Walk up from cwd and app base to find inputs/StatisticsExporterConfig.json
        foreach (var start in new[] { Directory.GetCurrentDirectory(), baseDir })
        {
            var dir = new DirectoryInfo(start);
            for (int i = 0; i < 8 && dir != null; i++, dir = dir.Parent)
            {
                var probe = Path.Combine(dir.FullName, "inputs", "StatisticsExporterConfig.json");
                if (File.Exists(probe))
                    return probe;
            }
        }

        return null;
    }

    private static bool IsLeftAlignedField(string field)
    {
        return field == "Date" || field == "Time" || field == "YonList";
    }

    private string GetColumnValueByField(int i, string field)
    {
        var trader = _statistics.TraderForExport!;
        var bar = trader.Data[i];

        return field switch
        {
            "BarNo" => i.ToString(),
            "Date" => bar.Date.ToString("yyyy.MM.dd"),
            "Time" => bar.DateTime.ToString("HH:mm:ss"),
            "Open" => bar.Open.ToString("F2"),
            "High" => bar.High.ToString("F2"),
            "Low" => bar.Low.ToString("F2"),
            "Close" => bar.Close.ToString("F2"),
            "Volume" => bar.Volume.ToString("F0"),
            "Size" => bar.Size.ToString("F0"),
            "Diff" => bar.Diff.ToString("F2"),
            "ChangePct" => bar.ChangePct.ToString("F2"),

            "YonList" => trader.lists.YonList[i],
            "SeviyeList" => trader.lists.SeviyeList[i].ToString("F2"),
            "SinyalList" => trader.lists.SinyalList[i].ToString("F1"),

            "IsTradeEnabledList" => trader.lists.IsTradeEnabledList[i].ToString(),
            "IsPozKapatEnabledList" => trader.lists.IsPozKapatEnabledList[i].ToString(),

            "KarZararPuanList" => trader.lists.KarZararPuanList[i].ToString("F2"),
            "KarZararFiyatList" => trader.lists.KarZararFiyatList[i].ToString("F2"),
            "KarZararPuanYuzdeList" => trader.lists.KarZararPuanYuzdeList[i].ToString("F2"),
            "KarZararFiyatYuzdeList" => trader.lists.KarZararFiyatYuzdeList[i].ToString("F2"),

            "KarAlList" => trader.lists.KarAlList[i].ToString("F2"),
            "ZararKesList" => trader.lists.ZararKesList[i].ToString("F2"),
            "IzleyenStopList" => trader.lists.IzleyenStopList[i].ToString("F2"),

            "IslemSayisiList" => trader.lists.IslemSayisiList[i].ToString(),
            "AlisSayisiList" => trader.lists.AlisSayisiList[i].ToString(),
            "SatisSayisiList" => trader.lists.SatisSayisiList[i].ToString(),
            "FlatSayisiList" => trader.lists.FlatSayisiList[i].ToString(),
            "PassSayisiList" => trader.lists.PassSayisiList[i].ToString(),

            "KontratSayisiList" => trader.lists.KontratSayisiList[i].ToString("F2"),
            "VarlikAdedSayisiList" => trader.lists.VarlikAdedSayisiList[i].ToString("F2"),
            "VarlikAdedSayisiMicroList" => trader.lists.VarlikAdedSayisiMicroList[i].ToString("F4"),
            "SonVarlikAdedSayisiList" => trader.lists.SonVarlikAdedSayisiList[i].ToString("F2"),
            "SonVarlikAdedSayisiMicroList" => trader.lists.SonVarlikAdedSayisiMicroList[i].ToString("F4"),

            "KomisyonVarlikAdedSayisiList" => trader.lists.KomisyonVarlikAdedSayisiList[i].ToString("F2"),
            "KomisyonVarlikAdedSayisiMicroList" => trader.lists.KomisyonVarlikAdedSayisiMicroList[i].ToString("F4"),
            "KomisyonIslemSayisiList" => trader.lists.KomisyonIslemSayisiList[i].ToString(),
            "KomisyonFiyatList" => trader.lists.KomisyonFiyatList[i].ToString("F2"),

            "KardaBarSayisiList" => trader.lists.KardaBarSayisiList[i].ToString(),
            "ZarardaBarSayisiList" => trader.lists.ZarardaBarSayisiList[i].ToString(),

            "BakiyePuanList" => trader.lists.BakiyePuanList[i].ToString("F2"),
            "BakiyeFiyatList" => trader.lists.BakiyeFiyatList[i].ToString("F2"),

            "GetiriPuanList" => trader.lists.GetiriPuanList[i].ToString("F2"),
            "GetiriFiyatList" => trader.lists.GetiriFiyatList[i].ToString("F2"),
            "GetiriPuanYuzdeList" => trader.lists.GetiriPuanYuzdeList[i].ToString("F2"),
            "GetiriFiyatYuzdeList" => trader.lists.GetiriFiyatYuzdeList[i].ToString("F2"),

            "BakiyePuanNetList" => trader.lists.BakiyePuanNetList[i].ToString("F2"),
            "BakiyeFiyatNetList" => trader.lists.BakiyeFiyatNetList[i].ToString("F2"),

            "GetiriPuanNetList" => trader.lists.GetiriPuanNetList[i].ToString("F2"),
            "GetiriFiyatNetList" => trader.lists.GetiriFiyatNetList[i].ToString("F2"),
            "GetiriPuanYuzdeNetList" => trader.lists.GetiriPuanYuzdeNetList[i].ToString("F2"),
            "GetiriFiyatYuzdeNetList" => trader.lists.GetiriFiyatYuzdeNetList[i].ToString("F2"),

            "EmirKomutList" => trader.lists.EmirKomutList[i].ToString("F0"),
            "EmirStatusList" => trader.lists.EmirStatusList[i].ToString("F0"),
            _ => ""
        };
    }

    private sealed class StatisticsExporterConfig
    {
        public ListOutputs? listOutputs { get; set; }
    }

    private sealed class ListOutputs
    {
        public FullOutput? full { get; set; }
    }

    private sealed class FullOutput
    {
        public List<ColumnConfig> columns { get; set; } = new();
    }

    private sealed class ColumnConfig
    {
        public string? field { get; set; }
        public string? header { get; set; }
        public int width { get; set; }
        public bool enabled { get; set; } = true;
    }
}
