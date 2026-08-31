// =============================================================================
// runStrategy.csx - Veri oku, strateji calistir, sonuclari goster
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
int    strategyChoice  = 0; // 0=SimpleMostStrategy, 1=SimpleMAStrategy, 2=SimpleRSIStrategy, 3=SimpleOTTStrategy

string strategyName;
Dictionary<string, object> strategyParams;

if (strategyChoice == 0)
{
    strategyName = "SimpleMostStrategy";
    strategyParams = new Dictionary<string, object>
    {
        ["period"] = 21,
        ["percent"] = 1.0,
        ["mostMaMethod"] = "EMA",
        ["priceSource"] = "Close",
        ["signalModeIndex"] = 0
    };
}
else if (strategyChoice == 1)
{
    strategyName = "SimpleMAStrategy";
    strategyParams = new Dictionary<string, object>
    {
        ["fastPeriod"] = 10,
        ["slowPeriod"] = 20,
        ["fastMaMethod"] = "EMA",
        ["slowMaMethod"] = "EMA",
        ["priceSource"] = "Close",
        ["signalModeIndex"] = 0
    };
}
else if (strategyChoice == 2)
{
    strategyName = "SimpleRSIStrategy";
    strategyParams = new Dictionary<string, object>
    {
        ["period"] = 14,
        ["oversold"] = 30,
        ["overbought"] = 70,
        ["priceSource"] = "Close",
        ["signalModeIndex"] = 0
    };
}
else if (strategyChoice == 3)
{
    strategyName = "SimpleOTTStrategy";
    strategyParams = new Dictionary<string, object>
    {
        ["period"] = 2,
        ["percent"] = 1.4,
        ["ottMaMethod"] = "VIDYA",
        ["priceSource"] = "Close",
        ["signalModeIndex"] = 0
    };
}
else
{
    throw new ArgumentOutOfRangeException(nameof(strategyChoice), $"Bilinmeyen strategyChoice: {strategyChoice}");
}
// -----------------------------------------------------------------------------

Log($"=== runStrategy.csx ===");
Log($"Data file : {dataFile}");
Log($"Strategy  : {strategyName}");

// 1. Veri oku
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
Log($"Sembol    : {meta.GetValueOrDefault("GrafikSembol", "N/A")}");
Log($"Periyot   : {meta.GetValueOrDefault("GrafikPeriyot", "N/A")}");
Log($"Bar Count : {meta.GetValueOrDefault("BarCount", "N/A")}");

reader.ReadDataFast(dataFile);
var data = reader.GetData();
Log($"Okunan    : {data.Count} bar");

if (data.Count == 0)
{
    Log("[HATA] Data bos.");
    return;
}

// 2. AlgoTrader konfigure et
algoTrader.SetData(data);

// RunSingleTraderWithProgressAsync() AlgoTrader'in kendi ic _timer/_logger
// alanlarini kullaniyor - console'daki her menu bunlari RegisterLogger/
// RegisterTimer ile dolduruyor, script'te de gerekiyor.
algoTrader.RegisterLogger(LogManager.GetInstance());
algoTrader.RegisterTimer(TimeManager.GetInstance());

algoTrader.SingleTraderRunMode = TraderRunMode.TradeOnly;

algoTrader.ConfigureStrategy(strategyName, strategyParams);

if (meta != null)
{
    algoTrader.SymbolName   = meta.GetValueOrDefault("GrafikSembol", "N/A");
    algoTrader.SymbolPeriod = meta.GetValueOrDefault("GrafikPeriyot", "N/A");
}

algoTrader.Initialize();

Log($"\n{algoTrader.GetDataInfo()}");

// 3. Calistir
Log("Trader calisiyor...");
await algoTrader.RunSingleTraderWithProgressAsync();

// 4. Sonuc
var trader = algoTrader.SingleTrader;
if (trader != null)
{
    Log($"\n=== SONUCLAR ===");
    Log($"Ozet      : {trader.TaramaOzeti ?? "N/A"}");
    SendResult("TaramaOzeti", trader.TaramaOzeti ?? "N/A");
}

reader.Dispose();
Log("=== Bitti ===");
"OK"
