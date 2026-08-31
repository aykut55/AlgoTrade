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
string dataFile        = @"C:\data\csvFiles\VIP\05\VIP-X030-T.csv";
int    strategyChoice  = 0; // 0=SimpleMostStrategy (period x percent), 1=SimpleMAStrategy (fastPeriod x slowPeriod), 2=SimpleRSIStrategy (period x oversold), 3=SimpleOTTStrategy (period x percent), 4=SimpleSuperTrendStrategy (period x multiplier), 5=SimpleParabolicSARStrategy (step x max)

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
else if (strategyChoice == 3)
{
    strategyName = "SimpleOTTStrategy";
    sweepParam1Name   = "period";
    sweepParam1Values = new object[] { 1, 2, 3, 5, 10 };
    sweepParam2Name   = "percent";
    sweepParam2Values = new object[] { 0.5, 1.0, 1.4, 2.0, 3.0 };
    fixedParams = new Dictionary<string, object>
    {
        ["ottMaMethod"] = "VIDYA",
        ["priceSource"] = "Close",
        ["signalModeIndex"] = 0
    };
}
else if (strategyChoice == 4)
{
    strategyName = "SimpleSuperTrendStrategy";
    sweepParam1Name   = "period";
    sweepParam1Values = new object[] { 7, 10, 14, 21 };
    sweepParam2Name   = "multiplier";
    sweepParam2Values = new object[] { 1.0, 2.0, 3.0, 4.0 };
    fixedParams = new Dictionary<string, object>
    {
        ["priceSource"] = "Close",
        ["signalModeIndex"] = 0
    };
}
else if (strategyChoice == 5)
{
    strategyName = "SimpleParabolicSARStrategy";
    sweepParam1Name   = "step";
    sweepParam1Values = new object[] { 0.01, 0.02, 0.03, 0.05 };
    sweepParam2Name   = "max";
    sweepParam2Values = new object[] { 0.1, 0.2, 0.3, 0.4 };
    fixedParams = new Dictionary<string, object>
    {
        ["priceSource"] = "Close",
        ["signalModeIndex"] = 0
    };
}
else if (strategyChoice == 6)
{
    strategyName = "SimpleADXStrategy";
    sweepParam1Name   = "period";
    sweepParam1Values = new object[] { 7, 10, 14, 21 };
    sweepParam2Name   = "adxThreshold";
    sweepParam2Values = new object[] { 15, 20, 25, 30 };
    fixedParams = new Dictionary<string, object>
    {
        ["signalModeIndex"] = 0
    };
}
else if (strategyChoice == 7)
{
    strategyName = "SimpleDIStrategy";
    sweepParam1Name   = "period";
    sweepParam1Values = new object[] { 7, 10, 14, 21 };
    sweepParam2Name   = "signalModeIndex";
    sweepParam2Values = new object[] { 0, 1, 3, 7 };
    fixedParams = new Dictionary<string, object>();
}
else if (strategyChoice == 8)
{
    strategyName = "SimpleMACDStrategy";
    sweepParam1Name   = "fastPeriod";
    sweepParam1Values = new object[] { 8, 10, 12, 16 };
    sweepParam2Name   = "slowPeriod";
    sweepParam2Values = new object[] { 20, 26, 30, 35 };
    fixedParams = new Dictionary<string, object>
    {
        ["signalPeriod"] = 9,
        ["priceSource"] = "Close",
        ["signalModeIndex"] = 0
    };
}
else if (strategyChoice == 9)
{
    strategyName = "SimpleStochasticStrategy";
    sweepParam1Name   = "kPeriod";
    sweepParam1Values = new object[] { 7, 10, 14, 21 };
    sweepParam2Name   = "dPeriod";
    sweepParam2Values = new object[] { 2, 3, 5, 8 };
    fixedParams = new Dictionary<string, object>
    {
        ["centerLine"] = 50,
        ["signalModeIndex"] = 0
    };
}
else if (strategyChoice == 10)
{
    strategyName = "SimpleBollingerStrategy";
    sweepParam1Name   = "period";
    sweepParam1Values = new object[] { 10, 15, 20, 30 };
    sweepParam2Name   = "multiplier";
    sweepParam2Values = new object[] { 1.5, 2.0, 2.5, 3.0 };
    fixedParams = new Dictionary<string, object>
    {
        ["priceSource"] = "Close",
        ["signalModeIndex"] = 0
    };
}
else if (strategyChoice == 11)
{
    strategyName = "SimpleATRStrategy";
    sweepParam1Name   = "atrPeriod";
    sweepParam1Values = new object[] { 7, 10, 14, 21 };
    sweepParam2Name   = "multiplier";
    sweepParam2Values = new object[] { 1.5, 2.0, 2.5, 3.0 };
    fixedParams = new Dictionary<string, object>
    {
        ["maPeriod"] = 20,
        ["priceSource"] = "Close",
        ["signalModeIndex"] = 0
    };
}
else if (strategyChoice == 12)
{
    strategyName = "SimpleCMFStrategy";
    sweepParam1Name   = "period";
    sweepParam1Values = new object[] { 10, 15, 20, 30 };
    sweepParam2Name   = "positiveThreshold";
    sweepParam2Values = new object[] { 0.05, 0.1, 0.15, 0.2 };
    fixedParams = new Dictionary<string, object>
    {
        ["negativeThreshold"] = -0.1,
        ["signalModeIndex"] = 0
    };
}
else if (strategyChoice == 13)
{
    strategyName = "SimpleMFIStrategy";
    sweepParam1Name   = "period";
    sweepParam1Values = new object[] { 7, 10, 14, 21 };
    sweepParam2Name   = "oversold";
    sweepParam2Values = new object[] { 10, 15, 20, 25 };
    fixedParams = new Dictionary<string, object>
    {
        ["overbought"] = 80,
        ["signalModeIndex"] = 0
    };
}
else if (strategyChoice == 14)
{
    strategyName = "SimpleKairiStrategy";
    sweepParam1Name   = "period";
    sweepParam1Values = new object[] { 10, 15, 20, 30 };
    sweepParam2Name   = "positiveThreshold";
    sweepParam2Values = new object[] { 3, 5, 7, 10 };
    fixedParams = new Dictionary<string, object>
    {
        ["negativeThreshold"] = -5,
        ["priceSource"] = "Close",
        ["signalModeIndex"] = 0
    };
}
else if (strategyChoice == 15)
{
    strategyName = "SimpleMomentumStrategy";
    sweepParam1Name   = "period";
    sweepParam1Values = new object[] { 7, 10, 12, 21 };
    sweepParam2Name   = "signalModeIndex";
    sweepParam2Values = new object[] { 0, 1, 3, 7 };
    fixedParams = new Dictionary<string, object>
    {
        ["priceSource"] = "Close"
    };
}
else if (strategyChoice == 16)
{
    strategyName = "SimpleHHVLLVStrategy";
    sweepParam1Name   = "period";
    sweepParam1Values = new object[] { 10, 15, 20, 30 };
    sweepParam2Name   = "signalModeIndex";
    sweepParam2Values = new object[] { 0, 1, 3, 7 };
    fixedParams = new Dictionary<string, object>
    {
        ["priceSource"] = "Close"
    };
}
else if (strategyChoice == 17)
{
    strategyName = "SimpleHYLYStrategy";
    sweepParam1Name   = "period";
    sweepParam1Values = new object[] { 10, 15, 20, 30 };
    sweepParam2Name   = "threshold";
    sweepParam2Values = new object[] { 70, 75, 80, 85 };
    fixedParams = new Dictionary<string, object>
    {
        ["priceSource"] = "Close",
        ["signalModeIndex"] = 0
    };
}
else if (strategyChoice == 18)
{
    strategyName = "SimpleIchimokuStrategy";
    sweepParam1Name   = "tenkanPeriod";
    sweepParam1Values = new object[] { 7, 9, 11, 13 };
    sweepParam2Name   = "kijunPeriod";
    sweepParam2Values = new object[] { 20, 26, 30, 35 };
    fixedParams = new Dictionary<string, object>
    {
        ["senkouPeriod"] = 52,
        ["signalModeIndex"] = 0
    };
}
else if (strategyChoice == 19)
{
    strategyName = "SimpleMavilimWStrategy";
    sweepParam1Name   = "param1";
    sweepParam1Values = new object[] { 2, 3, 4, 5 };
    sweepParam2Name   = "param2";
    sweepParam2Values = new object[] { 4, 5, 6, 7 };
    fixedParams = new Dictionary<string, object>
    {
        ["signalModeIndex"] = 0
    };
}
else if (strategyChoice == 20)
{
    strategyName = "SimplePMaxStrategy";
    sweepParam1Name   = "atrPeriod";
    sweepParam1Values = new object[] { 7, 10, 13, 16 };
    sweepParam2Name   = "multiplier";
    sweepParam2Values = new object[] { 2.0, 3.0, 4.0, 5.0 };
    fixedParams = new Dictionary<string, object>
    {
        ["maPeriod"] = 10,
        ["pmaxMaMethod"] = "EMA",
        ["signalModeIndex"] = 0
    };
}
else if (strategyChoice == 21)
{
    strategyName = "SimpleTillsonT3Strategy";
    sweepParam1Name   = "period";
    sweepParam1Values = new object[] { 3, 5, 7, 9 };
    sweepParam2Name   = "signalModeIndex";
    sweepParam2Values = new object[] { 0, 1, 3, 7 };
    fixedParams = new Dictionary<string, object>
    {
        ["priceSource"] = "Close"
    };
}
else if (strategyChoice == 22)
{
    strategyName = "SimpleAlphaTrendStrategy";
    sweepParam1Name   = "atrPeriod";
    sweepParam1Values = new object[] { 7, 10, 14, 21 };
    sweepParam2Name   = "coefficient";
    sweepParam2Values = new object[] { 0.5, 1.0, 1.5, 2.0 };
    fixedParams = new Dictionary<string, object>
    {
        ["momentumPeriod"] = 14,
        ["useMFI"] = true,
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
