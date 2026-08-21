# MultipleTrader — Çoklu Strateji + Consensus (Menü [3])

> [Class Reference](../01-class-reference.md) setinin bir parçası — bu sınıf ayrı dosyada,
> çünkü [SingleTrader](02-singletrader.md) ve [StockDataReader](09-stockdatareader.md) gibi
> diğer sınıflardan çok daha derin işlendi (tam sınıf iskeleti, consensus motorunun davranış
> tablosu, gerçek orkestrasyon kaynağı, instantiation envanteri, kullanım haritası). Yöntem:
> [06-class-doc-method.md](../06-class-doc-method.md).

### Dosyalar

- `src/AlgoTrade.Core/Trading/Traders/MultipleTrader.cs` (833 satır)
- `src/AlgoTrade.Core/Trading/Traders/SingleTrader.cs` (mainTrader + her child bir
  `SingleTrader` instance'ı — bkz. [SingleTrader dokümanı](02-singletrader.md), bu sayfa onu
  önceden anlamış olduğunu varsayıyor)
- `src/AlgoTrade.Core/Trading/Core/TradeSignals.cs` — `BuildConsensusSignal()`'ın döndürdüğü
  enum (`None`/`Buy`/`Sell`/`TakeProfit`/`StopLoss`/`Flat`/`Skip`)

### Rolü

- Birden fazla child `SingleTrader`'ı **her biri kendi sinyaliyle gerçekten trade ederek** aynı
  bar üzerinde çalıştırır, sinyallerini bir "consensus" kuralıyla birleştirip tek bir `mainTrader`
  (id=-1) ile ayrı bir gerçek emir üretir.
- Önemli: child'lar sinyal üretip pasif kalmaz — her biri `SingleTrader.Run()`'ın aynısını
  çalıştırıp **kendi defterinde** gerçek trade yapar (bkz. [Run()](#run--çocuk-traderları--maintrader-pipeline)
  altında, `trader.Run(i)` çağrısı). Yani her child'ın kendi `WriteStatisticsToFile()` çıktısı, o
  stratejiyi TEK BAŞINA çalıştırsaydın alacağın sonucun birebir aynısıdır.
- Kendi başına bir "modül kompozisyonu" yok (SingleTrader'daki 9 modül gibi) — bunun yerine
  **trader kompozisyonu**: `_mainTrader` (private, `GetMainTrader()` ile erişilir) + `Traders`
  (public `List<SingleTrader>`, child'lar).
- `MarketDataProvider`'dan TÜREMEZ (SingleTrader/StockDataReader'ın aksine) — kendi `Data`/
  `Indicators`/`Logger` property'lerini doğrudan tutar, `IDisposable`'ı implement etmez ama
  `Dispose()` metodu var (arayüzü resmi olarak uygulamıyor).

### Ne zaman kullanılır

- (a) Gerçekten birden fazla stratejiyi birleştirip TEK bir consensus sinyaliyle trade etmek
  istediğinde (Console `[3]`/`[6]`).
- (b) Aynı sembolde birden fazla stratejinin performansını YAN YANA karşılaştırmak istediğinde
  (`WriteMultipleTraderStatistics()` ile — mainTrader satırını yok sayıp child satırlarına
  bakarsın).
- (c) Hazır 4 consensus modundan (`Net`/`Majority`/`All`/`Any`) hiçbiri yetmiyorsa, script'ten
  `CustomConsensusFunc` ile kendi kuralını yazmak istediğinde — bkz. [Tipik Kullanım — Script'ten
  Çağrılma](#tipik-kullanım--scriptten-çağrılma-customconsensusfunc-örneği).

### Sınıf İskeleti (ilk bakış)

Aşağıdaki bloktaki metod gövdeleri kaldırılmış — sadece alan/property/metod imzaları (public +
private, hepsi), gerçek kaynağın (`MultipleTrader.cs`) sırasıyla birebir aynı. `BuildConsensusSignal()`
istisna: gövdesi (asıl consensus mantığı) [Consensus — `BuildConsensusSignal()`](#consensus--buildconsensussignal)
altında tam olarak ayrıca gösteriliyor.

```csharp linenums="1"
public class MultipleTrader
{
    public int Id { get; private set; }
    public List<StockData> Data { get; private set; }
    public IndicatorManager Indicators { get; private set; }
    public LogManager? Logger { get; private set; }

    public List<SingleTrader> Traders { get; private set; }
    public bool IsInitialized { get; private set; }
    public int CurrentIndex { get; private set; }

    // ---- State flags ----
    public bool IsStarted { get; set; }
    public bool IsRunning { get; set; }
    public bool IsStopped { get; set; }
    public bool IsStopRequested { get; set; }

    private SingleTrader _mainTrader;

    public bool DynamicPositionSizeEnabled { get; set; } = false;   // bkz. Not — TODO, işlevsiz

    public string ConsensusMode { get; set; } = "Net";
    public int ConsensusMinNetCount { get; set; } = 1;
    public Func<List<SingleTrader>, TradeSignals>? CustomConsensusFunc { get; set; }

    public Action<MultipleTrader, int, int>? OnProgress { get; set; }

    public bool SaveStatisticsToFile { get; set; } = true;
    public bool WriteChildTradersDataToFiles { get; set; } = false;

    // ---- Bar-bar liste çıktısı (Lists Export) ----
    public string MultipleTraderListsTxtFileName { get; set; } = "MultipleTraderLists.txt";
    public string MultipleTraderListsCsvFileName { get; set; } = "MultipleTraderLists.csv";
    public bool SaveMultipleTraderListsTxtEnabled { get; set; } = true;
    public bool SaveMultipleTraderListsCsvEnabled { get; set; } = true;

    // ---- Trader-bazlı özet karşılaştırma (Statistics Export) ----
    public string MultipleTraderStatisticsTxtFileName { get; set; } = "MultipleTraderStatistics.txt";
    public string MultipleTraderStatisticsCsvFileName { get; set; } = "MultipleTraderStatistics.csv";
    public bool SaveMultipleTraderStatisticsTxtEnabled { get; set; } = true;
    public bool SaveMultipleTraderStatisticsCsvEnabled { get; set; } = true;

    public bool PlotEnabled { get; set; } = false;

    // ---- Kurulum ----
    public MultipleTrader();
    public MultipleTrader(int id, List<StockData> data, IndicatorManager indicators, LogManager? logger);

    public void Initialize(List<StockData> data);
    public void AddTrader(SingleTrader trader);
    public void Reset();
    public void Init();

    // ---- Consensus & Run ----
    public TradeSignals BuildConsensusSignal();
    private TradeSignals BuildNetConsensus(int buyCount, int sellCount);
    public void Run(int i);

    // ---- Finalize ----
    public void Finalize();   // #pragma warning disable/restore CS0465

    // ---- Main Trader Methods ----
    public SingleTrader GetMainTrader();
    public void SetCallbacks(
        Action<SingleTrader, int>? onReset = null, Action<SingleTrader, int>? onInit = null,
        Action<SingleTrader, int>? onRun = null, Action<SingleTrader, int>? onFinal = null,
        Action<SingleTrader, int>? onBeforeOrders = null, Action<SingleTrader, string, int>? onNotifySignal = null,
        Action<SingleTrader, int>? onAfterOrders = null, Action<SingleTrader, int, int, double>? onProgress = null,
        Action<SingleTrader>? onApplyUserFlags = null);
    public void Stop();

    // ---- MultipleTrader Lists Export ----
    public void WriteMultipleTraderListsToFiles(string logDir);
    private void WriteMultipleTraderListsToTxt(string logDir);
    private void WriteHeaderTxt(System.IO.StreamWriter writer);
    private void WriteBarDataTxt(System.IO.StreamWriter writer, int barIndex);
    private void WriteMultipleTraderListsToCsv(string logDir);
    private void WriteHeaderCsv(System.IO.StreamWriter writer);
    private void WriteBarDataCsv(System.IO.StreamWriter writer, int barIndex);
    private string GetYon(SingleTrader trader, int barIndex);
    private double GetSeviye(SingleTrader trader, int barIndex);
    private double GetSinyal(SingleTrader trader, int barIndex);

    // ---- MultipleTrader Statistics Export ----
    public void WriteMultipleTraderStatistics(string logDir);
    private List<(string Name, Dictionary<string, string> Summary)> BuildStatisticsRows();
    private void WriteMultipleTraderStatisticsToTxt(string logDir);
    private void WriteMultipleTraderStatisticsToCsv(string logDir);

    // ---- Dispose ----
    public void Dispose();
}
```

### Üye İndeksi — Hangisi Nerede Anlatılıyor

Yukarıdaki iskeletteki her üye, kaynak sırasıyla, `MultipleTrader::Üye` notasyonuyla — aşağıdaki
Public API bölümlerinden hangisinde detaylandırıldığına link veriyor. **#** kolonu, yukarıdaki
kod bloğunun (`linenums="1"`) gerçek satır numarasıyla birebir eşleşiyor.

| # | Üye | Tür | Detay |
|---|---|---|---|
| 3 | `MultipleTrader::Id` | public property | [Kimlik ve Kurulum](#kimlik-ve-kurulum) |
| 4 | `MultipleTrader::Data` | public property | [Kimlik ve Kurulum](#kimlik-ve-kurulum) |
| 5 | `MultipleTrader::Indicators` | public property | [Kimlik ve Kurulum](#kimlik-ve-kurulum) |
| 6 | `MultipleTrader::Logger` | public property | [Kimlik ve Kurulum](#kimlik-ve-kurulum) |
| 8 | `MultipleTrader::Traders` | public property | [Kimlik ve Kurulum](#kimlik-ve-kurulum) — child'ların listesi |
| 9 | `MultipleTrader::IsInitialized` | public property | [Kimlik ve Kurulum](#kimlik-ve-kurulum) |
| 10 | `MultipleTrader::CurrentIndex` | public property | [Kimlik ve Kurulum](#kimlik-ve-kurulum) — bkz. Not, fiilen güncellenmiyor |
| 13 | `MultipleTrader::IsStarted` | public property | [Run() — Çocuk Trader'ları + mainTrader Pipeline](#run--çocuk-traderları--maintrader-pipeline) |
| 14 | `MultipleTrader::IsRunning` | public property | [Run() — Çocuk Trader'ları + mainTrader Pipeline](#run--çocuk-traderları--maintrader-pipeline) |
| 15 | `MultipleTrader::IsStopped` | public property | [Run() — Çocuk Trader'ları + mainTrader Pipeline](#run--çocuk-traderları--maintrader-pipeline) |
| 16 | `MultipleTrader::IsStopRequested` | public property | [Main Trader Methods — `Stop()`](#maintrader-yardımcıları-getmaintrader--setcallbacks--stop) |
| 18 | `MultipleTrader::_mainTrader` | private field | [Kimlik ve Kurulum](#kimlik-ve-kurulum) |
| 20 | `MultipleTrader::DynamicPositionSizeEnabled` | public property | Ölü kod — bkz. [Not](#run--çocuk-traderları--maintrader-pipeline) |
| 22 | `MultipleTrader::ConsensusMode` | public property | [Consensus — `BuildConsensusSignal()`](#consensus--buildconsensussignal) |
| 23 | `MultipleTrader::ConsensusMinNetCount` | public property | [Consensus — `BuildConsensusSignal()`](#consensus--buildconsensussignal) |
| 24 | `MultipleTrader::CustomConsensusFunc` | public property | [Consensus — `BuildConsensusSignal()`](#consensus--buildconsensussignal) |
| 26 | `MultipleTrader::OnProgress` | public property (delegate) | Ölü kod — bkz. [Not](#kullanım-haritası), hiçbir yerden `Invoke` edilmiyor |
| 28 | `MultipleTrader::SaveStatisticsToFile` | public property | [Çağrı Zinciri](#çağrı-zinciri--menüden-çağrılma-programcs--algotrader--multipletrader) |
| 29 | `MultipleTrader::WriteChildTradersDataToFiles` | public property | [Bar-bar Liste Çıktısı](#bar-bar-liste-çıktısı-writemultipletraderliststofiles) |
| 32 | `MultipleTrader::MultipleTraderListsTxtFileName` | public property | [Bar-bar Liste Çıktısı](#bar-bar-liste-çıktısı-writemultipletraderliststofiles) |
| 33 | `MultipleTrader::MultipleTraderListsCsvFileName` | public property | [Bar-bar Liste Çıktısı](#bar-bar-liste-çıktısı-writemultipletraderliststofiles) |
| 34 | `MultipleTrader::SaveMultipleTraderListsTxtEnabled` | public property | [Bar-bar Liste Çıktısı](#bar-bar-liste-çıktısı-writemultipletraderliststofiles) |
| 35 | `MultipleTrader::SaveMultipleTraderListsCsvEnabled` | public property | [Bar-bar Liste Çıktısı](#bar-bar-liste-çıktısı-writemultipletraderliststofiles) |
| 38 | `MultipleTrader::MultipleTraderStatisticsTxtFileName` | public property | [Trader-bazlı Özet Karşılaştırma](#trader-bazlı-özet-karşılaştırma-writemultipletraderstatistics) |
| 39 | `MultipleTrader::MultipleTraderStatisticsCsvFileName` | public property | [Trader-bazlı Özet Karşılaştırma](#trader-bazlı-özet-karşılaştırma-writemultipletraderstatistics) |
| 40 | `MultipleTrader::SaveMultipleTraderStatisticsTxtEnabled` | public property | [Trader-bazlı Özet Karşılaştırma](#trader-bazlı-özet-karşılaştırma-writemultipletraderstatistics) |
| 41 | `MultipleTrader::SaveMultipleTraderStatisticsCsvEnabled` | public property | [Trader-bazlı Özet Karşılaştırma](#trader-bazlı-özet-karşılaştırma-writemultipletraderstatistics) |
| 43 | `MultipleTrader::PlotEnabled` | public property | [Kullanım Haritası](#kullanım-haritası) — `mainTrader.PlotEnabled` tercih ediliyor, bu hiç okunmuyor |
| 46 | `MultipleTrader::MultipleTrader()` | constructor (parametresiz) | [Kimlik ve Kurulum](#kimlik-ve-kurulum) |
| 47 | `MultipleTrader::MultipleTrader(id, data, indicators, logger)` | constructor | [Kimlik ve Kurulum](#kimlik-ve-kurulum) |
| 49 | `MultipleTrader::Initialize(data)` | public method | [Kimlik ve Kurulum](#kimlik-ve-kurulum) — bkz. Not, `Data`'yı SIFIRLAR |
| 50 | `MultipleTrader::AddTrader(trader)` | public method | [Kimlik ve Kurulum](#kimlik-ve-kurulum) |
| 51 | `MultipleTrader::Reset()` | public method | [Kimlik ve Kurulum](#kimlik-ve-kurulum) — bkz. Not, child'lara dokunmuyor |
| 52 | `MultipleTrader::Init()` | public method | [Kimlik ve Kurulum](#kimlik-ve-kurulum) — bkz. Not, boş döngü |
| 55 | `MultipleTrader::BuildConsensusSignal()` | public method | [Consensus — `BuildConsensusSignal()`](#consensus--buildconsensussignal) |
| 56 | `MultipleTrader::BuildNetConsensus(...)` | private method | [Consensus — `BuildConsensusSignal()`](#consensus--buildconsensussignal) |
| 57 | `MultipleTrader::Run(i)` | public method | [Run() — Çocuk Trader'ları + mainTrader Pipeline](#run--çocuk-traderları--maintrader-pipeline) |
| 60 | `MultipleTrader::Finalize()` | public method | [Finalize()](#finalize) |
| 63 | `MultipleTrader::GetMainTrader()` | public method | [mainTrader Yardımcıları](#maintrader-yardımcıları-getmaintrader--setcallbacks--stop) |
| 64 | `MultipleTrader::SetCallbacks(...)` | public method | [mainTrader Yardımcıları](#maintrader-yardımcıları-getmaintrader--setcallbacks--stop) |
| 70 | `MultipleTrader::Stop()` | public method | [mainTrader Yardımcıları](#maintrader-yardımcıları-getmaintrader--setcallbacks--stop) |
| 73 | `MultipleTrader::WriteMultipleTraderListsToFiles(logDir)` | public method | [Bar-bar Liste Çıktısı](#bar-bar-liste-çıktısı-writemultipletraderliststofiles) |
| 74 | `MultipleTrader::WriteMultipleTraderListsToTxt(logDir)` | private method | [Bar-bar Liste Çıktısı](#bar-bar-liste-çıktısı-writemultipletraderliststofiles) — iç yardımcı, ayrıca anlatılmıyor |
| 75 | `MultipleTrader::WriteHeaderTxt(writer)` | private method | [Bar-bar Liste Çıktısı](#bar-bar-liste-çıktısı-writemultipletraderliststofiles) — iç yardımcı |
| 76 | `MultipleTrader::WriteBarDataTxt(writer, barIndex)` | private method | [Bar-bar Liste Çıktısı](#bar-bar-liste-çıktısı-writemultipletraderliststofiles) — iç yardımcı |
| 77 | `MultipleTrader::WriteMultipleTraderListsToCsv(logDir)` | private method | [Bar-bar Liste Çıktısı](#bar-bar-liste-çıktısı-writemultipletraderliststofiles) — iç yardımcı |
| 78 | `MultipleTrader::WriteHeaderCsv(writer)` | private method | [Bar-bar Liste Çıktısı](#bar-bar-liste-çıktısı-writemultipletraderliststofiles) — iç yardımcı |
| 79 | `MultipleTrader::WriteBarDataCsv(writer, barIndex)` | private method | [Bar-bar Liste Çıktısı](#bar-bar-liste-çıktısı-writemultipletraderliststofiles) — iç yardımcı |
| 80 | `MultipleTrader::GetYon(trader, barIndex)` | private method | [Bar-bar Liste Çıktısı](#bar-bar-liste-çıktısı-writemultipletraderliststofiles) — iç yardımcı |
| 81 | `MultipleTrader::GetSeviye(trader, barIndex)` | private method | [Bar-bar Liste Çıktısı](#bar-bar-liste-çıktısı-writemultipletraderliststofiles) — iç yardımcı |
| 82 | `MultipleTrader::GetSinyal(trader, barIndex)` | private method | [Bar-bar Liste Çıktısı](#bar-bar-liste-çıktısı-writemultipletraderliststofiles) — iç yardımcı |
| 85 | `MultipleTrader::WriteMultipleTraderStatistics(logDir)` | public method | [Trader-bazlı Özet Karşılaştırma](#trader-bazlı-özet-karşılaştırma-writemultipletraderstatistics) |
| 86 | `MultipleTrader::BuildStatisticsRows()` | private method | [Trader-bazlı Özet Karşılaştırma](#trader-bazlı-özet-karşılaştırma-writemultipletraderstatistics) — iç yardımcı |
| 87 | `MultipleTrader::WriteMultipleTraderStatisticsToTxt(logDir)` | private method | [Trader-bazlı Özet Karşılaştırma](#trader-bazlı-özet-karşılaştırma-writemultipletraderstatistics) — iç yardımcı |
| 88 | `MultipleTrader::WriteMultipleTraderStatisticsToCsv(logDir)` | private method | [Trader-bazlı Özet Karşılaştırma](#trader-bazlı-özet-karşılaştırma-writemultipletraderstatistics) — iç yardımcı |
| 91 | `MultipleTrader::Dispose()` | public method | [Kimlik ve Kurulum](#kimlik-ve-kurulum) |

## Public API

### Kimlik ve Kurulum

- `MultipleTrader()` — parametresiz constructor: sadece `Traders = new List<SingleTrader>()`,
  `IsInitialized = false`. `_mainTrader` bu yolla **hiç yaratılmaz** — bu constructor'ı kullanan
  tek kod yok gibi görünüyor (bkz. [Kullanım Haritası](#kullanım-haritası)); gerçek kullanım hep
  parametreli overload üzerinden.
- `MultipleTrader(id, data, indicators, logger)` — asıl kullanılan constructor: `Id`/`Data`/
  `Indicators`/`Logger`'ı atar, `Traders` listesini yaratır, **`_mainTrader = new SingleTrader(-1,
  "mainTrader", data, indicators, logger)`** ile mainTrader'ı hemen kurar, `IsInitialized = true`.
- `Initialize(data)` — `data` boşsa `ArgumentException`; `Data`'yı YENİDEN atar, `CurrentIndex = 0`,
  `IsInitialized = true`. **Constructor zaten `Data`'yı ayarladığı için normal akışta bu metoda
  hiç ihtiyaç yok** — `AddTrader()` (`IsInitialized` guard'ı hariç) çağırmadan önce gerekmiyor.
- `AddTrader(trader)` — `IsInitialized` değilse `InvalidOperationException`. `Traders.Add(trader)`;
  `trader.IsInitialized` `false` ise `trader.SetData(Data)` çağrılır (ama pratikte her zaman
  `createChildTraders()`/manuel script akışında child zaten `Init()` edilmiş halde ekleniyor,
  bkz. [Çağrı Zinciri](#çağrı-zinciri--menüden-çağrılma-programcs--algotrader--multipletrader)).
- `Reset()` — `CurrentIndex = 0`, state flag'lerini (`IsStarted`/`IsRunning`/`IsStopped`/
  `IsStopRequested`) sıfırlar. `foreach (var trader in Traders) { }` döngüsü **boş gövde** —
  child trader'ların kendi `Reset()`'i burada ÇAĞRILMIYOR (her child kendi `Reset()`'ini
  `createChildTraders()`/script akışında ayrı ayrı, kendisi çağırıyor).
- `Init()` — `CurrentIndex = 0`; `foreach` döngüsü içindeki `trader.Init()` çağrısı **yorum
  satırı** — kod içi yorumda gerekçesi açık: "trader'ların initleri bağımsız bir şekilde, daha
  önceden çağrılıyor, burada tekrar çağrılmasına gerek yok ... 2 kere çağrılmış olur". Yani bu
  metod fiilen **hiçbir şey yapmıyor** (`CurrentIndex` sıfırlamak dışında) — child'ların `Init()`'i
  `AddTrader()`'dan önce, çağıran kod tarafından yapılmış olmalı.
- `Dispose()` — `_mainTrader?.Dispose()` + `_mainTrader = null`, sonra her `trader.Dispose()`,
  `Traders.Clear()`. `IDisposable`'ı resmi olarak implement etmiyor (arayüz yok class
  tanımında) ama desen aynı.

### Consensus — `BuildConsensusSignal()`

```csharp linenums="1"
public TradeSignals BuildConsensusSignal()
{
    if (CustomConsensusFunc != null)
    {
        TradeSignals customResult = CustomConsensusFunc(Traders);
        if (customResult == TradeSignals.Buy || customResult == TradeSignals.Sell)
            LogManager.Log(LogLevel.Debug, LogSinks.File, $"MultipleTrader consensus [Custom]: -> {customResult}");
        return customResult;
    }

    int buyCount = 0, sellCount = 0, flatCount = 0;

    foreach (var trader in Traders)
    {
        if (trader.is_son_yon_a()) buyCount++;
        else if (trader.is_son_yon_s()) sellCount++;
        else if (trader.is_son_yon_f()) flatCount++;
    }

    TradeSignals result;

    switch (ConsensusMode?.Trim().ToLowerInvariant())
    {
        case "majority":
            int half = Traders.Count / 2;
            result = buyCount > half ? TradeSignals.Buy
                   : sellCount > half ? TradeSignals.Sell
                   : TradeSignals.Flat;
            break;

        case "all":
            result = (Traders.Count > 0 && buyCount == Traders.Count) ? TradeSignals.Buy
                   : (Traders.Count > 0 && sellCount == Traders.Count) ? TradeSignals.Sell
                   : TradeSignals.Flat;
            break;

        case "any":
            result = (buyCount > 0 && sellCount > 0) ? TradeSignals.Flat
                   : buyCount > 0 ? TradeSignals.Buy
                   : sellCount > 0 ? TradeSignals.Sell
                   : TradeSignals.Flat;
            break;

        case "net": case null: case "":
            result = BuildNetConsensus(buyCount, sellCount);
            break;

        default:
            LogManager.LogWarning($"MultipleTrader: taninmayan ConsensusMode '{ConsensusMode}', 'Net' moduna dusuluyor.");
            result = BuildNetConsensus(buyCount, sellCount);
            break;
    }

    if (result == TradeSignals.Buy || result == TradeSignals.Sell)
        LogManager.Log(LogLevel.Debug, LogSinks.File, $"MultipleTrader consensus [{ConsensusMode}]: Buy={buyCount} Sell={sellCount} Flat={flatCount} -> {result}");

    return result;
}

private TradeSignals BuildNetConsensus(int buyCount, int sellCount)
{
    int netSignal = buyCount - sellCount;
    if (netSignal >= ConsensusMinNetCount) return TradeSignals.Buy;
    if (netSignal <= -ConsensusMinNetCount) return TradeSignals.Sell;
    return TradeSignals.Flat;
}
```

- `CustomConsensusFunc` (`Func<List<SingleTrader>, TradeSignals>?`) doluysa **her şeyi baypas
  eder** — hazır `ConsensusMode`/`ConsensusMinNetCount` hiç okunmaz, doğrudan `CustomConsensusFunc(Traders)`
  çağrılır ve sonucu döner. Script'ten atanır (`AppConfig.json`'dan ATANAMAZ — `Func` serialize
  edilemez); tek enjeksiyon yolu [Tipik Kullanım — Script'ten Çağrılma](#tipik-kullanım--scriptten-çağrılma-customconsensusfunc-örneği).
- `ConsensusMode` (case-insensitive, `Trim().ToLowerInvariant()`) 4 hazır modu seçer:

  | Mode | Davranış |
  |---|---|
  | `Net` (varsayılan, `null`/`""` de buraya düşer) | `buyCount - sellCount >= ConsensusMinNetCount` → Buy; `<= -ConsensusMinNetCount` → Sell; aksi → Flat |
  | `Majority` | `buyCount > Traders.Count/2` → Buy; `sellCount > Traders.Count/2` → Sell; aksi → Flat |
  | `All` | Tüm child'lar aynı yönde (`buyCount==Traders.Count` veya `sellCount==Traders.Count`) → o yön; aksi → Flat |
  | `Any` | En az 1 Buy VE en az 1 Sell varsa → Flat (çelişki); sadece Buy varsa → Buy; sadece Sell varsa → Sell; hiçbiri → Flat |
  | Tanınmayan değer | `LogManager.LogWarning(...)` ile uyarı loglanır, `Net`'e düşülür |

- Oy sayımı **sadece son yöne bakar** (`is_son_yon_a/_s/_f()`, bkz. [SingleTrader § Yön
  Sorguları](02-singletrader.md#yön-sorguları)) — child'ın strateji sinyali (`Buy`/`Sell`/`Flat`/
  vb.) değil, child trader'ın O BARDAKİ GERÇEK POZİSYON YÖNÜ oy olarak sayılıyor. Bu, hazır bir
  strateji `Buy` sinyali verse bile child'ın kendi filtreleri (timing/equity curve) o sinyali
  bloklamışsa oyun hâlâ önceki yöne göre sayılacağı anlamına gelir.
- Loglama sadece Buy/Sell sonuçlarında ve sadece `LogSinks.File`'a yazılır (Console'a DEĞİL) —
  kod içi yorum: "bar başına senkron Console.WriteLine, büyük veri setlerinde ... koşum süresini
  dakikalarca uzatan bir darboğazdı."

### Run() — Çocuk Trader'ları + mainTrader Pipeline

```csharp linenums="1"
public void Run(int i)
{
    // ... sayaç değişkenleri (noneSignalCount, alSignalCount, ...) — kullanılmıyor, bkz. Not

    if (i >= Data.Count)
        return;

    // --- Her child'ı GERÇEKTEN çalıştır ---
    foreach (var trader in Traders)
    {
        trader.Run(i);   // ← child'ın kendi SingleTrader.Run() akışı, gerçek trade dahil

        TradeSignals signal = trader.strategySignal;
        // signal'e göre sayaçlar artırılıyor (noneSignalCount vb. — bkz. Not, hiç okunmuyor)
    }

    // --- Consensus üret ---
    TradeSignals consensusSignal = BuildConsensusSignal();

    // TODO: DynamicPositionSizeEnabled — lot büyüklüğü güncelleme (bkz. Not, işlevsiz)

    // --- mainTrader'ı consensus sinyaliyle, SingleTrader.Run()'ı ÇAĞIRMADAN elle çalıştır ---
    _mainTrader.ExecutePreOrderMethods(i);

    if (i < 1)
        return;

    _mainTrader.strategySignal = consensusSignal;
    _mainTrader.MapStrategyCommandsToTradeCommands(_mainTrader.strategySignal);
    _mainTrader.ApplyTimingFilters(i);
    _mainTrader.ApplyEquityCurveFilter(i);
    _mainTrader.ResolveFilterDecisions(i);
    _mainTrader.ExecutePostOrderMethods(i);
}
```

- Her child için `trader.Run(i)` gerçek `SingleTrader.Run()`'dır — [SingleTrader § Run() İç
  Akışı](02-singletrader.md#run-iç-akışı)'ndaki 6 adımlı zincirin (`ExecutePreOrderMethods` →
  `ExecuteStrategy` → ... → `ExecutePostOrderMethods`) TAMAMI çalışır. Child'ın kendi
  `lists`/`status`/`signals` state'i, tek başına çalıştırılmış gibi doğru şekilde birikir.
- mainTrader için AYNI `SingleTrader.Run()` **çağrılmıyor** — bunun yerine `SingleTrader`'ın
  `Run()` içinde zaten yaptığı 6 adımlık pipeline'ın 5'i (strateji/sorgu adımları hariç,
  consensus zaten stratejinin yerini alıyor) burada elle tekrarlanıyor. Bu, `SingleTrader.Run()`'ın
  `OnRun?.Invoke(this, 0)`/`OnRun?.Invoke(this, 1)` satırlarının (bkz.
  [SingleTrader § Event'ler](02-singletrader.md#eventler)) mainTrader için hiç çalışmadığı
  anlamına geliyor — pratik etkisi sıfır çünkü `OnSingleTraderRun` callback'i zaten boş gövdeli
  (bkz. [SingleTrader § Callback'lerin Gerçek Gövdeleri](02-singletrader.md#callbacklerin-gerçek-gövdeleri-algotradercs158-223)),
  ama script'ten kendi `OnRun` callback'ini bağlayan biri mainTrader için hiç tetiklenmediğini
  fark etmeyebilir.

> **Not — `Run(i)`'nin başındaki 7 sayaç değişkeni (`noneSignalCount`, `alSignalCount`,
> `satSignalCount`, `flatOlSignalCount`, `passGecSignalCount`, `karAlSignalCount`,
> `zararKesSignalCount`) hesaplanıyor ama hiçbir yerde okunmuyor:** `MultipleTrader.cs:307-364`
> — her child'ın `trader.strategySignal`'ine bakıp 7 sayacı tek tek artırıyor, ama fonksiyon
> bunlardan hiçbirini `return` etmiyor, log'lamıyor, bir alana da yazmıyor — hesaplanıp anında
> atılan, tamamen etkisiz kod. `BuildConsensusSignal()`'ın kendi içindeki `buyCount`/`sellCount`/
> `flatCount` (farklı, ayrı sayaçlar — `is_son_yon_*()` tabanlı) bunların yerine geçmiş
> görünüyor; bu 7'li muhtemelen daha eski bir consensus tasarımından kalma kalıntı.

> **Not — `DynamicPositionSizeEnabled` tanımlı ama işlevsiz:** `Run(i)` içindeki
> `// TODO: DynamicPositionSizeEnabled - lot büyüklüğü güncelleme` yorumunun altında hiçbir kod
> yok — sınıf yorumundaki "Dinamik Lot (DynamicPositionSizeEnabled=true): Consensus sinyalinden
> gelen lot büyüklüğü kullanılır" açıklamasına rağmen, bu flag'i `true` yapmanın **hiçbir**
> davranışsal etkisi yok (`docs/PROJECT_ANALYSIS.md`'de de "gövdesi TODO, işlevsiz" olarak
> işaretli). `PozisyonBuyuklugu` mevcut projede yok — kod yorumuna göre gelecekte eklenmesi
> planlanmış bir özellik için ayrılmış bir yer tutucu.

### Finalize()

```csharp linenums="1"
public void Finalize()
{
    CurrentIndex = 0;
    foreach (var trader in Traders)
    {
        trader.Finalize();
    }

    if (!IsInitialized)
        throw new InvalidOperationException("Trader not initialized");

    LogManager.LogRaw($"\nCalculating statistics...");
    _mainTrader.CalculateStatistics();

    LogManager.LogRaw($"\nCalculating performances...");
    _mainTrader.GetPerformansParams(out double bakiyePuan, out double lotSayisi, out double varlikAdedCarpani);
    _mainTrader.CalculatePerformances(bakiyePuan, lotSayisi, varlikAdedCarpani);
}
```

- Her child'ın `Finalize()`'ı **gerçekten çağrılıyor** (`Reset()`/`Init()`'in aksine) — her
  child'ın kendi `CalculateStatistics()`/`CalculatePerformances()` zinciri (bkz. [SingleTrader
  § Yaşam Döngüsü](02-singletrader.md#yaşam-döngüsü)) burada tetiklenir.
  `IsInitialized` kontrolü child'ların Finalize'ından SONRA yapılıyor — yani `MultipleTrader`
  hiç initialize edilmemiş olsa bile (`Traders` boş değilse) child'lar önce finalize edilir,
  exception ondan sonra fırlar.
- `#pragma warning disable/restore CS0465` — `SingleTrader.Finalize()`'daki ile aynı sebep
  (metod adı `Finalize`, CLR finalizer'ıyla isim çakışması uyarısını bastırır, gerçek bir
  destructor override'ı değil, bkz. [SingleTrader § Yaşam
  Döngüsü](02-singletrader.md#yaşam-döngüsü)).
- mainTrader'ın `GetStatisticsHeaderRow()`/`GetStatisticsDataRow()`'u burada ÇAĞRILMIYOR —
  `RunMultipleTraderWithProgressAsync()`'te bu adım tamamen yorum satırı (`// TODO : Asagisı
  singleTrader e benzer olarak yapılacak`, bkz. [Tam Kaynak](#runmultipletraderwithprogressasync--tam-kaynak-algotradercs1871-2149)
  satır 56-63).

### mainTrader Yardımcıları: `GetMainTrader()` / `SetCallbacks()` / `Stop()`

- `GetMainTrader()` → `SingleTrader` — `_mainTrader`'ı döner; `algoTrader.MultipleTrader.GetMainTrader()`
  Console/script'in mainTrader'a erişim yolu.
- `SetCallbacks(onReset, onInit, onRun, onFinal, onBeforeOrders, onNotifySignal, onAfterOrders,
  onProgress, onApplyUserFlags)` — **hem `_mainTrader` hem `Traders` listesindeki HER child'a**
  aynı callback setini uygular (`_mainTrader.SetCallbacks(...)` + `foreach` ile her child için
  tekrar). `RunMultipleTraderWithProgressAsync()` bu metodu ÇAĞIRMAZ — onun yerine mainTrader'a
  `mainTrader.ClearCallbacks().SetCallbacks(...)` (SingleTrader'ın kendi metodu) ve her child'a
  `createChildTraders()` içinde ayrı ayrı aynısı uygulanır; yani bu `MultipleTrader::SetCallbacks`
  toplu-uygulama kolaylığı Console akışında kullanılmıyor, sadece script'ten manuel kurulumda
  (`multipleTrader.SetCallbacks(...)` tek satırda hepsine) işe yarar.
- `Stop()` — `IsRunning` ise `IsStopRequested = true` + log. Child'ların kendi
  `IsStopRequested`'ını AYRI AYRI set etmiyor — `Run(i)`'nin bar döngüsü
  (`RunMultipleTraderWithProgressAsync()`'teki `for` döngüsü) her bar başında
  `multipleTrader.IsStopRequested`'a bakıyor, döngü kırılınca ne mainTrader'ın ne child'ların
  kendi `IsStopRequested` bayrağı ayrıca set edilmiyor (SingleTrader'ın tek başına
  çalıştığındaki simetrik davranışından farklı, ama pratik etkisi yok çünkü döngü zaten durmuş
  oluyor).

### Bar-bar Liste Çıktısı: `WriteMultipleTraderListsToFiles(...)`

- `WriteMultipleTraderListsToFiles(logDir)` — `SaveMultipleTraderListsTxtEnabled`/`CsvEnabled`
  bayraklarına göre `WriteMultipleTraderListsToTxt`/`ToCsv`'yi çağırır. **Bar-bar** (her bar için
  tüm trader'ların Yön/Seviye/Sinyal'i yan yana) rapor — **performans raporu DEĞİL**.
- TXT formatı sabit-genişlik kolonlu (`BarNo|Date|Time|Open|High|Low|Close|Volume|Size|Change|Change%`
  + `MainYon|MainSvy|MainSny` + her child için `T{n}Yon|T{n}Svy|T{n}Sny`), CSV `;`-ayraçlı aynı
  kolonlarla (`MainTrader_Yon` vb. tam isimlerle).
  `GetYon`/`GetSeviye`/`GetSinyal` — `trader.lists.YonList`/`SeviyeList`/`SinyalList`'ten
  `barIndex` ile okur, sınır/null kontrolü yapar (boş string / `0.0` fallback).
- `WriteChildTradersDataToFiles` (property, satır 29) bu metodun DEĞİL, `WriteTraderDataToFilesAsync(MultipleTrader)`'ın
  kontrol ettiği ayrı bir bayrak — child'ların kendi `WriteStatisticsToFile()` (tam performans
  raporu) çıktısını üretip üretmeyeceğini belirler, bar-bar liste dosyasıyla karıştırılmamalı.

### Trader-bazlı Özet Karşılaştırma: `WriteMultipleTraderStatistics(...)`

- Amaç (kod içi yorumdan): mainTrader ve her childTrader'ın `WriteStatisticsToFile()` ile zaten
  ayrı ayrı üretilen tam performans raporlarını (`SingleTraderStatistics.txt/.csv` vb.) TEK bir
  dosyada, **satır=trader / kolon=metrik** biçiminde yan yana özetler. Var olan trade-bazlı
  raporların YERİNE geçmez, onların konsolide bir karşılaştırma görünümüdür.
- `BuildStatisticsRows()` — `_mainTrader` + her `trader` için `(Name, statistics.GetOptimizationSummary())`
  çiftlerinden bir liste kurar (`GetOptimizationSummary()` — `Statistics.Statistics` sınıfından,
  bu dokümanın kapsamı dışında; `NetProfit`/`WinRate`/`ProfitFactor`/`MaxDrawdown` gibi anahtarlar
  içerir, bkz. [SingleTraderOptimizer § OptimizationResult](../01-class-reference.md#5-singletraderoptimizer--grid-search-optimizasyon)).
- `WriteMultipleTraderStatisticsToTxt`/`ToCsv` — TXT hizalı kolon genişlikli tablo (her kolon
  genişliği, o kolondaki en uzun değere göre dinamik hesaplanır), CSV düz `;`-ayraçlı.
- **`Finalize()` çağrıldıktan (istatistikler hesaplandıktan) SONRA çağrılmalı** — aksi halde
  `GetOptimizationSummary()` boş/varsayılan değerler döner. `WriteTraderDataToFilesAsync(MultipleTrader)`
  bunu doğru sırada (`Finalize()` sonrası) çağırıyor.
- Dosya adı sabiti hâlâ TODO işaretli: kod içi yorum "`MultipleTraderStatistics` ismi yerine
  daha açıklayıcı bir isim (örn. `MultipleTraderPerformanceSummary`) tercih edilebilir —
  kullanıcı ile konuşulup karar verilecek" — henüz karar verilmemiş, isim değişmedi.

## Çağrı Zinciri — Menüden Çağrılma (Program.cs → AlgoTrader → MultipleTrader)

1. `handleMultipleTrader()` (`Program.cs:3060-`) — [SingleTrader'daki `handleSingleTrader()`](02-singletrader.md#çağrı-zinciri--menüden-çağrılma-programcs--algotrader--singletrader)
   ile birebir aynı desen: `reloadAppConfig()` → `showModeConfigSummary("MultipleTrader")` →
   `[ENTER]/[E]/[R]/[B]` → `showMultipleTraderRunPreview()` → `[ENTER]/[E]/[R]/[B]` →
   `selectedRunMode = ParseRunMode(appConfig.MultipleTrader.RunMode)` → `runMultipleTraderAlgoTrade()`.
2. `runMultipleTraderAlgoTrade()` (`Program.cs:836-892`) — `stockDataReader`/`IsDataReady`
   kontrolü → `new AlgoTrader(...)` + logger/timer + `SetData(...)` + `SymbolName`/`SymbolPeriod`
   → `algoTrader.SingleTraderRunMode = selectedRunMode` → **`AppConfigApplier.ApplyMultipleTrader(algoTrader,
   appConfig.MultipleTrader, AppSettings.ConfigsDir)`** (bkz. [AppConfig
   Kaynağı](#appconfig-kaynağı--multipletraderconfig)) → `Initialize()` → **`await algoTrader.RunMultipleTraderWithProgressAsync()`**
   (tam kaynağı aşağıda) → `WriteTraderDataToFilesAsync(algoTrader.MultipleTrader!)` →
   `PlotEnabled` ise (mainTrader'ın `PlotEnabled`'ı — `MultipleTrader::PlotEnabled` DEĞİL, bkz.
   [Kullanım Haritası](#kullanım-haritası)) `PlotMultipleTraderData(...)`.
3. `AlgoTrader.RunMultipleTraderWithProgressAsync()` (`AlgoTrader.cs:1871-2149`) içinde
   **`createChildTraders()`** (`AlgoTrader.cs:1623-1752`) çağrılır — bu, [SingleTrader §
   `ApplySingleTraderFlagsConfigs`](02-singletrader.md#callbacklerin-gerçek-gövdeleri-algotradercs158-223)'in
   HER child için tekrarlanmış hali: `_childTraderConfigs` listesindeki her `ChildTraderConfigEntry`
   için `new SingleTrader(childId, ...)` → callback bağla → `Reset()` → attribute'ları set et →
   `initialTradeParams.ApplyFrom(config.TradeParams)` (MainTrader'ın TradeParams'ı — TÜM child'lar
   AYNI pozisyon büyüklüğü parametrelerini paylaşır) → per-child `Signals`/`Save`/`Export` config'i
   uygula → `MultipleTraderModeEnabled = true` → equity curve filter (child'a özel `ecfId`) →
   `RunMode` ata → strateji/sorgu ata (`GetStrategy(config.StrategyId)`/`GetQuery(...)`) → `Init()`
   → `multipleTrader.AddTrader(childTrader)`.

> **Not — kod içindeki TODO yorumu artık güncel değil:** `RunMultipleTraderWithProgressAsync()`
> içinde `createChildTraders()` çağrısının hemen altında hâlâ duran uzun bir yorum bloğu
> (`AlgoTrader.cs:1999-2009`), "Sorun 1 — Hardcoded (3 fixed block), dinamik değil ... Şu an hep
> 3 blok hardcoded" diyor. Ama `createChildTraders()`'ın gerçek kaynağına bakınca (`AlgoTrader.cs:1628`,
> `for (int i = 0; i < _childTraderConfigs.Count; i++)`) bu ZATEN dinamik bir döngü — hardcoded 3
> blok değil, `_childTraderConfigs.Count` kaç olursa o kadar child üretiyor. Yorum, muhtemelen
> `createChildTraders()` daha sonra dinamikleştirilirken silinmeyi unutmuş, artık kaynağın gerçek
> davranışını yanlış tarif eden bir kalıntı.

## AppConfig Kaynağı — `MultipleTraderConfig`

`AppConfig.json`'daki `"MultipleTrader"` bölümünü karşılayan C# sınıfları (`AppConfig.cs:253-343`):

```csharp linenums="1"
public class MultipleTraderConfig
{
    public string RunMode { get; set; } = "TradeOnly";
    public MultipleTraderSaveConfig Save   { get; set; } = new();
    public TraderExportConfig?      Export { get; set; }
    public ConsensusConfig    Consensus    { get; set; } = new();
    public MainTraderConfig   MainTrader   { get; set; } = new();
    public List<ChildTraderEntry> ChildTraders { get; set; } = new();   // sıra önemli — ChildId referans olarak kullanılır
}

public class MultipleTraderSaveConfig
{
    public bool   SaveStatisticsToFile              { get; set; } = true;
    public bool   SaveMultipleTraderListsTxtEnabled { get; set; } = true;
    public bool   SaveMultipleTraderListsCsvEnabled { get; set; } = true;
    public string MultipleTraderListsTxtFileName    { get; set; } = "MultipleTraderLists.txt";
    public string MultipleTraderListsCsvFileName    { get; set; } = "MultipleTraderLists.csv";
    public bool   WriteChildTradersDataToFiles      { get; set; } = true;
    // FilePrefix: MainTrader → {FilePrefix}_Main_{FileName}  |  Child → {FilePrefix}_Child{i}_{FileName}
    public string FilePrefix { get; set; } = "MultipleTrader";
}

public class ConsensusConfig
{
    public string Mode        { get; set; } = "Net";
    public int    MinNetCount { get; set; } = 1;
}

// MainTrader — Strategy/Query YOK, sinyal tamamen ChildTrader'lardan (consensus) gelir
public class MainTraderConfig
{
    public EcfRef?                  EquityCurveFilter { get; set; }
    public TradeParamsConfig        TradeParams       { get; set; } = new();   // bkz. SingleTrader doc — TÜM child'lara aktarılır
    public TraderSignalsConfig      Signals           { get; set; } = new();
    public TraderPlotConfig         Plot              { get; set; } = new();
    public TraderOptimizationConfig Optimization      { get; set; } = new();
    public TraderSaveConfig         Save              { get; set; } = new();
    public TraderExportConfig?      Export            { get; set; }
}

// ChildTraderEntry — TradeParams YOK (MainTrader'dan alınır), her child kendi Strategy/Signals/Save'ine sahip
public class ChildTraderEntry
{
    public int                      ChildId           { get; set; }
    public StrategyRef              Strategy          { get; set; } = new();
    public QueryRef?                Query             { get; set; }
    public EcfRef?                  EquityCurveFilter { get; set; }
    public TraderSignalsConfig      Signals           { get; set; } = new();
    public TraderPlotConfig         Plot              { get; set; } = new();
    public TraderOptimizationConfig Optimization      { get; set; } = new();
    public TraderSaveConfig         Save              { get; set; } = new();
    public TraderExportConfig?      Export            { get; set; }
}
```

Bu sınıfların `AppConfig.json`'daki gerçek karşılığı (`inputs/configs/AppConfig/AppConfig.json:105-`,
kısaltılmış — `TradeParams`/`Signals`/`Save` alt-nesnelerinin tam alan listesi [SingleTrader §
AppConfig Kaynağı](02-singletrader.md#appconfig-kaynağı--singletraderconfig)'nda, birebir aynı şema):

```json linenums="1"
"MultipleTrader": {
    "RunMode": "TradeOnly",
    "Save": {
      "SaveStatisticsToFile": true,
      "SaveMultipleTraderListsTxtEnabled": true,
      "SaveMultipleTraderListsCsvEnabled": true,
      "MultipleTraderListsTxtFileName": "MultipleTraderLists.txt",
      "MultipleTraderListsCsvFileName": "MultipleTraderLists.csv",
      "WriteChildTradersDataToFiles": true,
      "FilePrefix": "MultipleTrader"
    },
    "Export": { "ExportEnabled": true, "ConfigFile": "StatisticsExporterConfig.json", "Version": "v1" },
    "Consensus": { "Mode": "Net", "MinNetCount": 1 },
    "MainTrader": {
      "EquityCurveFilter": { "ConfigFile": "EquityCurveFilterConfig.txt", "Name": "", "Version": "v1" },
      "TradeParams": {
        "MarketType": "FxCrypto", "IlkBakiye": 100000.0, "KontratSayisi": 1,
        "LotSayisi": 0.01, "HisseSayisi": 1000.0, "KomisyonCarpan": 0.0,
        "KaymaMiktari": 0.0, "PyramidingEnabled": false
      },
      "Signals": { "AlEnabled": true, "SatEnabled": true, "FlatOlEnabled": true, "...": "..." },
      "Plot": { "PlotEnabled": true },
      "Optimization": { "OptimizationEnabled": false },
      "Save": { "...": "... (12 flag + 12 dosya adı)" },
      "Export": { "ExportEnabled": true, "ConfigFile": "StatisticsExporterConfig.json", "Version": "v1" }
    },
    "ChildTraders": [
      {
        "ChildId": 0,
        "Strategy": { "ConfigFile": "StrategyConfig.txt", "Name": "SimpleMostStrategy", "Version": "v1" },
        "Query": { "ConfigFile": "QueryConfig.txt", "Name": "SimpleQuery1", "Version": "v1" },
        "EquityCurveFilter": { "ConfigFile": "EquityCurveFilterConfig.txt", "Name": "", "Version": "v1" },
        "Signals": { "...": "... (TraderSignalsConfig, 12 alan)" },
        "Save": { "...": "... (12 flag + 12 dosya adı)" },
        "Export": { "ExportEnabled": true, "ConfigFile": "StatisticsExporterConfig.json", "Version": "v1" }
      },
      { "ChildId": 1, "Strategy": { "...": "... (örn. aynı strateji, farklı Version: \"v2\")" } }
    ]
}
```

- `ChildTraders` bir DİZİ — kaç eleman varsa o kadar child trader yaratılır (bkz. yukarıdaki
  `createChildTraders()` Not'u — bu artık gerçekten dinamik).
- `MainTrader.TradeParams` TEK bir yerde tanımlı ve `AppConfigApplier.ApplyMultipleTrader()`
  içinde (`entry.TradeParams.ApplyFrom(mainTradeParams)`) TÜM child'lara kopyalanır — yani
  `ChildTraders[i]` altında `TradeParams` alanı YOK, her child farklı pozisyon büyüklüğü
  parametresi kullanamaz (Console/AppConfig yolundan; script'ten elle set edilebilir).
- Strateji/Sorgu/ECF referansları **benzersizleştirilip** (`Name`+`Version` eşleşmesine göre)
  tek bir `_strategyConfigs`/`_queryConfigs`/`_equityCurveFilterConfigs` girişine indirgeniyor —
  iki child aynı `(Name, Version)` kombinasyonunu kullanıyorsa aynı config id'sini paylaşır
  (gereksiz tekrar yüklemeyi önler).
- `FilePrefix` (varsayılan `"MultipleTrader"`) — mainTrader dosyaları `{FilePrefix}_Main_{FileName}`,
  her child `{FilePrefix}_Child{i}_{FileName}` olarak öneklenir; iki farklı `MultipleTrader`
  koşumunun (örn. farklı sembol/config) çıktı dosyaları birbirini ezmesin diye.

## `RunMultipleTraderWithProgressAsync()` — Tam Kaynak (`AlgoTrader.cs:1871-2149`)

```csharp linenums="1" hl_lines="10 24 34 66 92 104"
public async Task RunMultipleTraderWithProgressAsync(CancellationToken cancellationToken = default)
{
    int totalBars = 0;

    if (!IsInitialized) {
        throw new InvalidOperationException("AlgoTrader not initialized. Call Initialize() first.");
    }

    try
    {
        _timer!.RestartTimer("0");
        totalBars = GetDataCount();
        Log($"AlgoTrader '{Name}' MultipleTrader started. Total bars: {totalBars}");

        // Indicators
        if (indicators != null) { indicators.Dispose(); indicators = null; }
        indicators = new IndicatorManager(this.Data);
        if (indicators == null) throw new InvalidOperationException("indicators can not be created...");

        // MultipleTrader — Cleanup previous run
        if (multipleTrader != null) { multipleTrader.Dispose(); multipleTrader = null; }

        multipleTrader = new MultipleTrader(0, this.Data, indicators, _logger);
        if (multipleTrader == null) throw new InvalidOperationException("multipleTrader can not be created...");

        multipleTrader.Reset();

        // MultipleTrader save config (AppConfig.MultipleTrader.Save)
        if (_multipleTraderSaveConfig is { } mts)
        {
            multipleTrader.SaveStatisticsToFile              = mts.SaveStatisticsToFile;
            multipleTrader.SaveMultipleTraderListsTxtEnabled = mts.SaveMultipleTraderListsTxtEnabled;
            multipleTrader.SaveMultipleTraderListsCsvEnabled = mts.SaveMultipleTraderListsCsvEnabled;
            if (!string.IsNullOrWhiteSpace(mts.MultipleTraderListsTxtFileName))
                multipleTrader.MultipleTraderListsTxtFileName = mts.MultipleTraderListsTxtFileName;
            if (!string.IsNullOrWhiteSpace(mts.MultipleTraderListsCsvFileName))
                multipleTrader.MultipleTraderListsCsvFileName = mts.MultipleTraderListsCsvFileName;
            multipleTrader.WriteChildTradersDataToFiles      = mts.WriteChildTradersDataToFiles;
        }
        else
        {
            // Fallback
            multipleTrader.SaveStatisticsToFile              = true;
            multipleTrader.SaveMultipleTraderListsTxtEnabled = true;
            multipleTrader.SaveMultipleTraderListsCsvEnabled = true;
            multipleTrader.MultipleTraderListsTxtFileName    = "MultipleTraderLists.txt";
            multipleTrader.MultipleTraderListsCsvFileName    = "MultipleTraderLists.csv";
            multipleTrader.WriteChildTradersDataToFiles      = false;
        }

        // MultipleTrader consensus config (AppConfig.MultipleTrader.Consensus)
        if (_multipleTraderConsensusConfig is { } mtc)
        {
            if (!string.IsNullOrWhiteSpace(mtc.Mode))
                multipleTrader.ConsensusMode = mtc.Mode;
            multipleTrader.ConsensusMinNetCount = mtc.MinNetCount;
        }

        var mainTrader = multipleTrader.GetMainTrader();
        if (mainTrader == null) throw new InvalidOperationException("mainTrader can not be created...");

        // mainTrader'a callback bağla (SingleTrader'ın kendi metoduyla — MultipleTrader::SetCallbacks DEĞİL)
        mainTrader.ClearCallbacks()
                   .SetCallbacks(OnSingleTraderReset, OnSingleTraderInit, OnSingleTraderRun, OnSingleTraderFinal,
                                 OnSingleTraderBeforeOrder, OnSingleTraderNotifySignal, OnSingleTraderAfterOrder, OnSingleTraderProgress);

        mainTrader.Reset();

        mainTrader.SymbolName             = this.SymbolName;
        mainTrader.SymbolPeriod           = this.SymbolPeriod;
        mainTrader.SystemId               = this.SystemId;
        mainTrader.SystemName             = this.SystemName;
        mainTrader.StrategyId             = this.StrategyId;
        mainTrader.StrategyName           = this.StrategyName;
        mainTrader.QueryId                = this.QueryId;
        mainTrader.QueryName              = this.QueryName;
        mainTrader.LastExecutionTime      = System.DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss");
        mainTrader.LastExecutionTimeStart = System.DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss");

        // TradeParams — AppConfig.MultipleTrader.MainTrader.TradeParams
        if (_singleTraderTradeParamsConfig is not null)
            mainTrader.initialTradeParams!.ApplyFrom(_singleTraderTradeParamsConfig);

        // Sinyal/Save/Plot/Export config'lerini tek çağrıda uygula (SingleTrader'la PAYLAŞILAN slotlar)
        ApplySingleTraderFlagsConfigs(mainTrader);

        mainTrader.MultipleTraderModeEnabled = true;

        SetSingleTraderConfigureEquityCurveFilter(mainTrader);

        mainTrader.RunMode = SingleTraderRunMode;

        mainTrader.Init();

        // Child SingleTrader'ları oluştur ve MultipleTrader'a ekle
        createChildTraders();

        multipleTrader.Init();

        Log("\nRunning multipleTrader...");

        _timer!.RestartTimer("1");
        _timer!.RestartTimer("2");

        IsRunning = true;
        await Task.Run(() =>
        {
            multipleTrader.IsStarted = true;
            multipleTrader.IsRunning = true;
            multipleTrader.IsStopped = false;
            multipleTrader.IsStopRequested = false;

            for (int i = 0; i < totalBars; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (multipleTrader.IsStopRequested)
                {
                    Log($"MultipleTrader stopped by user request at bar {i}/{totalBars}");
                    break;
                }

                multipleTrader.Run(i);

                double percentage = (i + 1) / (double)totalBars * 100.0;
                OnTraderProgress?.Invoke(i + 1, totalBars, percentage);
            }
        }, cancellationToken);
        IsRunning = false;

        _timer!.StopTimer("2");

        // Her child + mainTrader için tarama bilgileri (Finalize gerek kalmadan alınabilir)
        var tradersCount = multipleTrader.Traders.Count;
        for (int i = 0; i < tradersCount; i++)
        {
            var singleTrader = multipleTrader.Traders[i];
            var _ozet = singleTrader.TaramaOzeti;   // her child için ayrı ayrı — ama sonucu hiçbir yere yazılmıyor
        }
        var ozet = mainTrader.TaramaOzeti;          // mainTrader için de aynı — bu da kullanılmıyor

        Log("\nFinalizing multipleTrader...");

        _timer!.RestartTimer("3");
        multipleTrader.Finalize();

        // TODO : Asagisı singleTrader e benzer olarak yapılacak (GetStatisticsHeaderRow/DataRow) — yorum satırı, çalışmıyor
        // Dosyaya yazma: WriteTraderDataToFilesAsync metoduna taşındı — yorum satırı, çalışmıyor

        _timer!.StopTimer("3");
        _timer!.StopTimer("1");
        _timer!.StopTimer("0");
        // t0-t3 elapsed time logları...
    }
    catch (Exception ex)
    {
        Log($"An error occurred while running in RunMultipleTraderWithProgressAsync(): {ex.Message}");
    }
    finally { }

    if (multipleTrader is not null)
    {
        multipleTrader.IsRunning = false;
        multipleTrader.IsStopped = true;
    }
}
```

> **Not — `TaramaOzeti` döngüsü (satır 145-150) hesaplanıyor ama hiç kullanılmıyor:** SingleTrader'ın
> kendi `RunSingleTraderWithProgressAsync()`'inde aynı desen (`yon`/`kacBarOnce`/`karZarar`/
> `karZararYuzde`/`ozet` değişkenleri) en azından `Log(...)` ile ekrana basılıyordu (bkz.
> [SingleTrader § Tam Kaynak](02-singletrader.md#runsingletraderwithprogressasync--tam-kaynak-algotradercs1252-1530)
> satır 122-126). Burada (`AlgoTrader.cs:2051-2068`) hem her child için hem mainTrader için
> aynı property'ler okunuyor ama sonuç HİÇBİR YERE yazılmıyor/loglanmıyor — derleyicinin "unused
> variable" uyarısı vermemesi için (local `var`, kullanılmasa da atama başlı başına bir "kullanım"
> sayılır) muhtemelen ileride eklenecek bir log satırının yer tutucusu.

## Callback'lerin Gerçek Gövdeleri

`MultipleTrader` kendi callback gövdelerini TANIMLAMAZ — hem mainTrader'a hem her child'a
[SingleTrader dokümanındaki `OnSingleTraderReset`/`Init`/`Run`/`Final`/`BeforeOrder`/`NotifySignal`/
`AfterOrder`/`Progress`](02-singletrader.md#callbacklerin-gerçek-gövdeleri-algotradercs158-223)
callback'lerinin AYNI seti bağlanır (`AlgoTrader.cs:1956-1957` mainTrader için,
`AlgoTrader.cs:1637-1638` her child için) — ayrı bir `OnMultipleTraderXxx` callback ailesi YOK.
Bu yüzden:

- Tüm çocuk trader'ların `OnRun`/`OnBeforeOrder`/`OnAfterOrder`/... event'leri, SingleTrader'da
  belgelendiği gibi çalışır (`OnProgress` hariç hepsi boş gövde, bkz. link).
- mainTrader'ın `OnRun`'ı ise (yukarıda [Run()](#run--çocuk-traderları--maintrader-pipeline)
  bölümünde açıklandığı gibi) `MultipleTrader.Run()` `SingleTrader.Run()`'ı hiç çağırmadığı için
  ZATEN hiç tetiklenmiyor — callback boş olsa da olmasa da fark etmez.

## Dönüş / Sonuç — Global State

| Değişken/Erişim | Tip | Kaynak |
|---|---|---|
| `algoTrader.MultipleTrader` | `MultipleTrader` (public getter, `private set` — `multipleTrader` field'ının expression-bodied property'si) | `RunMultipleTraderWithProgressAsync()` içinde yaratılan `multipleTrader` |
| `algoTrader.MultipleTrader.GetMainTrader()` | `SingleTrader` | mainTrader — composite sinyalle gerçek trade yapan |
| `algoTrader.MultipleTrader.Traders` | `List<SingleTrader>` | Her child, kendi defterinde gerçek trade yapmış halde |
| 3 × `MultipleTraderLists.*`, `MultipleTraderStatistics.*` + mainTrader/child'ların kendi `*Statistics.*`/`*Lists.*` dosyaları | dosya | `WriteTraderDataToFilesAsync(algoTrader.MultipleTrader!)` → `WriteMultipleTraderListsToFiles` + `WriteMultipleTraderStatistics` + mainTrader/child `WriteStatisticsToFile` |

- `stockDataReader`/`stockDataList`/`stockMetaData` (bkz. [StockDataReader §
  Dönüş/Sonuç](09-stockdatareader.md#dönüş--sonuç--global-state)) bu akışın ÖN KOŞULU —
  `runMultipleTraderAlgoTrade()`'in ilk satırı bunları kontrol ediyor.
- `WriteChildTradersDataToFiles` (`MultipleTrader` property) `false` ise sadece mainTrader'ın
  istatistik dosyaları yazılır, child'ların `*Statistics.*`/`*Lists.*` dosyaları HİÇ üretilmez
  (ama `MultipleTraderStatistics.*`/`MultipleTraderLists.*` — karşılaştırma/bar-bar dosyaları —
  bu bayraktan etkilenmez, her zaman yazılır).

## Tipik Kullanım — Script'ten Çağrılma (`CustomConsensusFunc` Örneği)

- Konum: `Program.cs`/`AlgoTrader` akışının DIŞINDA — `RunMultipleTraderWithProgressAsync()`
  `multipleTrader`'ı kendi içinde yaratıp aynı çağrıda çalıştırdığı için, "oluşturuldu ama henüz
  çalışmadı" arası bir enjeksiyon noktası yok. `CustomConsensusFunc` atamak istiyorsan
  `MultipleTrader`'ı **manuel** kurman şart (bkz. [03-scripting-guide.md § Üç Kullanım
  Seviyesi](../03-scripting-guide.md#4-üç-kullanım-seviyesi), Seviye B).
- Gerçek örnek: `inputs/scripts/CustomConsensusExample.csx` — 7 hazır referans consensus
  method'u (`NetConsensusReference`/`MajorityConsensusReference`/`AllConsensusReference`/
  `AnyConsensusReference` — hazır 4 modun script karşılığı; `FirstChildWinsConsensus`/
  `WeightedConsensus`/`BothAgreeConsensus` — hazır modların üretemeyeceği özel örnekler).

**1) Data + Indicators + 2 child strateji hazırlığı** (`algoTrader` sadece strateji factory'si
olarak kullanılıyor, `RunXxxWithProgressAsync()` HİÇ çağrılmıyor)

```csharp linenums="1"
var indicators = new IndicatorManager(data);

algoTrader.SetData(data);
algoTrader.RegisterLogger(LogManager.GetInstance());
algoTrader.RegisterTimer(TimeManager.GetInstance());
algoTrader.SymbolName   = symbolName;
algoTrader.SymbolPeriod = symbolPeriod;
algoTrader.Initialize();

var childStrategy0 = algoTrader.CreateStrategyFromRegistry(data, indicators, "SimpleMostStrategy",
    new Dictionary<string, object> { ["period"] = 21, ["percent"] = 1.0, ["choice"] = 0 });
var childStrategy1 = algoTrader.CreateStrategyFromRegistry(data, indicators, "SimpleMostStrategy",
    new Dictionary<string, object> { ["period"] = 14, ["percent"] = 0.5, ["choice"] = 0 });
```

**2) MultipleTrader'ı manuel kur — mainTrader**

```csharp linenums="1"
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
```

**3) 2 child ekle**

```csharp linenums="1"
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
}

AddChild(0, childStrategy0);
AddChild(1, childStrategy1);

multipleTrader.Init();
```

**4) `CustomConsensusFunc` ata — Run döngüsünden ÖNCE, `Init()`'ten SONRA**

```csharp linenums="1"
TradeSignals FirstChildWinsConsensus(List<SingleTrader> traders)
{
    if (traders.Count == 0) return TradeSignals.Flat;
    var firstChild = traders[0];
    if (firstChild.is_son_yon_a()) return TradeSignals.Buy;
    if (firstChild.is_son_yon_s()) return TradeSignals.Sell;
    return TradeSignals.Flat;
}

multipleTrader.CustomConsensusFunc = FirstChildWinsConsensus;
```

**5) Kendi Run döngünü elle yaz**

```csharp linenums="1"
int totalBars = data.Count;
multipleTrader.IsStarted = true;
multipleTrader.IsRunning = true;

for (int i = 0; i < totalBars; i++)
{
    if (IsCancellationRequested) break;
    multipleTrader.Run(i);
}

multipleTrader.IsRunning = false;
multipleTrader.IsStopped = true;
```

**6) Finalize + dosyaya yaz + karşılaştır**

```csharp linenums="1"
multipleTrader.Finalize();

multipleTrader.WriteMultipleTraderListsToFiles(AppSettings.LogsDir);
multipleTrader.WriteMultipleTraderStatistics(AppSettings.LogsDir);
mainTrader.WriteStatisticsToFile(AppSettings.LogsDir, AppSettings.ConfigsDir);
foreach (var child in multipleTrader.Traders)
    child.WriteStatisticsToFile(AppSettings.LogsDir, AppSettings.ConfigsDir);

Log($"mainTrader Ozet : {mainTrader.TaramaOzeti}");
multipleTrader.Dispose();
```

- Doğrulama fikri (script'in kendi yorumundan): `FirstChildWinsConsensus` altında mainTrader'ın
  performansı, `childTrader_0`'ın SOLO performansına çok yakın çıkmalı —
  `MultipleTraderStatistics.csv`'de `mainTrader` vs `childTrader_0` satırlarını karşılaştırarak
  görülebilir. Bu, `CustomConsensusFunc`'ın gerçekten devreye girdiğini kanıtlamanın pratik yolu.
- Diğer 6 referans method (`NetConsensusReference` vb.) `multipleTrader.CustomConsensusFunc = ...`
  satırında yorum olarak hazır duruyor — tek satır değiştirerek denenebilir.

## Console/JSON Eşleşmesi

Manuel script kurulumunun Console karşılığı — kod yazmadan, `AppConfig.json` düzenleyerek (ama
`CustomConsensusFunc` gibi script-only genişletme noktaları HARİÇ — bunlar AppConfig'ten
erişilemez, `Func` serialize edilemediği için):

1. `inputs/configs/AppConfig/AppConfig.json` dosyasını aç.
2. `"MultipleTrader"` bölümünü düzenle (bkz. yukarıdaki [AppConfig Kaynağı](#appconfig-kaynağı--multipletraderconfig)
   tam örnek): `Consensus.Mode`/`MinNetCount` ile hazır 4 moddan birini seç, `MainTrader.TradeParams`
   ile TÜM child'ların ortak pozisyon büyüklüğünü, `ChildTraders` dizisine her child için ayrı
   `Strategy`/`Signals`/`Save` gir.
3. Kaydet, Console'u çalıştır, menüden `[3] MultipleTrader` (veya `[6]` "Read Data +
   MultipleTrader") seç.

Örneğin "3 child, Majority consensus" için `ChildTraders`'a 3 eleman eklenir ve:

```json linenums="1"
"Consensus": { "Mode": "Majority", "MinNetCount": 1 }
```

Arkada `AppConfigApplier.ApplyMultipleTrader(...)` bu JSON'u `algoTrader.SetChildTraderCount(3,
...)` + `algoTrader.SetMultipleTraderConsensusConfig(new MultipleTraderConsensusConfig { Mode =
"Majority", MinNetCount = 1 })` çağrılarına çevirir; `RunMultipleTraderWithProgressAsync()`
içinde bu değerler `multipleTrader.ConsensusMode`/`ConsensusMinNetCount`'a atanır — script'teki
`multipleTrader.ConsensusMode = "Majority";` ile birebir aynı sonucu üretir. `CustomConsensusFunc`
için JSON karşılığı YOK — script yazmak zorunlu tek yol.

## Kimler Kullanıyor — Instantiation Noktaları

`new MultipleTrader(...)` için tüm kod tabanında grep taraması — sadece **4 çağırım noktası**
(SingleTrader'ın 25'ine kıyasla çok daha az — `MultipleTrader` doğrudan başka sınıfların içinde
throwaway olarak kurulmuyor, sadece 1 gerçek "wrapper" var: `ConfirmingMultipleTrader`).

| Dosya | Bağlam | Satır |
|---|---|---|
| `AlgoTrade.Core/Trading/AlgoTrader.cs` | `RunMultipleTraderWithProgressAsync()` — `multipleTrader` (id=0) | 1914 |
| `AlgoTrade.Core/Trading/Traders/ConfirmingMultipleTrader.cs` | constructor — `_signalMultipleTrader` (tam bağımsız çalışan bir `MultipleTrader`, [Confirming* mimarisi](../01-class-reference.md#7-confirmingmultipletrader--consensus--sanal-pozisyon-konfirmasyonu)'nin "signal katmanı") | 113 |
| `inputs/scripts/02_RunMultipleTraderWithProgressAsync.csx` | top-level akış — `multipleTrader` | 109 |
| `inputs/scripts/CustomConsensusExample.csx` | top-level akış — `multipleTrader` | 81 |

- `ConfirmingMultipleTrader`, `MultipleTrader`'ı HİÇ değiştirmeden reuse ediyor — kendi
  `_signalMultipleTrader`'ı (ham sinyal üreten N child + consensus) tam bağımsız bir
  `MultipleTrader` instance'ı, `VirtualPositionConfirmer` bunun `GetMainTrader()`'ının ürettiği
  sinyali sanal olarak takip edip gerçek `ConfirmingMultipleTrader._mainTrader`'a aktarıyor.
- `MultipleTrader()` (parametresiz constructor, iskelet satır 46) hiçbir instantiation noktasında
  kullanılmıyor — 4 çağrının hepsi parametreli overload.

## Kullanım Haritası

| Üye | Durum | Nerede |
|---|---|---|
| Constructor (parametreli), `AddTrader`, `Reset`, `Run`, `Finalize`, `GetMainTrader`, `ConsensusMode`, `ConsensusMinNetCount` | ✅ | `RunMultipleTraderWithProgressAsync()` (yukarıda tam kaynağıyla var) |
| `BuildConsensusSignal()` (hardcoded 4 mod dalı) | ✅ | `Run(i)` içinden her bar çağrılıyor |
| `CustomConsensusFunc` | ✅ (sadece script'ten) | `CustomConsensusExample.csx` — Console/AppConfig yolundan hiç set edilemiyor |
| `WriteMultipleTraderListsToFiles`, `WriteMultipleTraderStatistics` | ✅ | `WriteTraderDataToFilesAsync(MultipleTrader)` |
| `SaveStatisticsToFile`, `WriteChildTradersDataToFiles`, 4+4 dosya adı/bayrak property'si | ✅ | `RunMultipleTraderWithProgressAsync()` + `AppConfig.MultipleTrader.Save` |
| `Dispose()` | ✅ | `RunMultipleTraderWithProgressAsync()`'in "Cleanup previous run" adımı (bir önceki koşumun `multipleTrader`'ı) + `CustomConsensusExample.csx` sonunda |
| `Traders` | ✅ | `createChildTraders()`'ın `AddTrader` ile doldurduğu liste, `WriteMultipleTraderListsToFiles`/`Statistics` bunu okur |
| `MultipleTrader()` (parametresiz constructor) | ❌ | Hiçbir yerde kullanılmıyor |
| `Initialize(data)` | ❌ | Hiçbir yerde çağrılmıyor — constructor zaten `Data`'yı ayarlıyor |
| `SetCallbacks(...)` (toplu — mainTrader+tüm child'lara) | ❌ (Console akışında) | `RunMultipleTraderWithProgressAsync()` bunu kullanmıyor, mainTrader'a `SingleTrader.SetCallbacks` doğrudan, child'lara `createChildTraders()` içinde ayrı ayrı bağlanıyor; sadece script'ten manuel kurulumda kullanışlı olabilir (henüz gerçek bir örnek yok) |
| `Stop()` | ❌ | Hiçbir yerde çağrılmıyor (SingleTrader/AlgoTrader'ın `IsStopRequested` mekanizması `algoTrader` seviyesinde farklı şekilde yönetiliyor) |
| `OnProgress` (delegate property) | ❌ | Hiçbir yerden `Invoke` edilmiyor — `AlgoTrader.OnTraderProgress` (farklı, `algoTrader` seviyesinde ayrı bir event) kullanılıyor onun yerine |
| `DynamicPositionSizeEnabled` | ❌ | Gövdesi TODO, işlevsiz — bkz. [Not](#run--çocuk-traderları--maintrader-pipeline) |
| `PlotEnabled` (`MultipleTrader` sınıfının kendi property'si) | ❌ | `runMultipleTraderAlgoTrade()` `mainTrader.PlotEnabled`'ı okuyor (`SingleTrader`'ın property'si), `MultipleTrader::PlotEnabled` hiç okunmuyor — muhtemelen kopya-kalıntı, iki ayrı "PlotEnabled" var ve sadece biri gerçekten kullanılıyor |
| `CurrentIndex` | ❌ | `Reset()`/`Initialize()`/`Finalize()`'da `0`'a set ediliyor ama `Run(i)` içinde HİÇ artırılmıyor/okunmuyor — `i` parametresi kullanılıyor, `CurrentIndex` state'i güncel tutulmuyor |
| `Init()` | ⚠️ | Çağrılıyor ama gövdesi fiilen no-op (`trader.Init()` yorum satırı) — bkz. [Kimlik ve Kurulum](#kimlik-ve-kurulum) |
| `GetYon`/`GetSeviye`/`GetSinyal` (private) | ✅ (sadece iç kullanım) | `WriteBarDataTxt`/`WriteBarDataCsv`'nin iç yardımcıları |

## İlgili Dosyalar

- [01-class-reference.md § 4. MultipleTrader](../01-class-reference.md#4-multipletrader--çoklu-strateji--consensus) —
  bu sayfanın ait olduğu index, kısa özet.
- [02-singletrader.md](02-singletrader.md) — `mainTrader`/child'ların gerçek tipi, aynı derinlikte
  belgelenen kardeş sayfa; bu sayfa onu önceden anlamış olduğunu varsayıyor.
- [09-stockdatareader.md](09-stockdatareader.md) — veri kaynağı (`Data`), aynı derinlikte
  belgelenen kardeş sayfa.
- [06-class-doc-method.md](../06-class-doc-method.md) — bu sayfanın yazıldığı yöntem.
- [03-scripting-guide.md](../03-scripting-guide.md) — `CustomConsensusFunc` için gereken "manuel
  kurulum" (Seviye B) desenin genel açıklaması.
- [02-console-menu-guide.md](../02-console-menu-guide.md) — Console menü rehberi, `[3]`/`[6]`
  satırları.
- `docs/PROJECT_ANALYSIS.md` — `DynamicPositionSizeEnabled` ölü kod bulgusunun ilk kaynağı.
