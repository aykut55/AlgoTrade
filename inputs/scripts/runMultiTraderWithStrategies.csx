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
string dataFile = @"C:\data\csvFiles\VIP\01\VIP-X030-T.csv";
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
    ["signalModeIndex"] = 0
});

algoTrader.AddStrategyConfig(1, "SimpleMAStrategy", new Dictionary<string, object>
{
    ["fastPeriod"] = 10,
    ["slowPeriod"] = 20,
    ["fastMaMethod"] = "EMA",
    ["slowMaMethod"] = "EMA",
    ["priceSource"] = "Close",
    ["signalModeIndex"] = 0
});

algoTrader.AddStrategyConfig(2, "SimpleRSIStrategy", new Dictionary<string, object>
{
    ["period"] = 14,
    ["oversold"] = 30,
    ["overbought"] = 70,
    ["priceSource"] = "Close",
    ["signalModeIndex"] = 0
});

algoTrader.AddStrategyConfig(3, "SimpleOTTStrategy", new Dictionary<string, object>
{
    ["period"] = 2,
    ["percent"] = 1.4,
    ["ottMaMethod"] = "VIDYA",
    ["priceSource"] = "Close",
    ["signalModeIndex"] = 0
});

algoTrader.AddStrategyConfig(4, "SimpleSuperTrendStrategy", new Dictionary<string, object>
{
    ["period"] = 10,
    ["multiplier"] = 3.0,
    ["priceSource"] = "Close",
    ["signalModeIndex"] = 0
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
