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
// 1. Indicators + 23 child strateji (trend-flip ailesi: Most/MA/RSI/OTT/SuperTrend/ParabolicSAR
// + kalan tum Simple*Strategy'ler: ADX/DI/MACD/Stochastic/Bollinger/ATR/CMF/MFI/Kairi/Momentum/
// HHVLLV/HYLY/Ichimoku/MavilimW/PMax/TillsonT3/AlphaTrend)
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
var childStrategy3 = algoTrader.CreateStrategyFromRegistry(data, indicators, "SimpleOTTStrategy",
    new Dictionary<string, object> { ["period"] = 2, ["percent"] = 1.4, ["ottMaMethod"] = "VIDYA", ["priceSource"] = "Close", ["signalModeIndex"] = 0 });
var childStrategy4 = algoTrader.CreateStrategyFromRegistry(data, indicators, "SimpleSuperTrendStrategy",
    new Dictionary<string, object> { ["period"] = 10, ["multiplier"] = 3.0, ["priceSource"] = "Close", ["signalModeIndex"] = 0 });
var childStrategy5 = algoTrader.CreateStrategyFromRegistry(data, indicators, "SimpleParabolicSARStrategy",
    new Dictionary<string, object> { ["step"] = 0.02, ["max"] = 0.2, ["priceSource"] = "Close", ["signalModeIndex"] = 0 });
var childStrategy6 = algoTrader.CreateStrategyFromRegistry(data, indicators, "SimpleADXStrategy",
    new Dictionary<string, object> { ["period"] = 14, ["adxThreshold"] = 25, ["signalModeIndex"] = 0 });
var childStrategy7 = algoTrader.CreateStrategyFromRegistry(data, indicators, "SimpleDIStrategy",
    new Dictionary<string, object> { ["period"] = 14, ["signalModeIndex"] = 0 });
var childStrategy8 = algoTrader.CreateStrategyFromRegistry(data, indicators, "SimpleMACDStrategy",
    new Dictionary<string, object> { ["fastPeriod"] = 12, ["slowPeriod"] = 26, ["signalPeriod"] = 9, ["priceSource"] = "Close", ["signalModeIndex"] = 0 });
var childStrategy9 = algoTrader.CreateStrategyFromRegistry(data, indicators, "SimpleStochasticStrategy",
    new Dictionary<string, object> { ["kPeriod"] = 14, ["dPeriod"] = 3, ["centerLine"] = 50, ["signalModeIndex"] = 0 });
var childStrategy10 = algoTrader.CreateStrategyFromRegistry(data, indicators, "SimpleBollingerStrategy",
    new Dictionary<string, object> { ["period"] = 20, ["multiplier"] = 2.0, ["priceSource"] = "Close", ["signalModeIndex"] = 0 });
var childStrategy11 = algoTrader.CreateStrategyFromRegistry(data, indicators, "SimpleATRStrategy",
    new Dictionary<string, object> { ["atrPeriod"] = 14, ["maPeriod"] = 20, ["multiplier"] = 2.0, ["priceSource"] = "Close", ["signalModeIndex"] = 0 });
var childStrategy12 = algoTrader.CreateStrategyFromRegistry(data, indicators, "SimpleCMFStrategy",
    new Dictionary<string, object> { ["period"] = 20, ["positiveThreshold"] = 0.1, ["negativeThreshold"] = -0.1, ["signalModeIndex"] = 0 });
var childStrategy13 = algoTrader.CreateStrategyFromRegistry(data, indicators, "SimpleMFIStrategy",
    new Dictionary<string, object> { ["period"] = 14, ["oversold"] = 20, ["overbought"] = 80, ["signalModeIndex"] = 0 });
var childStrategy14 = algoTrader.CreateStrategyFromRegistry(data, indicators, "SimpleKairiStrategy",
    new Dictionary<string, object> { ["period"] = 20, ["positiveThreshold"] = 5, ["negativeThreshold"] = -5, ["priceSource"] = "Close", ["signalModeIndex"] = 0 });
var childStrategy15 = algoTrader.CreateStrategyFromRegistry(data, indicators, "SimpleMomentumStrategy",
    new Dictionary<string, object> { ["period"] = 12, ["positiveThreshold"] = 0, ["negativeThreshold"] = 0, ["priceSource"] = "Close", ["signalModeIndex"] = 0 });
var childStrategy16 = algoTrader.CreateStrategyFromRegistry(data, indicators, "SimpleHHVLLVStrategy",
    new Dictionary<string, object> { ["period"] = 20, ["priceSource"] = "Close", ["signalModeIndex"] = 0 });
var childStrategy17 = algoTrader.CreateStrategyFromRegistry(data, indicators, "SimpleHYLYStrategy",
    new Dictionary<string, object> { ["period"] = 20, ["threshold"] = 80, ["priceSource"] = "Close", ["signalModeIndex"] = 0 });
var childStrategy18 = algoTrader.CreateStrategyFromRegistry(data, indicators, "SimpleIchimokuStrategy",
    new Dictionary<string, object> { ["tenkanPeriod"] = 9, ["kijunPeriod"] = 26, ["senkouPeriod"] = 52, ["signalModeIndex"] = 0 });
var childStrategy19 = algoTrader.CreateStrategyFromRegistry(data, indicators, "SimpleMavilimWStrategy",
    new Dictionary<string, object> { ["param1"] = 3, ["param2"] = 5, ["signalModeIndex"] = 0 });
var childStrategy20 = algoTrader.CreateStrategyFromRegistry(data, indicators, "SimplePMaxStrategy",
    new Dictionary<string, object> { ["atrPeriod"] = 10, ["multiplier"] = 3.0, ["maPeriod"] = 10, ["pmaxMaMethod"] = "EMA", ["signalModeIndex"] = 0 });
var childStrategy21 = algoTrader.CreateStrategyFromRegistry(data, indicators, "SimpleTillsonT3Strategy",
    new Dictionary<string, object> { ["period"] = 5, ["priceSource"] = "Close", ["signalModeIndex"] = 0 });
var childStrategy22 = algoTrader.CreateStrategyFromRegistry(data, indicators, "SimpleAlphaTrendStrategy",
    new Dictionary<string, object> { ["atrPeriod"] = 14, ["coefficient"] = 1.0, ["momentumPeriod"] = 14, ["useMFI"] = true, ["signalModeIndex"] = 0 });

// =============================================================================
// 2. MultipleTrader'i manuel kur (mainTrader + 23 child)
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
AddChild(3, childStrategy3);
AddChild(4, childStrategy4);
AddChild(5, childStrategy5);
AddChild(6, childStrategy6);
AddChild(7, childStrategy7);
AddChild(8, childStrategy8);
AddChild(9, childStrategy9);
AddChild(10, childStrategy10);
AddChild(11, childStrategy11);
AddChild(12, childStrategy12);
AddChild(13, childStrategy13);
AddChild(14, childStrategy14);
AddChild(15, childStrategy15);
AddChild(16, childStrategy16);
AddChild(17, childStrategy17);
AddChild(18, childStrategy18);
AddChild(19, childStrategy19);
AddChild(20, childStrategy20);
AddChild(21, childStrategy21);
AddChild(22, childStrategy22);

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

// -- Ozel ornek 3: kosullu filtre - sadece trend-flip ailesindeki ilk 6 child (Most+MA+RSI+OTT+
// SuperTrend+ParabolicSAR, child_0..5) hepsi ayni yonde ise o yon. Artik toplam 23 child var
// (bkz. yukarisi) - bu ornek BILEREK sadece ilk 6'ya bakiyor: hepsinin (23'unun) ayni anda
// ayni yonde olmasini istemek pratikte AllConsensusReference'in (yukaridaki, generic "All" modu)
// aynisi olurdu - custom path'in ayirt edici noktasi index'e ozel bir ALT KUME secebilmek.
TradeSignals AllOfFirstSixAgreeConsensus(List<SingleTrader> traders)
{
    if (traders.Count >= 6 && traders[0].is_son_yon_a() && traders[1].is_son_yon_a() && traders[2].is_son_yon_a() && traders[3].is_son_yon_a() && traders[4].is_son_yon_a() && traders[5].is_son_yon_a())
        return TradeSignals.Buy;
    if (traders.Count >= 6 && traders[0].is_son_yon_s() && traders[1].is_son_yon_s() && traders[2].is_son_yon_s() && traders[3].is_son_yon_s() && traders[4].is_son_yon_s() && traders[5].is_son_yon_s())
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
// multipleTrader.CustomConsensusFunc = AllOfFirstSixAgreeConsensus;
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
