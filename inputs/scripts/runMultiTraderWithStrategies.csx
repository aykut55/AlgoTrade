// =============================================================================
// runMultipleTrader.csx - Veri oku, multiple trader calistir, sonuclari goster
// Kullanim: Menu [8] ile calistirin
// =============================================================================
using System.IO;
using System.Collections.Concurrent;
using AlgoTrade.Core;
using AlgoTrade.Core.StockDataReader;
using AlgoTrade.Core.Trading;
using AlgoTrade.Core.Timer;

// ---- PARAMETRELER (Buradan degistirin) --------------------------------------
string dataFile = @"C:\data\csvFiles\VIP\05\VIP-X030-T.csv";
// -----------------------------------------------------------------------------

Log($"=== runMultipleTrader.csx ===");
Log($"Data file : {dataFile}");

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

// RunMultipleTraderWithProgressAsync() AlgoTrader'in kendi ic _timer/_logger
// alanlarini kullaniyor - console'daki her menu bunlari RegisterLogger/
// RegisterTimer ile dolduruyor, script'te de gerekiyor.
algoTrader.RegisterLogger(LogManager.GetInstance());
algoTrader.RegisterTimer(TimeManager.GetInstance());

algoTrader.SingleTraderRunMode = TraderRunMode.TradeOnly;

if (meta != null)
{
    algoTrader.SymbolName   = meta.GetValueOrDefault("GrafikSembol", "N/A");
    algoTrader.SymbolPeriod = meta.GetValueOrDefault("GrafikPeriyot", "N/A");
}

// 3. Strateji listesi
algoTrader.ClearStrategyConfigs();

algoTrader.AddStrategyConfig(0, "SimpleMostStrategy", new Dictionary<string, object>
{
    ["period"]  = 21,
    ["percent"] = 1.0,
    ["mostMaMethod"] = "EMA",
    ["priceSource"] = "Close",
    ["buySignalModeIndex"] = 0,
    ["sellSignalModeIndex"] = 0
});

algoTrader.AddStrategyConfig(1, "SimpleMAStrategy", new Dictionary<string, object>
{
    ["fastPeriod"] = 10,
    ["slowPeriod"] = 20,
    ["fastMaMethod"] = "EMA",
    ["slowMaMethod"] = "EMA",
    ["priceSource"] = "Close",
    ["buySignalModeIndex"] = 0,
    ["sellSignalModeIndex"] = 0
});

algoTrader.AddStrategyConfig(2, "SimpleRSIStrategy", new Dictionary<string, object>
{
    ["period"] = 14,
    ["oversold"] = 30,
    ["overbought"] = 70,
    ["priceSource"] = "Close",
    ["buySignalModeIndex"] = 0,
    ["sellSignalModeIndex"] = 0
});

algoTrader.AddStrategyConfig(3, "SimpleOTTStrategy", new Dictionary<string, object>
{
    ["period"] = 2,
    ["percent"] = 1.4,
    ["ottMaMethod"] = "VIDYA",
    ["priceSource"] = "Close",
    ["buySignalModeIndex"] = 0,
    ["sellSignalModeIndex"] = 0
});

algoTrader.AddStrategyConfig(4, "SimpleSuperTrendStrategy", new Dictionary<string, object>
{
    ["period"] = 10,
    ["multiplier"] = 3.0,
    ["priceSource"] = "Close",
    ["buySignalModeIndex"] = 0,
    ["sellSignalModeIndex"] = 0
});

algoTrader.AddStrategyConfig(5, "SimpleParabolicSARStrategy", new Dictionary<string, object>
{
    ["step"] = 0.02,
    ["max"] = 0.2,
    ["priceSource"] = "Close",
    ["buySignalModeIndex"] = 0,
    ["sellSignalModeIndex"] = 0
});

algoTrader.AddStrategyConfig(6, "SimpleADXStrategy", new Dictionary<string, object>
{
    ["period"] = 14,
    ["adxThreshold"] = 25,
    ["buySignalModeIndex"] = 0,
    ["sellSignalModeIndex"] = 0
});

algoTrader.AddStrategyConfig(7, "SimpleDIStrategy", new Dictionary<string, object>
{
    ["period"] = 14,
    ["buySignalModeIndex"] = 0,
    ["sellSignalModeIndex"] = 0
});

algoTrader.AddStrategyConfig(8, "SimpleMACDStrategy", new Dictionary<string, object>
{
    ["fastPeriod"] = 12,
    ["slowPeriod"] = 26,
    ["signalPeriod"] = 9,
    ["priceSource"] = "Close",
    ["buySignalModeIndex"] = 0,
    ["sellSignalModeIndex"] = 0
});

algoTrader.AddStrategyConfig(9, "SimpleStochasticStrategy", new Dictionary<string, object>
{
    ["kPeriod"] = 14,
    ["dPeriod"] = 3,
    ["centerLine"] = 50,
    ["buySignalModeIndex"] = 0,
    ["sellSignalModeIndex"] = 0
});

algoTrader.AddStrategyConfig(10, "SimpleBollingerStrategy", new Dictionary<string, object>
{
    ["period"] = 20,
    ["multiplier"] = 2.0,
    ["priceSource"] = "Close",
    ["buySignalModeIndex"] = 0,
    ["sellSignalModeIndex"] = 0
});

algoTrader.AddStrategyConfig(11, "SimpleATRStrategy", new Dictionary<string, object>
{
    ["atrPeriod"] = 14,
    ["maPeriod"] = 20,
    ["multiplier"] = 2.0,
    ["priceSource"] = "Close",
    ["buySignalModeIndex"] = 0,
    ["sellSignalModeIndex"] = 0
});

algoTrader.AddStrategyConfig(12, "SimpleCMFStrategy", new Dictionary<string, object>
{
    ["period"] = 20,
    ["positiveThreshold"] = 0.1,
    ["negativeThreshold"] = -0.1,
    ["buySignalModeIndex"] = 0,
    ["sellSignalModeIndex"] = 0
});

algoTrader.AddStrategyConfig(13, "SimpleMFIStrategy", new Dictionary<string, object>
{
    ["period"] = 14,
    ["oversold"] = 20,
    ["overbought"] = 80,
    ["buySignalModeIndex"] = 0,
    ["sellSignalModeIndex"] = 0
});

algoTrader.AddStrategyConfig(14, "SimpleKairiStrategy", new Dictionary<string, object>
{
    ["period"] = 20,
    ["positiveThreshold"] = 5,
    ["negativeThreshold"] = -5,
    ["priceSource"] = "Close",
    ["buySignalModeIndex"] = 0,
    ["sellSignalModeIndex"] = 0
});

algoTrader.AddStrategyConfig(15, "SimpleMomentumStrategy", new Dictionary<string, object>
{
    ["period"] = 12,
    ["positiveThreshold"] = 0,
    ["negativeThreshold"] = 0,
    ["priceSource"] = "Close",
    ["buySignalModeIndex"] = 0,
    ["sellSignalModeIndex"] = 0
});

algoTrader.AddStrategyConfig(16, "SimpleHHVLLVStrategy", new Dictionary<string, object>
{
    ["period"] = 20,
    ["priceSource"] = "Close",
    ["buySignalModeIndex"] = 0,
    ["sellSignalModeIndex"] = 0
});

algoTrader.AddStrategyConfig(17, "SimpleHYLYStrategy", new Dictionary<string, object>
{
    ["period"] = 20,
    ["threshold"] = 80,
    ["priceSource"] = "Close",
    ["buySignalModeIndex"] = 0,
    ["sellSignalModeIndex"] = 0
});

algoTrader.AddStrategyConfig(18, "SimpleIchimokuStrategy", new Dictionary<string, object>
{
    ["tenkanPeriod"] = 9,
    ["kijunPeriod"] = 26,
    ["senkouPeriod"] = 52,
    ["buySignalModeIndex"] = 0,
    ["sellSignalModeIndex"] = 0
});

algoTrader.AddStrategyConfig(19, "SimpleMavilimWStrategy", new Dictionary<string, object>
{
    ["param1"] = 3,
    ["param2"] = 5,
    ["buySignalModeIndex"] = 0,
    ["sellSignalModeIndex"] = 0
});

algoTrader.AddStrategyConfig(20, "SimplePMaxStrategy", new Dictionary<string, object>
{
    ["atrPeriod"] = 10,
    ["multiplier"] = 3.0,
    ["maPeriod"] = 10,
    ["pmaxMaMethod"] = "EMA",
    ["buySignalModeIndex"] = 0,
    ["sellSignalModeIndex"] = 0
});

algoTrader.AddStrategyConfig(21, "SimpleTillsonT3Strategy", new Dictionary<string, object>
{
    ["period"] = 5,
    ["priceSource"] = "Close",
    ["buySignalModeIndex"] = 0,
    ["sellSignalModeIndex"] = 0
});

algoTrader.AddStrategyConfig(22, "SimpleAlphaTrendStrategy", new Dictionary<string, object>
{
    ["atrPeriod"] = 14,
    ["coefficient"] = 1.0,
    ["momentumPeriod"] = 14,
    ["useMFI"] = true,
    ["buySignalModeIndex"] = 0,
    ["sellSignalModeIndex"] = 0
});

// 4. Query listesi
algoTrader.ClearQueryConfigs();

algoTrader.AddQueryConfig(0, "SimpleQuery1", new Dictionary<string, object>
{
    ["ma8Period"]   = 8,
    ["ma200Period"] = 200,
    ["choice"]      = 0
});

algoTrader.AddQueryConfig(1, "SimpleQuery1", new Dictionary<string, object>
{
    ["ma8Period"]   = 5,
    ["ma200Period"] = 100,
    ["choice"]      = 0
});

// 5. EquityCurveFilter listesi
algoTrader.ClearEquityCurveFilterConfigs();

algoTrader.AddEquityCurveFilterConfig(0,
    enabled: false,
    thresholdTypeIsPercent: true,
    profitThreshold: 0.05,
    lossThreshold: -0.05,
    trigger: ConfirmationTrigger.Both);

algoTrader.AddEquityCurveFilterConfig(1,
    enabled: false,
    thresholdTypeIsPercent: true,
    profitThreshold: 0.05,
    lossThreshold: -0.05,
    trigger: ConfirmationTrigger.Both);

// 6. Initialize ve calistir
algoTrader.Initialize();

Log($"\n{algoTrader.GetDataInfo()}");

Log("MultipleTrader calisiyor...");
await algoTrader.RunMultipleTraderWithProgressAsync();

// 7. Sonuclar
var mt = algoTrader.MultipleTrader;
if (mt != null)
{
    var mainTrader = mt.GetMainTrader();
    Log($"\n=== SONUCLAR ===");
    Log($"Main Trader Ozet : {mainTrader.TaramaOzeti ?? "N/A"}");
    SendResult("MainTrader_TaramaOzeti", mainTrader.TaramaOzeti ?? "N/A");

    for (int i = 0; i < mt.Traders.Count; i++)
    {
        var child = mt.Traders[i];
        Log($"Child_{i} Ozet    : {child.TaramaOzeti ?? "N/A"}");
        SendResult($"Child_{i}_TaramaOzeti", child.TaramaOzeti ?? "N/A");
    }
}

reader.Dispose();
Log("=== Bitti ===");
"OK"
