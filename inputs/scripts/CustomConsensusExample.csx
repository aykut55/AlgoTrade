// =============================================================================
// CustomConsensusExample.csx - MultipleTrader'in consensus kuralini script'ten
// tanimlama ornegi (migration-guide.md Madde 5.3 - "MultiTrader icin Scripting").
// Kullanim: Menu [8] Run Script ile calistirin.
//
// NOT: algoTrader.RunMultipleTraderWithProgressAsync() kendi icinde multipleTrader'i
// yaratip ayni cagri icinde calistirdigi icin (bkz. AlgoTrader.cs:1914), disaridan
// CustomConsensusFunc atayacak bir "olusturuldu ama henuz calismadi" arasi yok.
// Bu yuzden bu ornek runMultiTraderWithStrategies.csx'in aksine, MultipleTrader'i
// 02_RunMultipleTraderWithProgressAsync.csx'teki gibi MANUEL kurup calistiriyor -
// boylece CustomConsensusFunc'i Init()'ten sonra, Run donguisunden once atayabiliyoruz.
// =============================================================================
using System.IO;
using System.Diagnostics;
using AlgoTrade.Core;
using AlgoTrade.Core.StockDataReader;
using AlgoTrade.Core.Trading;
using AlgoTrade.Core.Trading.Core;
using AlgoTrade.Core.Timer;

// ---- PARAMETRELER (Buradan degistirin) --------------------------------------
string dataFile = @"C:\data\csvFiles\VIP\01\VIP-X030-T.csv";
// -----------------------------------------------------------------------------

Log("=== CustomConsensusExample.csx ===");
Log($"Data file : {dataFile}");

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
string symbolName   = meta.GetValueOrDefault("GrafikSembol", "N/A");
string symbolPeriod = meta.GetValueOrDefault("GrafikPeriyot", "N/A");
Log($"Sembol    : {symbolName}");
Log($"Periyot   : {symbolPeriod}");

reader.ReadDataFast(dataFile);
var data = reader.GetData();
Log($"Okunan    : {data.Count} bar");

if (data.Count == 0)
{
    Log("[HATA] Data bos.");
    return;
}

// =============================================================================
// 1. Indicators + uc child strateji (SimpleMostStrategy + SimpleMAStrategy + SimpleRSIStrategy)
// =============================================================================
var indicators = new IndicatorManager(data);

// algoTrader'i sadece strateji factory'si olarak kullaniyoruz.
algoTrader.SetData(data);
algoTrader.RegisterLogger(LogManager.GetInstance());
algoTrader.RegisterTimer(TimeManager.GetInstance());
algoTrader.SymbolName   = symbolName;
algoTrader.SymbolPeriod = symbolPeriod;
algoTrader.Initialize();

var childStrategy0 = algoTrader.CreateStrategyFromRegistry(data, indicators, "SimpleMostStrategy",
    new Dictionary<string, object> { ["period"] = 21, ["percent"] = 1.0, ["mostMaMethod"] = "EMA", ["priceSource"] = "Close", ["signalModeIndex"] = 0 });
var childStrategy1 = algoTrader.CreateStrategyFromRegistry(data, indicators, "SimpleMAStrategy",
    new Dictionary<string, object> { ["fastPeriod"] = 10, ["slowPeriod"] = 20, ["fastMaMethod"] = "EMA", ["slowMaMethod"] = "EMA", ["priceSource"] = "Close", ["signalModeIndex"] = 0 });
var childStrategy2 = algoTrader.CreateStrategyFromRegistry(data, indicators, "SimpleRSIStrategy",
    new Dictionary<string, object> { ["period"] = 14, ["oversold"] = 30, ["overbought"] = 70, ["priceSource"] = "Close", ["signalModeIndex"] = 0 });

// =============================================================================
// 2. MultipleTrader'i manuel kur (mainTrader + 3 child)
// =============================================================================
Log("\nMultipleTrader kuruluyor (manuel, CustomConsensusFunc atanabilsin diye)...");

var multipleTrader = new MultipleTrader(0, data, indicators, null);
multipleTrader.Reset();

var mainTrader = multipleTrader.GetMainTrader();
mainTrader.Reset();
mainTrader.initialTradeParams!.Reset()
    .SetBakiyeParams(ilkBakiye: 100000.0)
    .SetKontratParamsViopEndex(kontratSayisi: 1)
    .SetKomisyonParams(komisyonCarpan: 20.0)
    .SetKaymaParams(kaymaMiktari: 0.5);
mainTrader.RunMode = TraderRunMode.TradeOnly;
mainTrader.ConfigureUserFlagsOnce();
mainTrader.SaveStatisticsToFile = true;
mainTrader.Init();

void AddChild(int childId, IStrategy strategy)
{
    var child = new SingleTrader(childId, $"childTrader_{childId}", data, indicators, null);
    child.RunMode = TraderRunMode.TradeOnly;
    child.SetStrategy(strategy);
    child.Reset();
    child.SymbolName = symbolName;
    child.SymbolPeriod = symbolPeriod;
    child.initialTradeParams!.Reset()
        .SetBakiyeParams(ilkBakiye: 100000.0)
        .SetKontratParamsViopEndex(kontratSayisi: 1)
        .SetKomisyonParams(komisyonCarpan: 20.0)
        .SetKaymaParams(kaymaMiktari: 0.5);
    child.ConfigureUserFlagsOnce();
    child.SaveStatisticsToFile = true;
    child.Init();
    multipleTrader.AddTrader(child);
    Log($"  childTrader_{childId} olusturuldu.");
}

AddChild(0, childStrategy0);
AddChild(1, childStrategy1);
AddChild(2, childStrategy2);

multipleTrader.Init();

// =============================================================================
// 3. Custom Consensus Kurallari - referans method'lar
// =============================================================================
// Asagidaki method'lar gercek, derlenen C# kodu (yorum degil) - CustomConsensusFunc'a
// hangisini atadigini asagidaki "AKTIF KURAL" satirinda degistirerek deneyebilirsin.
// Ilk 4'u MultipleTrader.cs'teki hardcoded Net/Majority/All/Any modlarinin BIREBIR
// script'e tasinmis hali (kaynak: BuildConsensusSignal() + BuildNetConsensus()) -
// kendi kuralini yazarken buradan kopyalayip degistirebilirsin. Son 3'u ise hazir
// modlardan hicbirinin uretemeyecegi, tamamen ozel ornekler.

TradeSignals NetConsensusReference(List<SingleTrader> traders)
{
    int buyCount = 0, sellCount = 0;
    foreach (var t in traders)
    {
        if (t.is_son_yon_a()) buyCount++;
        else if (t.is_son_yon_s()) sellCount++;
    }
    int netSignal = buyCount - sellCount;
    int consensusMinNetCount = 1; // ConsensusMinNetCount ile ayni varsayilan
    if (netSignal >= consensusMinNetCount) return TradeSignals.Buy;
    if (netSignal <= -consensusMinNetCount) return TradeSignals.Sell;
    return TradeSignals.Flat;
}

TradeSignals MajorityConsensusReference(List<SingleTrader> traders)
{
    int buyCount = 0, sellCount = 0;
    foreach (var t in traders)
    {
        if (t.is_son_yon_a()) buyCount++;
        else if (t.is_son_yon_s()) sellCount++;
    }
    int half = traders.Count / 2;
    if (buyCount > half) return TradeSignals.Buy;
    if (sellCount > half) return TradeSignals.Sell;
    return TradeSignals.Flat;
}

TradeSignals AllConsensusReference(List<SingleTrader> traders)
{
    int buyCount = 0, sellCount = 0;
    foreach (var t in traders)
    {
        if (t.is_son_yon_a()) buyCount++;
        else if (t.is_son_yon_s()) sellCount++;
    }
    if (traders.Count > 0 && buyCount == traders.Count) return TradeSignals.Buy;
    if (traders.Count > 0 && sellCount == traders.Count) return TradeSignals.Sell;
    return TradeSignals.Flat;
}

TradeSignals AnyConsensusReference(List<SingleTrader> traders)
{
    int buyCount = 0, sellCount = 0;
    foreach (var t in traders)
    {
        if (t.is_son_yon_a()) buyCount++;
        else if (t.is_son_yon_s()) sellCount++;
    }
    if (buyCount > 0 && sellCount > 0) return TradeSignals.Flat;
    if (buyCount > 0) return TradeSignals.Buy;
    if (sellCount > 0) return TradeSignals.Sell;
    return TradeSignals.Flat;
}

// -- Ozel ornek 1: "ilk child kazanir" - diger child'lar tamamen yok sayiliyor.
// Hazir 4 moddan hicbirinin uretemeyecegi bir davranis (onlarin hepsi TUM
// child'larin oyuna bakar) - bu yuzden custom path'in gercekten calistigini
// kanitlamak icin varsayilan aktif kural olarak bu secildi (bkz. sohbet).
// Dogrulama fikri: bu kural altinda mainTrader'in performansi, childTrader_0'in
// SOLO performansina cok yakin cikmali - MultipleTraderStatistics.csv'de
// mainTrader vs childTrader_0 satirlarini karsilastirarak gorulebilir.
TradeSignals FirstChildWinsConsensus(List<SingleTrader> traders)
{
    if (traders.Count == 0) return TradeSignals.Flat;
    var firstChild = traders[0];
    if (firstChild.is_son_yon_a()) return TradeSignals.Buy;
    if (firstChild.is_son_yon_s()) return TradeSignals.Sell;
    return TradeSignals.Flat;
}

// -- Ozel ornek 2: agirlikli oy - child_2'nin (RSI) oyu 3 kat sayilsin.
TradeSignals WeightedConsensus(List<SingleTrader> traders)
{
    int weightedBuy = 0, weightedSell = 0;
    for (int i = 0; i < traders.Count; i++)
    {
        int weight = (i == 2) ? 3 : 1;
        if (traders[i].is_son_yon_a()) weightedBuy += weight;
        else if (traders[i].is_son_yon_s()) weightedSell += weight;
    }
    if (weightedBuy > weightedSell) return TradeSignals.Buy;
    if (weightedSell > weightedBuy) return TradeSignals.Sell;
    return TradeSignals.Flat;
}

// -- Ozel ornek 3: kosullu filtre - sadece child_0, child_1 VE child_2 (Most+MA+RSI) ayni yonde ise o yon.
TradeSignals AllThreeAgreeConsensus(List<SingleTrader> traders)
{
    if (traders.Count >= 3 && traders[0].is_son_yon_a() && traders[1].is_son_yon_a() && traders[2].is_son_yon_a())
        return TradeSignals.Buy;
    if (traders.Count >= 3 && traders[0].is_son_yon_s() && traders[1].is_son_yon_s() && traders[2].is_son_yon_s())
        return TradeSignals.Sell;
    return TradeSignals.Flat;
}

// ---- AKTIF KURAL (buradan degistir) ------------------------------------------
multipleTrader.CustomConsensusFunc = FirstChildWinsConsensus;
// multipleTrader.CustomConsensusFunc = NetConsensusReference;       // hazir Net ile ayni
// multipleTrader.CustomConsensusFunc = MajorityConsensusReference;  // hazir Majority ile ayni
// multipleTrader.CustomConsensusFunc = AllConsensusReference;       // hazir All ile ayni
// multipleTrader.CustomConsensusFunc = AnyConsensusReference;       // hazir Any ile ayni
// multipleTrader.CustomConsensusFunc = WeightedConsensus;
// multipleTrader.CustomConsensusFunc = AllThreeAgreeConsensus;
// -------------------------------------------------------------------------------
Log("\n[CustomConsensus] CustomConsensusFunc atandi: 'ilk child kazanir' kurali aktif.");

// =============================================================================
// 4. Run donguisu
// =============================================================================
int totalBars = data.Count;
Log($"\nCalisiyor... Total bars: {totalBars}");

var sw = Stopwatch.StartNew();
multipleTrader.IsStarted = true;
multipleTrader.IsRunning = true;

for (int i = 0; i < totalBars; i++)
{
    if (IsCancellationRequested)
    {
        Log($"Script iptal edildi, bar {i}/{totalBars}");
        break;
    }
    multipleTrader.Run(i);
}

multipleTrader.IsRunning = false;
multipleTrader.IsStopped = true;
sw.Stop();
Log($"Run tamamlandi: {sw.ElapsedMilliseconds} msec.");

// =============================================================================
// 5. Finalize + Dosyaya yaz (MultipleTraderStatistics.csv karsilastirma icin)
// =============================================================================
Log("\nFinalize ediliyor...");
multipleTrader.Finalize();

multipleTrader.WriteMultipleTraderListsToFiles(AppSettings.LogsDir);
multipleTrader.WriteMultipleTraderStatistics(AppSettings.LogsDir);
mainTrader.WriteStatisticsToFile(AppSettings.LogsDir, AppSettings.ConfigsDir);
foreach (var child in multipleTrader.Traders)
    child.WriteStatisticsToFile(AppSettings.LogsDir, AppSettings.ConfigsDir);

// =============================================================================
// 6. Ozet
// =============================================================================
Log($"\n=== SONUCLAR ===");
Log($"mainTrader (Custom consensus) Ozet : {mainTrader.TaramaOzeti ?? "N/A"}");
SendResult("MainTrader_TaramaOzeti", mainTrader.TaramaOzeti ?? "N/A");
for (int i = 0; i < multipleTrader.Traders.Count; i++)
{
    var child = multipleTrader.Traders[i];
    Log($"childTrader_{i} Ozet : {child.TaramaOzeti ?? "N/A"}");
    SendResult($"Child_{i}_TaramaOzeti", child.TaramaOzeti ?? "N/A");
}
Log($"\nKarsilastirma dosyasi: {Path.Combine(AppSettings.LogsDir, multipleTrader.MultipleTraderStatisticsCsvFileName)}");
Log("  -> mainTrader satiri ile childTrader_0 satirini karsilastir: neredeyse ayni cikmali.");

multipleTrader.Dispose();
reader.Dispose();
Log("=== Bitti ===");
"OK"
