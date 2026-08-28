// =============================================================================
// paramSweep.csx - Farkli parametrelerle strateji testi (parametre taramasi)
// Kullanim: Menu [4] ile calistirin
// =============================================================================
using System.IO;
using System.Collections.Concurrent;
using AlgoTrade.Core;
using AlgoTrade.Core.StockDataReader;
using AlgoTrade.Core.Trading;
using AlgoTrade.Core.Timer;

// ---- PARAMETRELER (Buradan degistirin) --------------------------------------
string dataFile       = @"C:\data\csvFiles\VIP\01\VIP-X030-T.csv";
string strategyName   = "SimpleMostStrategy";

int[]    periods  = new[] { 10, 15, 21, 30, 50 };
double[] percents = new[] { 0.5, 1.0, 1.5, 2.0 };
string   mostMaMethod   = "EMA";
string   priceSource    = "Close";
int      signalModeIndex = 0;
// -----------------------------------------------------------------------------

Log($"=== paramSweep.csx ===");
Log($"Data file : {dataFile}");
Log($"Strategy  : {strategyName}");
Log($"Periods   : [{string.Join(", ", periods)}]");
Log($"Percents  : [{string.Join(", ", percents)}]");
Log($"Toplam kombinasyon: {periods.Length * percents.Length}");

// 1. Veri oku (bir kez)
if (!File.Exists(dataFile))
{
    Log($"[HATA] Dosya bulunamadi: {dataFile}");
    return;
}

var reader = new StockDataReader();
reader.ReadMetaData(dataFile);

if (!reader.IsMetaDataRead)
{
    Log("[HATA] MetaData okunamadi.");
    return;
}

var meta = reader.GetMetaData();
var sembol = meta.GetValueOrDefault("GrafikSembol", "N/A");
var periyot = meta.GetValueOrDefault("GrafikPeriyot", "N/A");
Log($"Sembol    : {sembol}  Periyot: {periyot}");

reader.ReadDataFast(dataFile);
var data = reader.GetData();
Log($"Okunan    : {data.Count} bar\n");

if (data.Count == 0)
{
    Log("[HATA] Data bos.");
    return;
}

// RunSingleTraderWithProgressAsync() AlgoTrader'in kendi ic _timer/_logger
// alanlarini kullaniyor - console'daki her menu bunlari RegisterLogger/
// RegisterTimer ile dolduruyor, script'te de gerekiyor (algoTrader.Reset()
// bunlari sifirlamiyor, tek seferlik kayit yeterli).
algoTrader.RegisterLogger(LogManager.GetInstance());
algoTrader.RegisterTimer(TimeManager.GetInstance());

// 2. Parametre taramasi
var results = new List<(int period, double percent, string ozet)>();
int runNo = 0;
int totalRuns = periods.Length * percents.Length;

foreach (var p in periods)
{
    foreach (var pct in percents)
    {
        runNo++;
        Log($"[{runNo}/{totalRuns}] period={p}, percent={pct}");

        algoTrader.Reset();
        algoTrader.SetData(data);
        algoTrader.SingleTraderRunMode = TraderRunMode.TradeOnly;

        algoTrader.ConfigureStrategy(strategyName, new Dictionary<string, object>
        {
            ["period"]  = p,
            ["percent"] = pct,
            ["mostMaMethod"] = mostMaMethod,
            ["priceSource"] = priceSource,
            ["signalModeIndex"] = signalModeIndex
        });

        if (meta != null)
        {
            algoTrader.SymbolName   = sembol;
            algoTrader.SymbolPeriod = periyot;
        }

        algoTrader.Initialize();
        await algoTrader.RunSingleTraderWithProgressAsync();

        var trader = algoTrader.SingleTrader;
        var ozet = trader?.TaramaOzeti ?? "N/A";
        results.Add((p, pct, ozet));

        Log($"         -> {ozet}");
    }
}

// 3. Ozet tablo
Log($"\n{"=== SONUC TABLOSU ===",50}");
Log($"{"Period",8} {"Percent",8} {"Ozet"}");
Log($"{"------",8} {"-------",8} {"----"}");

foreach (var r in results)
{
    Log($"{r.period,8} {r.percent,8:F1} {r.ozet}");
}

reader.Dispose();
Log($"\n=== {totalRuns} kombinasyon tamamlandi ===");
"OK"
