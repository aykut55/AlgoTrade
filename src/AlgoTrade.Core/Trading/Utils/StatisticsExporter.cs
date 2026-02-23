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
        sb.AppendLine("================================================================================");
        sb.AppendLine($"                    SINGLE TRADER STATISTICS REPORT - ({_statistics.SistemName}-{_statistics.GrafikSembol})");
        sb.AppendLine($"                    Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine("================================================================================");
        sb.AppendLine();

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
        var columns = cfg?.GetEnabledColumns();
        if (columns == null || columns.Count == 0)
            throw new InvalidOperationException("Statistics exporter config has no enabled columns.");

        using StreamWriter writer = CreateSharedWriter(filePath);
        writer.WriteLine($"BAR-BY-BAR TRADING DATA (ALL) - {_statistics.SistemName} ({_statistics.GrafikSembol})");
        writer.WriteLine($"Generated: {DateTime.Now:yyyy.MM.dd HH:mm:ss}");
        writer.WriteLine("".PadRight(850, '='));

        var headerCells = new List<string>(columns.Count);
        foreach (var col in columns)
        {
            int width = col.width > 0 ? col.width : 10;
            headerCells.Add((col.header ?? col.field ?? "").PadLeft(width));
        }
        writer.WriteLine(string.Join(" | ", headerCells));
        writer.WriteLine("".PadRight(850, '-'));

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
        var columns = cfg?.GetEnabledColumns();
        if (columns == null || columns.Count == 0)
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

    public void SavePerformansToTxtFromConfig(string filePath, string configPath = "inputs/StatisticsExporterConfig.json", string profile = "Full")
    {
        var trader = _statistics.TraderForExport;
        if (trader == null || trader.Data == null || trader.Data.Count == 0)
            return;

        var resolvedConfigPath = ResolveConfigPath(configPath);
        if (resolvedConfigPath == null)
            throw new FileNotFoundException("Statistics exporter config not found.", configPath);

        var json = File.ReadAllText(resolvedConfigPath);
        var cfg = JsonSerializer.Deserialize<StatisticsExporterConfig>(json);
        var columns = cfg?.GetEnabledColumnsFromPerformans(profile);
        if (columns == null || columns.Count == 0)
            throw new InvalidOperationException("Performans config has no enabled columns.");

        var rows = _statistics.PerformansRows;

        using StreamWriter writer = CreateSharedWriter(filePath);
        writer.WriteLine($"SINGLE TRADER PERFORMANS (PROFILE: {profile}) - {_statistics.SistemName} ({_statistics.GrafikSembol})");
        writer.WriteLine($"Generated: {DateTime.Now:yyyy.MM.dd HH:mm:ss}");
        writer.WriteLine("".PadRight(200, '='));

        var colWidths = new int[columns.Count];
        var headerCells = new List<string>(columns.Count);
        for (int c = 0; c < columns.Count; c++)
        {
            var col = columns[c];
            int width = col.width > 0 ? col.width : 10;
            string header = col.header ?? col.field ?? "";
            width = Math.Max(width, header.Length);
            colWidths[c] = width;

            bool leftAlign = IsLeftAlignedPerformansField(col.field ?? "");
            headerCells.Add(leftAlign ? header.PadRight(width) : header.PadLeft(width));
        }
        writer.WriteLine(string.Join(" | ", headerCells));
        writer.WriteLine("".PadRight(200, '-'));

        for (int i = 0; i < rows.Count; i++)
        {
            var rowCells = new List<string>(columns.Count);
            for (int c = 0; c < columns.Count; c++)
            {
                var col = columns[c];
                int width = colWidths[c];
                string value = GetPerformansColumnValueByField(rows[i], i, col.field ?? "");
                bool leftAlign = IsLeftAlignedPerformansField(col.field ?? "");
                rowCells.Add(leftAlign ? value.PadRight(width) : value.PadLeft(width));
            }
            writer.WriteLine(string.Join(" | ", rowCells));
        }

        writer.WriteLine("".PadRight(200, '='));
    }

    public void SavePerformansToCsvFromConfig(string filePath, string configPath = "inputs/StatisticsExporterConfig.json", string profile = "Full")
    {
        var trader = _statistics.TraderForExport;
        if (trader == null || trader.Data == null || trader.Data.Count == 0)
            return;

        var resolvedConfigPath = ResolveConfigPath(configPath);
        if (resolvedConfigPath == null)
            throw new FileNotFoundException("Statistics exporter config not found.", configPath);

        var json = File.ReadAllText(resolvedConfigPath);
        var cfg = JsonSerializer.Deserialize<StatisticsExporterConfig>(json);
        var columns = cfg?.GetEnabledColumnsFromPerformans(profile);
        if (columns == null || columns.Count == 0)
            throw new InvalidOperationException("Performans config has no enabled columns.");

        var rows = _statistics.PerformansRows;

        using StreamWriter writer = CreateSharedWriter(filePath);
        writer.WriteLine(string.Join(";", columns.ConvertAll(c => c.header ?? c.field ?? "")));

        for (int i = 0; i < rows.Count; i++)
        {
            var values = new List<string>(columns.Count);
            foreach (var col in columns)
            {
                values.Add(GetPerformansColumnValueByField(rows[i], i, col.field ?? ""));
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
        WriteAllTextShared(filePath, BuildDetailedReport());
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

        var fullMap = _statistics.GetOptimizationSummary();
        if (writeHeader)
            AppendAllTextShared(filePath, string.Join(";", fullMap.Keys) + Environment.NewLine);
        AppendAllTextShared(filePath, string.Join(";", fullMap.Values) + Environment.NewLine);
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

        var fullMap = _statistics.GetOptimizationSummary();
        if (writeHeader)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"OPTIMIZATION RESULTS - {DateTime.Now:yyyy.MM.dd HH:mm:ss}");
            sb.AppendLine("".PadRight(200, '='));
            sb.AppendLine(string.Join(" | ", fullMap.Keys.Select(k => k.PadLeft(15))));
            sb.AppendLine("".PadRight(200, '-'));
            WriteAllTextShared(filePath, sb.ToString());
        }

        AppendAllTextShared(filePath, string.Join(" | ", fullMap.Values.Select(v => v.PadLeft(15))) + Environment.NewLine);
    }

    public static List<(string Field, string Header, int Width)> LoadOptimizationColumns(
        string configPath, string profile = "Full")
    {
        if (!File.Exists(configPath)) return new();
        var json = File.ReadAllText(configPath);
        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var cfg = JsonSerializer.Deserialize<StatisticsExporterConfig>(json, opts);
        var cols = cfg?.GetEnabledOptimizationColumns(profile) ?? new();
        return cols.Select(c => (c.field ?? "", c.header ?? c.field ?? "", c.width)).ToList();
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

    private string BuildDetailedReport()
    {
        string GetValue(string key)
        {
            if (_statistics.StatisticsMap.TryGetValue(key, out var value))
                return value?.ToString() ?? "...";
            return "...";
        }

        const int reportBoxWidth = 78;
        static string CenterText(string text, int width)
        {
            if (text.Length >= width)
                return text[..width];

            var leftPadding = (width - text.Length) / 2;
            return new string(' ', leftPadding) + text.PadRight(width - leftPadding);
        }

        void AddSection(StringBuilder sb, string title, params (string Label, string Key)[] rows)
        {
            var boxWidth = reportBoxWidth;
            const int labelWidth = 24;
            var valueWidth = boxWidth - labelWidth - 6; // "| " + " : " + " |"

            var headerDashCount = Math.Max(0, boxWidth - title.Length - 3); // "┌─ " + title + " ─┐"
            sb.AppendLine($"┌─ {title} {new string('─', headerDashCount)}┐");
            foreach (var row in rows)
            {
                var value = GetValue(row.Key);
                if (value.Length > valueWidth)
                    value = value[..valueWidth];

                sb.AppendLine($"│ {row.Label.PadRight(labelWidth)} : {value.PadRight(valueWidth)} │");
            }
            sb.AppendLine($"└{new string('─', boxWidth)}┘");
            sb.AppendLine();
        }

        var sb = new StringBuilder();
        sb.AppendLine($"┌{new string('─', reportBoxWidth)}┐");
        sb.AppendLine($"│{CenterText("SINGLE TRADER RUN RESULTS - DETAILED REPORT", reportBoxWidth)}│");
        sb.AppendLine($"│{CenterText($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}", reportBoxWidth)}│");
        sb.AppendLine($"└{new string('─', reportBoxWidth)}┘");
        sb.AppendLine();

        AddSection(sb, "TRADER & SYSTEM INFORMATION",
            ("Trader ID", "TraderId"),
            ("Trader Name", "TraderName"),
            ("Symbol Name", "SymbolName"),
            ("Symbol Period", "SymbolPeriod"),
            ("System ID", "SystemId"),
            ("System Name", "SystemName"),
            ("Strategy ID", "StrategyId"),
            ("Strategy Name", "StrategyName")
        );

        AddSection(sb, "EXECUTION INFORMATION",
            ("Last Execution ID", "LastExecutionId"),
            ("Last Execution Time", "LastExecutionTime"),
            ("Execution Start", "LastExecutionTimeStart"),
            ("Execution Stop", "LastExecutionTimeStop"),
            ("Execution Time (ms)", "LastExecutionTimeInMSec"),
            ("Last Reset Time", "LastResetTime"),
            ("Statistics Calc Time", "LastStatisticsCalculationTime")
        );

        AddSection(sb, "BAR INFORMATION",
            ("Total Bars", "ToplamBarSayisi"),
            ("Selected Bar Number", "SecilenBarNumarasi"),
            ("Selected Bar DateTime", "SecilenBarTarihSaati"),
            ("Selected Bar Date", "SecilenBarTarihi"),
            ("Selected Bar Time", "SecilenBarSaati"),
            ("First Bar DateTime", "IlkBarTarihSaati"),
            ("First Bar Date", "IlkBarTarihi"),
            ("First Bar Time", "IlkBarSaati"),
            ("Last Bar DateTime", "SonBarTarihSaati"),
            ("Last Bar Date", "SonBarTarihi"),
            ("Last Bar Time", "SonBarSaati"),
            ("First Bar Index", "IlkBarIndex"),
            ("Last Bar Index", "SonBarIndex"),
            ("Last Bar Open", "SonBarAcilisFiyati"),
            ("Last Bar High", "SonBarYuksekFiyati"),
            ("Last Bar Low", "SonBarDusukFiyati"),
            ("Last Bar Close", "SonBarKapanisFiyati")
        );

        AddSection(sb, "TIME STATISTICS",
            ("Total Months", "ToplamGecenSureAy"),
            ("Total Days", "ToplamGecenSureGun"),
            ("Total Hours", "ToplamGecenSureSaat"),
            ("Total Minutes", "ToplamGecenSureDakika"),
            ("Avg Monthly Trades", "OrtAylikIslemSayisi"),
            ("Avg Weekly Trades", "OrtHaftalikIslemSayisi"),
            ("Avg Daily Trades", "OrtGunlukIslemSayisi"),
            ("Avg Hourly Trades", "OrtSaatlikIslemSayisi")
        );

        AddSection(sb, "BALANCE & RETURNS",
            ("Initial Balance (Price)", "IlkBakiyeFiyat"),
            ("Initial Balance (Points)", "IlkBakiyePuan"),
            ("Final Balance (Price)", "BakiyeFiyat"),
            ("Final Balance (Points)", "BakiyePuan"),
            ("Gross Return (Price)", "GetiriFiyat"),
            ("Gross Return (Points)", "GetiriPuan"),
            ("Gross Return % (Price)", "GetiriFiyatYuzde"),
            ("Gross Return % (Points)", "GetiriPuanYuzde"),
            ("Commission", "KomisyonFiyat"),
            ("Net Balance (Price)", "BakiyeFiyatNet"),
            ("Net Balance (Points)", "BakiyePuanNet"),
            ("Net Return (Price)", "GetiriFiyatNet"),
            ("Net Return (Points)", "GetiriPuanNet"),
            ("Net Return % (Price)", "GetiriFiyatYuzdeNet"),
            ("Net Return % (Points)", "GetiriPuanYuzdeNet"),
            ("Return Kz", "GetiriKz"),
            ("Return Kz Net", "GetiriKzNet"),
            ("Return Kz System", "GetiriKzSistem"),
            ("Return Kz System %", "GetiriKzSistemYuzde"),
            ("Return Kz Net System", "GetiriKzNetSistem"),
            ("Return Kz Net System %", "GetiriKzNetSistemYuzde")
        );

        AddSection(sb, "BALANCE MIN/MAX",
            ("Min Balance (Price)", "MinBakiyeFiyat"),
            ("Max Balance (Price)", "MaxBakiyeFiyat"),
            ("Min Balance (Points)", "MinBakiyePuan"),
            ("Max Balance (Points)", "MaxBakiyePuan"),
            ("Min Balance %", "MinBakiyeFiyatYuzde"),
            ("Max Balance %", "MaxBakiyeFiyatYuzde"),
            ("Min Balance Index", "MinBakiyeFiyatIndex"),
            ("Max Balance Index", "MaxBakiyeFiyatIndex"),
            ("Min Balance Net", "MinBakiyeFiyatNet"),
            ("Max Balance Net", "MaxBakiyeFiyatNet"),
            ("Min Balance Net Index", "MinBakiyeFiyatNetIndex"),
            ("Max Balance Net Index", "MaxBakiyeFiyatNetIndex"),
            ("Min Balance Net %", "MinBakiyeFiyatNetYuzde"),
            ("Max Balance Net %", "MaxBakiyeFiyatNetYuzde")
        );

        AddSection(sb, "TRADE COUNTS",
            ("Total Trades", "IslemSayisi"),
            ("Buy Trades", "AlisSayisi"),
            ("Sell Trades", "SatisSayisi"),
            ("Flat Count", "FlatSayisi"),
            ("Pass Count", "PassSayisi"),
            ("Take Profit Count", "KarAlSayisi"),
            ("Stop Loss Count", "ZararKesSayisi"),
            ("Winning Trades", "KazandiranIslemSayisi"),
            ("Losing Trades", "KaybettirenIslemSayisi"),
            ("Neutral Trades", "NotrIslemSayisi"),
            ("Winning Buys", "KazandiranAlisSayisi"),
            ("Losing Buys", "KaybettirenAlisSayisi"),
            ("Neutral Buys", "NotrAlisSayisi"),
            ("Winning Sells", "KazandiranSatisSayisi"),
            ("Losing Sells", "KaybettirenSatisSayisi"),
            ("Neutral Sells", "NotrSatisSayisi")
        );

        AddSection(sb, "COMMAND COUNTS",
            ("Buy Commands", "AlKomutSayisi"),
            ("Sell Commands", "SatKomutSayisi"),
            ("Pass Commands", "PasGecKomutSayisi"),
            ("Take Profit Commands", "KarAlKomutSayisi"),
            ("Stop Loss Commands", "ZararKesKomutSayisi"),
            ("Flat Commands", "FlatOlKomutSayisi")
        );

        AddSection(sb, "COMMISSION DETAILS",
            ("Commission Trades", "KomisyonIslemSayisi"),
            ("Commission Asset Count", "KomisyonVarlikAdedSayisi"),
            ("Commission Micro", "KomisyonVarlikAdedSayisiMicro"),
            ("Commission Multiplier", "KomisyonCarpan"),
            ("Total Commission", "KomisyonFiyat"),
            ("Commission %", "KomisyonFiyatYuzde"),
            ("Include Commission", "KomisyonuDahilEt")
        );

        AddSection(sb, "PROFIT & LOSS",
            ("P&L (Price)", "KarZararFiyat"),
            ("P&L % (Price)", "KarZararFiyatYuzde"),
            ("P&L (Points)", "KarZararPuan"),
            ("Total Profit (Price)", "ToplamKarFiyat"),
            ("Total Loss (Price)", "ToplamZararFiyat"),
            ("Net Profit (Price)", "NetKarFiyat"),
            ("Total Profit (Points)", "ToplamKarPuan"),
            ("Total Loss (Points)", "ToplamZararPuan"),
            ("Net Profit (Points)", "NetKarPuan"),
            ("Max Profit (Price)", "MaxKarFiyat"),
            ("Max Loss (Price)", "MaxZararFiyat"),
            ("Max Profit (Points)", "MaxKarPuan"),
            ("Max Loss (Points)", "MaxZararPuan"),
            ("Bars in Profit", "KardaBarSayisi"),
            ("Bars in Loss", "ZarardaBarSayisi"),
            ("Win Rate", "KarliIslemOrani")
        );

        AddSection(sb, "RISK METRICS",
            ("Max Drawdown", "GetiriMaxDD"),
            ("Max Drawdown Date", "GetiriMaxDDTarih"),
            ("Max Loss", "GetiriMaxKayip"),
            ("Profit Factor", "ProfitFactor"),
            ("Profit Factor (Net)", "ProfitFactorNet"),
            ("Profit Factor (System)", "ProfitFactorSistem")
        );

        AddSection(sb, "SIGNALS & EXECUTION STATUS",
            ("Signal", "Sinyal"),
            ("Last Direction", "SonYon"),
            ("Previous Direction", "PrevYon"),
            ("Last Price", "SonFiyat"),
            ("Last Buy Price", "SonAFiyat"),
            ("Last Sell Price", "SonSFiyat"),
            ("Last Flat Price", "SonFFiyat"),
            ("Last Pass Price", "SonPFiyat"),
            ("Previous Price", "PrevFiyat"),
            ("Last Bar Number", "SonBarNo"),
            ("Last Buy Bar Number", "SonABarNo"),
            ("Last Sell Bar Number", "SonSBarNo"),
            ("Order Command", "EmirKomut"),
            ("Order Status", "EmirStatus")
        );

        AddSection(sb, "ASSET & POSITION INFO",
            ("Share Count", "HisseSayisi"),
            ("Contract Count", "KontratSayisi"),
            ("Asset Multiplier", "VarlikAdedCarpani"),
            ("Asset Count", "VarlikAdedSayisi"),
            ("Asset Count (Micro)", "VarlikAdedSayisiMicro"),
            ("Slippage Amount", "KaymaMiktari"),
            ("Include Slippage", "KaymayiDahilEt"),
            ("Micro Lot Size Enabled", "MicroLotSizeEnabled"),
            ("Pyramiding Enabled", "PyramidingEnabled"),
            ("Max Position Size Enabled", "MaxPositionSizeEnabled"),
            ("Max Position Size", "MaxPositionSize"),
            ("Max Position Size (Micro)", "MaxPositionSizeMicro")
        );

        AddSection(sb, "PERIODIC RETURNS (PRICE)",
            ("This Month", "GetiriFiyatBuAy"),
            ("Last Month", "GetiriFiyatAy1"),
            ("This Week", "GetiriFiyatBuHafta"),
            ("Last Week", "GetiriFiyatHafta1"),
            ("Today", "GetiriFiyatBuGun"),
            ("Yesterday", "GetiriFiyatGun1"),
            ("This Hour", "GetiriFiyatBuSaat"),
            ("Last Hour", "GetiriFiyatSaat1")
        );

        AddSection(sb, "PERIODIC RETURNS (POINTS)",
            ("This Month (Pts)", "GetiriPuanBuAy"),
            ("Last Month (Pts)", "GetiriPuanAy1"),
            ("This Week (Pts)", "GetiriPuanBuHafta"),
            ("Last Week (Pts)", "GetiriPuanHafta1"),
            ("Today (Pts)", "GetiriPuanBuGun"),
            ("Yesterday (Pts)", "GetiriPuanGun1"),
            ("This Hour (Pts)", "GetiriPuanBuSaat"),
            ("Last Hour (Pts)", "GetiriPuanSaat1")
        );

        sb.AppendLine($"┌{new string('─', reportBoxWidth)}┐");
        sb.AppendLine($"│{CenterText("END OF REPORT", reportBoxWidth)}│");
        sb.AppendLine($"└{new string('─', reportBoxWidth)}┘");
        return sb.ToString();
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

        // 3) Walk up from cwd and app base to find inputs config file (tolerant to common typos)
        foreach (var start in new[] { Directory.GetCurrentDirectory(), baseDir })
        {
            var dir = new DirectoryInfo(start);
            for (int i = 0; i < 8 && dir != null; i++, dir = dir.Parent)
            {
                var inputsDir = Path.Combine(dir.FullName, "inputs");
                var candidates = new[]
                {
                    "StatisticsExporterConfig.json",           // correct
                    "StatstcsExporterConfg.json",              // user-typo variant 1
                    "StatscsExporterConfg.json"                // user-typo variant 2
                };
                foreach (var name in candidates)
                {
                    var probe = Path.Combine(inputsDir, name);
                    if (File.Exists(probe))
                        return probe;
                }
            }
        }

        return null;
    }

    private static bool IsLeftAlignedField(string field)
    {
        return field == "Date" || field == "Time" || field == "YonList";
    }

    private static bool IsLeftAlignedPerformansField(string field)
    {
        return field == "YonList" ||
               field == "AcilisTarihi" ||
               field == "AcilisSaati" ||
               field == "KapanisTarihi" ||
               field == "KapanisSaati";
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

    private string GetPerformansColumnValueByField(StatisticsModel.PerformansRow row, int rowIndex, string field)
    {
        return field switch
        {
            "No" => (rowIndex + 1).ToString(),
            "YonList" => row.Yon,
            "KontratSayisi" => row.KontratSayisi.ToString("F2"),
            "AcilisTarihi" => row.AcilisTarihSaat.ToString("yyyy.MM.dd"),
            "AcilisSaati" => row.AcilisTarihSaat.ToString("HH:mm:ss"),
            "AcilisFiyati" => row.AcilisFiyati.ToString("F2"),
            "KapanisTarihi" => row.KapanisTarihSaat.ToString("yyyy.MM.dd"),
            "KapanisSaati" => row.KapanisTarihSaat.ToString("HH:mm:ss"),
            "KapanisFiyati" => row.KapanisFiyati.ToString("F2"),
            "KarZararPuan" => row.KarZararPuan.ToString("F2"),
            "BakiyePuan" => row.BakiyePuan.ToString("F2"),
            "GetiriPuan" => row.GetiriPuan.ToString("F2"),
            "GetiriPuanYuzde" => row.GetiriPuanYuzde.ToString("F2"),
            _ => ""
        };
    }

    private sealed class StatisticsExporterConfig
    {
        // Backward-compatible root (old structure)
        public ListOutputs? listOutputs { get; set; }

        // New structure root (with tolerant aliases)
        public SingleTraderNode? SingleTrader { get; set; }
        public SingleTraderNode? SngleTrader { get; set; } // alias tolerance
        public SingleTraderNode? ngleTrader { get; set; }  // alias tolerance

        public SingleTraderOptimizationNode? SingleTraderOptimization { get; set; }

        public List<ColumnConfig>? TryGetColumns()
        {
            // Prefer new structure if present
            var st = SingleTrader ?? SngleTrader ?? ngleTrader;
            var lists = st?.SingleTraderLists;
            if (lists == null)
                lists = st?.SngleTraderLsts; // alias tolerance

            // New: Full profile
            var fullCols = lists?.Full?.listOutputs?.full?.columns
                           ?? lists?.Full?.lstOutputs?.full?.columns;
            if (fullCols != null && fullCols.Count > 0)
                return fullCols;

            // New: Minimal profile
            var minimalCols = lists?.Minimal?.listOutputs?.full?.columns
                               ?? lists?.Minimal?.lstOutputs?.full?.columns;
            if (minimalCols != null && minimalCols.Count > 0)
                return minimalCols;

            // Transitional new-old mix: listOutputs directly under SingleTraderLists
            var newOldCols = lists?.listOutputs?.full?.columns
                             ?? lists?.lstOutputs?.full?.columns;
            if (newOldCols != null && newOldCols.Count > 0)
                return newOldCols;

            // If lists not present, check Performans profiles (optional)
            var perf = st?.SingleTraderPerformans ?? st?.SngleTraderPerformans ?? st?.ngleTraderPerformans;
            var perfFull = perf?.Full?.listOutputs?.full?.columns
                           ?? perf?.Full?.lstOutputs?.full?.columns;
            if (perfFull != null && perfFull.Count > 0)
                return perfFull;

            var perfMinimal = perf?.Minimal?.listOutputs?.full?.columns
                               ?? perf?.Minimal?.lstOutputs?.full?.columns;
            if (perfMinimal != null && perfMinimal.Count > 0)
                return perfMinimal;

            // Fallback to old structure
            return listOutputs?.full?.columns;
        }

        public List<ColumnConfig> GetEnabledColumns()
        {
            var cols = TryGetColumns() ?? new List<ColumnConfig>();
            // Normalize alias keys and synonyms before filtering
            NormalizeColumns(cols);
            return cols.FindAll(c => c.enabled);
        }

        public List<ColumnConfig>? TryGetColumnsFromPerformans(string? profile = null)
        {
            var st = SingleTrader ?? SngleTrader ?? ngleTrader;
            var perf = st?.SingleTraderPerformans ?? st?.SngleTraderPerformans ?? st?.ngleTraderPerformans;
            if (perf == null)
                return null;

            TraderListProfile? chosen = null;
            if (!string.IsNullOrWhiteSpace(profile))
            {
                if (string.Equals(profile, "Full", StringComparison.OrdinalIgnoreCase)) chosen = perf.Full;
                else if (string.Equals(profile, "Minimal", StringComparison.OrdinalIgnoreCase)) chosen = perf.Minimal;
            }

            chosen ??= perf.Full ?? perf.Minimal;
            if (chosen == null)
                return null;

            return chosen.listOutputs?.full?.columns
                   ?? chosen.lstOutputs?.full?.columns;
        }

        public List<ColumnConfig> GetEnabledColumnsFromPerformans(string? profile = null)
        {
            var cols = TryGetColumnsFromPerformans(profile) ?? new List<ColumnConfig>();
            NormalizePerformansColumns(cols);
            return cols.FindAll(c => c.enabled);
        }

        public List<ColumnConfig> GetEnabledOptimizationColumns(string profile = "Full")
        {
            var node = SingleTraderOptimization;
            if (node == null) return new List<ColumnConfig>();
            var chosen = string.Equals(profile, "Minimal", StringComparison.OrdinalIgnoreCase)
                ? node.Minimal : node.Full;
            chosen ??= node.Full ?? node.Minimal;

            var fullOutput = chosen?.listOutputs?.full;
            if (fullOutput == null) return new List<ColumnConfig>();

            // optimizationResult önce, sonra optimizationSummary, arkasından columns
            var merged = new List<ColumnConfig>();
            if (fullOutput.optimizationResult != null)
                merged.AddRange(fullOutput.optimizationResult);
            if (fullOutput.optimizationSummary != null)
                merged.AddRange(fullOutput.optimizationSummary);
            merged.AddRange(fullOutput.columns);

            foreach (var c in merged) { if (c.width <= 0) c.width = 10; }
            return merged.FindAll(c => c.enabled);
        }

        private static void NormalizeColumns(List<ColumnConfig> cols)
        {
            foreach (var c in cols)
            {
                // Accept alias keys from config
                if (string.IsNullOrWhiteSpace(c.field) && !string.IsNullOrWhiteSpace(c.feld))
                    c.field = c.feld;
                if ((c.width <= 0) && c.wdth.HasValue && c.wdth.Value > 0)
                    c.width = c.wdth.Value;

                // Map common shorthand field names to internal names
                switch (c.field)
                {
                    case "No":
                        c.field = "BarNo";
                        break;
                    case "YonLst":
                        c.field = "YonList";
                        break;
                    case "KontratSayst":
                        c.field = "KontratSayisiList";
                        break;
                    case "AcilisTarihi":
                        c.field = "Date";
                        break;
                    case "AcilisSaati":
                        c.field = "Time";
                        break;
                    case "AcilisTar":
                        c.field = "Date"; // bar date
                        break;
                    case "AcilisFiyati":
                        c.field = "Open";
                        break;
                    case "KapanisTarihi":
                        c.field = "Date";
                        break;
                    case "KapanisSaati":
                        c.field = "Time";
                        break;
                    case "KapanisTar":
                        c.field = "Date"; // bar date (close date aligns with bar date)
                        break;
                    case "KapanisFiyati":
                        c.field = "Close";
                        break;
                    case "KarZararPuan":
                        c.field = "KarZararPuanList";
                        break;
                    case "KarZarar":
                        c.field = "KarZararFiyatList";
                        break;
                    case "BakiyePuan":
                        c.field = "BakiyePuanList";
                        break;
                    case "BakiyeFiyat":
                        c.field = "BakiyeFiyatList";
                        break;
                    case "BakiyeFiyatNet":
                        c.field = "BakiyeFiyatNetList";
                        break;
                }

                // Default sensible width
                if (c.width <= 0) c.width = 10;
            }
        }

        private static void NormalizePerformansColumns(List<ColumnConfig> cols)
        {
            foreach (var c in cols)
            {
                if (string.IsNullOrWhiteSpace(c.field) && !string.IsNullOrWhiteSpace(c.feld))
                    c.field = c.feld;
                if ((c.width <= 0) && c.wdth.HasValue && c.wdth.Value > 0)
                    c.width = c.wdth.Value;

                switch (c.field)
                {
                    case "YonLst":
                        c.field = "YonList";
                        break;
                    case "KontratSayst":
                        c.field = "KontratSayisi";
                        break;
                    case "AcilisTar":
                        c.field = "AcilisTarihi";
                        break;
                    case "AcilisSaat":
                        c.field = "AcilisSaati";
                        break;
                    case "KapanisTar":
                        c.field = "KapanisTarihi";
                        break;
                    case "KapanisSaat":
                        c.field = "KapanisSaati";
                        break;
                    case "KarZarar":
                        c.field = "KarZararPuan";
                        break;
                }

                if (c.width <= 0) c.width = 10;
            }
        }
    }

    private sealed class SingleTraderNode
    {
        public SingleTraderListsNode? SingleTraderLists { get; set; }
        public SingleTraderListsNode? SngleTraderLsts { get; set; } // alias tolerance
        public SingleTraderPerformansNode? SingleTraderPerformans { get; set; }
        public SingleTraderPerformansNode? SngleTraderPerformans { get; set; } // alias tolerance
        public SingleTraderPerformansNode? ngleTraderPerformans { get; set; } // alias tolerance
    }

    private sealed class SingleTraderListsNode
    {
        public int version { get; set; }
        public string? description { get; set; }
        // Backward compat: old structure had listOutputs directly here
        public ListOutputs? listOutputs { get; set; }

        // New structure: profiles under SingleTraderLists
        public TraderListProfile? Full { get; set; }
        public TraderListProfile? Minimal { get; set; }
        // Alias tolerance
        public ListOutputs? lstOutputs { get; set; }
    }

    private sealed class TraderListProfile
    {
        public int? version { get; set; }
        public int? verson { get; set; } // alias tolerance
        public string? description { get; set; }
        public string? descrpton { get; set; } // alias tolerance
        public ListOutputs? listOutputs { get; set; }
        public ListOutputs? lstOutputs { get; set; } // alias tolerance
    }

    private sealed class SingleTraderPerformansNode
    {
        public int? version { get; set; }
        public int? verson { get; set; } // alias tolerance
        public string? description { get; set; }
        public string? descrpton { get; set; } // alias tolerance
        public TraderListProfile? Full { get; set; }
        public TraderListProfile? Minimal { get; set; }
    }

    private sealed class SingleTraderOptimizationNode
    {
        public int version { get; set; }
        public string? description { get; set; }
        public TraderListProfile? Full    { get; set; }
        public TraderListProfile? Minimal { get; set; }
    }

    private sealed class ListOutputs
    {
        public FullOutput? full { get; set; }
    }

    private sealed class FullOutput
    {
        public List<ColumnConfig> columns { get; set; } = new();
        public List<ColumnConfig>? optimizationSummary { get; set; }
        public List<ColumnConfig>? optimizationResult { get; set; }
    }

    private sealed class ColumnConfig
    {
        public string? field { get; set; }
        public string? feld { get; set; } // alias support
        public string? header { get; set; }
        public int width { get; set; }
        public int? wdth { get; set; } // alias support
        public bool enabled { get; set; } = true;
    }
}
