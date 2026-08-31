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
string dataFile        = @"C:\data\csvFiles\VIP\01\VIP-X030-T.csv";
int    strategyChoice  = 0; // 0=SimpleMostStrategy (period x percent), 1=SimpleMAStrategy (fastPeriod x slowPeriod), 2=SimpleRSIStrategy (period x oversold)

string strategyName;
string sweepParam1Name, sweepParam2Name;
object[] sweepParam1Values, sweepParam2Values;
Dictionary<string, object> fixedParams;

if (strategyChoice == 0)
{
    strategyName = "SimpleMostStrategy";
    sweepParam1Name   = "period";
    sweepParam1Values = new object[] { 10, 15, 21, 30, 50 };
    sweepParam2Name   = "percent";
    sweepParam2Values = new object[] { 0.5, 1.0, 1.5, 2.0 };
    fixedParams = new Dictionary<string, object>
    {
        ["mostMaMethod"] = "EMA",
        ["priceSource"] = "Close",
        ["signalModeIndex"] = 0
    };
}
else if (strategyChoice == 1)
{
    strategyName = "SimpleMAStrategy";
    sweepParam1Name   = "fastPeriod";
    sweepParam1Values = new object[] { 5, 10, 15, 20 };
    sweepParam2Name   = "slowPeriod";
    sweepParam2Values = new object[] { 20, 30, 50, 100 };
    fixedParams = new Dictionary<string, object>
    {
        ["fastMaMethod"] = "EMA",
        ["slowMaMethod"] = "EMA",
        ["priceSource"] = "Close",
        ["signalModeIndex"] = 0
    };
}
else if (strategyChoice == 2)
{
    strategyName = "SimpleRSIStrategy";
    sweepParam1Name   = "period";
    sweepParam1Values = new object[] { 7, 10, 14, 21, 28 };
    sweepParam2Name   = "oversold";
    sweepParam2Values = new object[] { 20, 25, 30, 35 };
    fixedParams = new Dictionary<string, object>
    {
        ["overbought"] = 70,
        ["priceSource"] = "Close",
        ["signalModeIndex"] = 0
    };
}
else
{
    throw new ArgumentOutOfRangeException(nameof(strategyChoice), $"Bilinmeyen strategyChoice: {strategyChoice}");
}
// -----------------------------------------------------------------------------

Log($"=== paramSweep.csx ===");
Log($"Data file : {dataFile}");
Log($"Strategy  : {strategyName}");
Log($"{sweepParam1Name,-12}: [{string.Join(", ", sweepParam1Values)}]");
Log($"{sweepParam2Name,-12}: [{string.Join(", ", sweepParam2Values)}]");
Log($"Toplam kombinasyon: {sweepParam1Values.Length * sweepParam2Values.Length}");

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
var results = new List<(object param1, object param2, string ozet)>();
int runNo = 0;
int totalRuns = sweepParam1Values.Length * sweepParam2Values.Length;

foreach (var v1 in sweepParam1Values)
{
    foreach (var v2 in sweepParam2Values)
    {
        runNo++;
        Log($"[{runNo}/{totalRuns}] {sweepParam1Name}={v1}, {sweepParam2Name}={v2}");

        algoTrader.Reset();
        algoTrader.SetData(data);
        algoTrader.SingleTraderRunMode = TraderRunMode.TradeOnly;

        var strategyParams = new Dictionary<string, object>(fixedParams)
        {
            [sweepParam1Name] = v1,
            [sweepParam2Name] = v2
        };
        algoTrader.ConfigureStrategy(strategyName, strategyParams);

        if (meta != null)
        {
            algoTrader.SymbolName   = sembol;
            algoTrader.SymbolPeriod = periyot;
        }

        algoTrader.Initialize();
        await algoTrader.RunSingleTraderWithProgressAsync();

        var trader = algoTrader.SingleTrader;
        var ozet = trader?.TaramaOzeti ?? "N/A";
        results.Add((v1, v2, ozet));

        Log($"         -> {ozet}");
    }
}

// 3. Ozet tablo
Log($"\n{"=== SONUC TABLOSU ===",50}");
Log($"{sweepParam1Name,8} {sweepParam2Name,8} {"Ozet"}");
Log($"{"------",8} {"-------",8} {"----"}");

foreach (var r in results)
{
    Log($"{r.param1,8} {r.param2,8} {r.ozet}");
}

reader.Dispose();
Log($"\n=== {totalRuns} kombinasyon tamamlandi ===");
"OK"
