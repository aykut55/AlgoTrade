// =============================================================================
// runStrategy.csx - Veri oku, strateji calistir, sonuclari goster
// Kullanim: Menu [4] ile calistirin
// =============================================================================
using System.IO;
using System.Collections.Concurrent;
using AlgoTrade.Core;
using AlgoTrade.Core.StockDataReader;
using AlgoTrade.Core.Trading;

// ---- PARAMETRELER (Buradan degistirin) --------------------------------------
string dataFile       = @"C:\data\csvFiles\VIP\01\VIP-X030-T.csv";
string strategyName   = "SimpleMostStrategy";
int    period         = 21;
double percent        = 1.0;
int    choice         = 0;
// -----------------------------------------------------------------------------

Log($"=== runStrategy.csx ===");
Log($"Data file : {dataFile}");
Log($"Strategy  : {strategyName} (period={period}, percent={percent}, choice={choice})");

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
algoTrader.SingleTraderRunMode = TraderRunMode.TradeOnly;

algoTrader.ConfigureStrategy(strategyName, new Dictionary<string, object>
{
    ["period"]  = period,
    ["percent"] = percent,
    ["choice"]  = choice
});

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
