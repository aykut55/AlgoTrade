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
int    strategyChoice  = 0; // 0=SimpleMostStrategy, 1=SimpleMAStrategy, 2=SimpleRSIStrategy, 3=SimpleOTTStrategy, 4=SimpleSuperTrendStrategy, 5=SimpleParabolicSARStrategy

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
else if (strategyChoice == 4)
{
    strategyName = "SimpleSuperTrendStrategy";
    strategyParams = new Dictionary<string, object>
    {
        ["period"] = 10,
        ["multiplier"] = 3.0,
        ["priceSource"] = "Close",
        ["signalModeIndex"] = 0
    };
}
else if (strategyChoice == 5)
{
    strategyName = "SimpleParabolicSARStrategy";
    strategyParams = new Dictionary<string, object>
    {
        ["step"] = 0.02,
        ["max"] = 0.2,
        ["priceSource"] = "Close",
        ["signalModeIndex"] = 0
    };
}
else if (strategyChoice == 6)
{
    strategyName = "SimpleADXStrategy";
    strategyParams = new Dictionary<string, object>
    {
        ["period"] = 14,
        ["adxThreshold"] = 25,
        ["signalModeIndex"] = 0
    };
}
else if (strategyChoice == 7)
{
    strategyName = "SimpleDIStrategy";
    strategyParams = new Dictionary<string, object>
    {
        ["period"] = 14,
        ["signalModeIndex"] = 0
    };
}
else if (strategyChoice == 8)
{
    strategyName = "SimpleMACDStrategy";
    strategyParams = new Dictionary<string, object>
    {
        ["fastPeriod"] = 12,
        ["slowPeriod"] = 26,
        ["signalPeriod"] = 9,
        ["priceSource"] = "Close",
        ["signalModeIndex"] = 0
    };
}
else if (strategyChoice == 9)
{
    strategyName = "SimpleStochasticStrategy";
    strategyParams = new Dictionary<string, object>
    {
        ["kPeriod"] = 14,
        ["dPeriod"] = 3,
        ["centerLine"] = 50,
        ["signalModeIndex"] = 0
    };
}
else if (strategyChoice == 10)
{
    strategyName = "SimpleBollingerStrategy";
    strategyParams = new Dictionary<string, object>
    {
        ["period"] = 20,
        ["multiplier"] = 2.0,
        ["priceSource"] = "Close",
        ["signalModeIndex"] = 0
    };
}
else if (strategyChoice == 11)
{
    strategyName = "SimpleATRStrategy";
    strategyParams = new Dictionary<string, object>
    {
        ["atrPeriod"] = 14,
        ["maPeriod"] = 20,
        ["multiplier"] = 2.0,
        ["priceSource"] = "Close",
        ["signalModeIndex"] = 0
    };
}
else if (strategyChoice == 12)
{
    strategyName = "SimpleCMFStrategy";
    strategyParams = new Dictionary<string, object>
    {
        ["period"] = 20,
        ["positiveThreshold"] = 0.1,
        ["negativeThreshold"] = -0.1,
        ["signalModeIndex"] = 0
    };
}
else if (strategyChoice == 13)
{
    strategyName = "SimpleMFIStrategy";
    strategyParams = new Dictionary<string, object>
    {
        ["period"] = 14,
        ["oversold"] = 20,
        ["overbought"] = 80,
        ["signalModeIndex"] = 0
    };
}
else if (strategyChoice == 14)
{
    strategyName = "SimpleKairiStrategy";
    strategyParams = new Dictionary<string, object>
    {
        ["period"] = 20,
        ["positiveThreshold"] = 5,
        ["negativeThreshold"] = -5,
        ["priceSource"] = "Close",
        ["signalModeIndex"] = 0
    };
}
else if (strategyChoice == 15)
{
    strategyName = "SimpleMomentumStrategy";
    strategyParams = new Dictionary<string, object>
    {
        ["period"] = 12,
        ["positiveThreshold"] = 0,
        ["negativeThreshold"] = 0,
        ["priceSource"] = "Close",
        ["signalModeIndex"] = 0
    };
}
else if (strategyChoice == 16)
{
    strategyName = "SimpleHHVLLVStrategy";
    strategyParams = new Dictionary<string, object>
    {
        ["period"] = 20,
        ["priceSource"] = "Close",
        ["signalModeIndex"] = 0
    };
}
else if (strategyChoice == 17)
{
    strategyName = "SimpleHYLYStrategy";
    strategyParams = new Dictionary<string, object>
    {
        ["period"] = 20,
        ["threshold"] = 80,
        ["priceSource"] = "Close",
        ["signalModeIndex"] = 0
    };
}
else if (strategyChoice == 18)
{
    strategyName = "SimpleIchimokuStrategy";
    strategyParams = new Dictionary<string, object>
    {
        ["tenkanPeriod"] = 9,
        ["kijunPeriod"] = 26,
        ["senkouPeriod"] = 52,
        ["signalModeIndex"] = 0
    };
}
else if (strategyChoice == 19)
{
    strategyName = "SimpleMavilimWStrategy";
    strategyParams = new Dictionary<string, object>
    {
        ["param1"] = 3,
        ["param2"] = 5,
        ["signalModeIndex"] = 0
    };
}
else if (strategyChoice == 20)
{
    strategyName = "SimplePMaxStrategy";
    strategyParams = new Dictionary<string, object>
    {
        ["atrPeriod"] = 10,
        ["multiplier"] = 3.0,
        ["maPeriod"] = 10,
        ["pmaxMaMethod"] = "EMA",
        ["signalModeIndex"] = 0
    };
}
else if (strategyChoice == 21)
{
    strategyName = "SimpleTillsonT3Strategy";
    strategyParams = new Dictionary<string, object>
    {
        ["period"] = 5,
        ["priceSource"] = "Close",
        ["signalModeIndex"] = 0
    };
}
else if (strategyChoice == 22)
{
    strategyName = "SimpleAlphaTrendStrategy";
    strategyParams = new Dictionary<string, object>
    {
        ["atrPeriod"] = 14,
        ["coefficient"] = 1.0,
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
