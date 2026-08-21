# ConfirmingMultipleTrader — Consensus + Sanal Pozisyon Konfirmasyonu (Menü [24])

> [Class Reference](../01-class-reference.md) setinin bir parçası — [SingleTrader](02-singletrader.md)/
> [MultipleTrader](03-multipletrader.md) gibi aynı derinlikte işlendi. Kardeş sayfa:
> [ConfirmingSingleTrader](04-confirmingsingletrader.md) — `VirtualPositionConfirmer` motoru
> (bu sayfada TEKRAR anlatılmıyor, [orada tam olarak ele
> alınmış](04-confirmingsingletrader.md#virtualpositionconfirmer--ortak-konfirmasyon-motoru))
> ikisi arasında BİREBİR ortak. Yöntem: [06-class-doc-method.md](../06-class-doc-method.md).

### Dosyalar

- `src/AlgoTrade.Core/Trading/Traders/ConfirmingMultipleTrader.cs` (483 satır)
- `src/AlgoTrade.Core/Trading/Core/VirtualPositionConfirmer.cs` — bkz. [ConfirmingSingleTrader §
  VirtualPositionConfirmer](04-confirmingsingletrader.md#virtualpositionconfirmer--ortak-konfirmasyon-motoru),
  bu sayfaya tekrar taşınmadı.
- `src/AlgoTrade.Core/Trading/Traders/MultipleTrader.cs` — `_signalMultipleTrader`, hiç
  değiştirilmeden reuse edilen TAM bir `MultipleTrader` instance'ı, bkz. [MultipleTrader
  dokümanı](03-multipletrader.md).
- `src/AlgoTrade.Core/Trading/Traders/SingleTrader.cs` — `_mainTrader`.

### Rolü

- [ConfirmingSingleTrader](04-confirmingsingletrader.md)'ın `MultipleTrader` karşılığı — tek bir
  stratejinin ham sinyali yerine **N child stratejinin consensus (bileşke) sinyalini** sanal
  pozisyonla konfirme eder. Consensus "AL" dediğinde gerçek emir AÇILMAZ — o bar'ın fiyatından
  sanal bir pozisyon takip edilmeye başlanır; eşik geçildiği ANDA gerçek sinyal `mainTrader`'a
  iletilir.
- Mimari — composition, ConfirmingSingleTrader'ın "signalTrader = tam çalışan bağımsız trader"
  deseninin `MultipleTrader` karşılığı: `_signalMultipleTrader` **tam, bağımsız çalışan gerçek
  bir `MultipleTrader`** (N child + kendi consensus mantığı, HİÇ DEĞİŞTİRİLMEDEN reuse ediliyor)
  — onun kendi mainTrader'ı bizim "ham sinyal kaynağımız". Konfirmasyon state machine'i
  (`VirtualPositionConfirmer`) ConfirmingSingleTrader ile ORTAK — kod tekrarı yok, aynı sınıf.
- `MultipleTrader`'ın kendi lifecycle konvansiyonuna uyulur: `MultipleTrader.Reset()/Init()`
  kendi mainTrader'ını/child'larını YÖNETMEZ (no-op'a yakın, bkz. [MultipleTrader §
  Kimlik ve Kurulum](03-multipletrader.md#kimlik-ve-kurulum)) — çağıran taraf (bu sınıf)
  `signalMultipleTrader`'ın mainTrader'ını ve her child'ı (`AddTrader`'dan ÖNCE) kendisi
  Reset/Init etmekle yükümlü, tıpkı `AlgoTrader.createChildTraders()`'ın yaptığı gibi.

### Ne zaman kullanılır

- Birden fazla stratejinin CONSENSUS'unu (tek strateji değil) gerçek pozisyona çevirmeden önce
  sanal pozisyonla teyit etmek istediğinde. Console `[24]`-`[25]`.
- Tasarım tartışması için bkz. `docs/todo.md`, "Getiri Eğrisi / KarZarar Eğrisi Konfirmasyonu
  (Madde 3)".

### Sınıf İskeleti (ilk bakış)

```csharp linenums="1"
public class ConfirmingMultipleTrader
{
    public int Id { get; private set; }
    public List<StockData> Data { get; private set; }
    public IndicatorManager Indicators { get; private set; }
    public LogManager? Logger { get; private set; }

    public bool IsInitialized { get; private set; }

    // ---- State flags ----
    public bool IsStarted { get; set; }
    public bool IsRunning { get; set; }
    public bool IsStopped { get; set; }
    public bool IsStopRequested { get; set; }

    private MultipleTrader _signalMultipleTrader;
    private SingleTrader _mainTrader;

    public Action<ConfirmingMultipleTrader, int, int>? OnProgress { get; set; }

    public bool SaveStatisticsToFile { get; set; } = true;

    // ---- Output file settings ----
    public string ConfirmingMultipleTraderListsTxtFileName { get; set; } = "ConfirmingMultipleTraderLists.txt";
    public string ConfirmingMultipleTraderListsCsvFileName { get; set; } = "ConfirmingMultipleTraderLists.csv";
    public bool SaveConfirmingMultipleTraderListsTxtEnabled { get; set; } = true;
    public bool SaveConfirmingMultipleTraderListsCsvEnabled { get; set; } = true;

    // ---- Consensus — signalMultipleTrader'a pass-through ----
    public string ConsensusMode { get => _signalMultipleTrader.ConsensusMode; set => _signalMultipleTrader.ConsensusMode = value; }
    public int ConsensusMinNetCount { get => _signalMultipleTrader.ConsensusMinNetCount; set => _signalMultipleTrader.ConsensusMinNetCount = value; }

    // ---- Confirmation — VirtualPositionConfirmer'a pass-through ----
    private readonly VirtualPositionConfirmer _confirmer = new();
    public bool ThresholdIsPercentage { get => _confirmer.ThresholdIsPercentage; set => _confirmer.ThresholdIsPercentage = value; }
    public double ProfitThreshold { get => _confirmer.ProfitThreshold; set => _confirmer.ProfitThreshold = value; }
    public double LossThreshold { get => _confirmer.LossThreshold; set => _confirmer.LossThreshold = value; }
    public ConfirmationTrigger Trigger { get => _confirmer.Trigger; set => _confirmer.Trigger = value; }
    public SignalConflictMode ConflictMode { get => _confirmer.ConflictMode; set => _confirmer.ConflictMode = value; }
    public bool FlattenImmediatelyOnFlatSignal { get => _confirmer.FlattenImmediatelyOnFlatSignal; set => _confirmer.FlattenImmediatelyOnFlatSignal = value; }

    // ---- Virtual Position State (diagnostic) ----
    private string[] _virtualYonHistory;
    private bool[] _confirmedHistory;
    public string? VirtualYon => _confirmer.VirtualYon;
    public double VirtualEntryPrice => _confirmer.VirtualEntryPrice;
    public bool IsConfirmed => _confirmer.IsConfirmed;

    // ---- Plotting — Signals ----
    public List<double> VirtualSignals => _signalMultipleTrader.GetMainTrader().lists.SinyalList;
    public List<double> Signals => _mainTrader.lists.SinyalList;

    // ---- Kurulum ----
    public ConfirmingMultipleTrader(int id, List<StockData> data, IndicatorManager indicators, LogManager? logger);

    public void AddTrader(SingleTrader trader);

    // ---- Lifecycle ----
    public void Reset();
    public void Init();

    // ---- Confirmation & Run ----
    private TradeSignals ResolveConfirmedSignal(int i);
    public void Run(int i);

    // ---- Finalize ----
    public void Finalize();

    // ---- Main/Signal Trader Access ----
    public SingleTrader GetMainTrader();
    public MultipleTrader GetSignalMultipleTrader();
    public void SetCallbacks(
        Action<SingleTrader, int>? onReset = null, Action<SingleTrader, int>? onInit = null,
        Action<SingleTrader, int>? onRun = null, Action<SingleTrader, int>? onFinal = null,
        Action<SingleTrader, int>? onBeforeOrders = null, Action<SingleTrader, string, int>? onNotifySignal = null,
        Action<SingleTrader, int>? onAfterOrders = null, Action<SingleTrader, int, int, double>? onProgress = null,
        Action<SingleTrader>? onApplyUserFlags = null);
    public void Stop();

    // ---- Lists Export ----
    private void WriteConfirmingMultipleTraderListsToFiles();
    private void WriteConfirmingMultipleTraderListsToTxt();
    private void WriteHeaderTxt(System.IO.StreamWriter writer);
    private void WriteBarDataTxt(System.IO.StreamWriter writer, int barIndex, SingleTrader signalMain);
    private void WriteConfirmingMultipleTraderListsToCsv();
    private void WriteHeaderCsv(System.IO.StreamWriter writer);
    private void WriteBarDataCsv(System.IO.StreamWriter writer, int barIndex, SingleTrader signalMain);
    private string GetVirtualYon(int barIndex);
    private string GetConfirmed(int barIndex);
    private string GetYon(SingleTrader trader, int barIndex);
    private double GetSeviye(SingleTrader trader, int barIndex);
    private double GetSinyal(SingleTrader trader, int barIndex);

    // ---- Dispose ----
    public void Dispose();
}
```

### Üye İndeksi — Hangisi Nerede Anlatılıyor

| # | Üye | Tür | Detay |
|---|---|---|---|
| 3 | `ConfirmingMultipleTrader::Id` | public property | [Kurulum](#kurulum) |
| 4 | `ConfirmingMultipleTrader::Data` | public property | [Kurulum](#kurulum) |
| 5 | `ConfirmingMultipleTrader::Indicators` | public property | [Kurulum](#kurulum) |
| 6 | `ConfirmingMultipleTrader::Logger` | public property | [Kurulum](#kurulum) |
| 8 | `ConfirmingMultipleTrader::IsInitialized` | public property | [Kurulum](#kurulum) |
| 11 | `ConfirmingMultipleTrader::IsStarted` | public property | [Run() — Konfirmasyon Akışı](#run--konfirmasyon-akışı) |
| 12 | `ConfirmingMultipleTrader::IsRunning` | public property | [Run() — Konfirmasyon Akışı](#run--konfirmasyon-akışı) |
| 13 | `ConfirmingMultipleTrader::IsStopped` | public property | [Run() — Konfirmasyon Akışı](#run--konfirmasyon-akışı) |
| 14 | `ConfirmingMultipleTrader::IsStopRequested` | public property | [Main/Signal Trader Access](#mainsignal-trader-access-getmaintrader--getsignalmultipletrader--setcallbacks--stop) — `Stop()` ile set edilir |
| 16 | `ConfirmingMultipleTrader::_signalMultipleTrader` | private field | [Kurulum](#kurulum) |
| 17 | `ConfirmingMultipleTrader::_mainTrader` | private field | [Kurulum](#kurulum) |
| 19 | `ConfirmingMultipleTrader::OnProgress` | public property (delegate) | [Run() — Konfirmasyon Akışı](#run--konfirmasyon-akışı) |
| 21 | `ConfirmingMultipleTrader::SaveStatisticsToFile` | public property | [Finalize()](#finalize) |
| 24 | `ConfirmingMultipleTrader::ConfirmingMultipleTraderListsTxtFileName` | public property | [Lists Export](#lists-export-writeconfirmingmultipletraderliststofiles) |
| 25 | `ConfirmingMultipleTrader::ConfirmingMultipleTraderListsCsvFileName` | public property | [Lists Export](#lists-export-writeconfirmingmultipletraderliststofiles) |
| 26 | `ConfirmingMultipleTrader::SaveConfirmingMultipleTraderListsTxtEnabled` | public property | [Lists Export](#lists-export-writeconfirmingmultipletraderliststofiles) |
| 27 | `ConfirmingMultipleTrader::SaveConfirmingMultipleTraderListsCsvEnabled` | public property | [Lists Export](#lists-export-writeconfirmingmultipletraderliststofiles) |
| 30 | `ConfirmingMultipleTrader::ConsensusMode` | public property (pass-through) | [Consensus Katmanı](#consensus-katmanı-_signalmultipletrader) |
| 31 | `ConfirmingMultipleTrader::ConsensusMinNetCount` | public property (pass-through) | [Consensus Katmanı](#consensus-katmanı-_signalmultipletrader) |
| 34 | `ConfirmingMultipleTrader::_confirmer` | private field | [ConfirmingSingleTrader § VirtualPositionConfirmer](04-confirmingsingletrader.md#virtualpositionconfirmer--ortak-konfirmasyon-motoru) |
| 35-40 | `ThresholdIsPercentage`…`FlattenImmediatelyOnFlatSignal` (6 pass-through property) | public property | [ConfirmingSingleTrader § VirtualPositionConfirmer](04-confirmingsingletrader.md#virtualpositionconfirmer--ortak-konfirmasyon-motoru) |
| 43 | `ConfirmingMultipleTrader::_virtualYonHistory` | private field | [Lists Export](#lists-export-writeconfirmingmultipletraderliststofiles) |
| 44 | `ConfirmingMultipleTrader::_confirmedHistory` | private field | [Lists Export](#lists-export-writeconfirmingmultipletraderliststofiles) |
| 45-47 | `VirtualYon`/`VirtualEntryPrice`/`IsConfirmed` (pass-through) | public property | [ConfirmingSingleTrader § VirtualPositionConfirmer](04-confirmingsingletrader.md#virtualpositionconfirmer--ortak-konfirmasyon-motoru) |
| 50 | `ConfirmingMultipleTrader::VirtualSignals` | public property (computed) | [Dönüş/Sonuç](#dönüş--sonuç--global-state) — consensus'un ham sinyal timeline'ı |
| 51 | `ConfirmingMultipleTrader::Signals` | public property (computed) | [Dönüş/Sonuç](#dönüş--sonuç--global-state) — konfirme edilmiş sinyal timeline'ı |
| 54 | `ConfirmingMultipleTrader::ConfirmingMultipleTrader(...)` | constructor | [Kurulum](#kurulum) |
| 56 | `ConfirmingMultipleTrader::AddTrader(trader)` | public method | [Consensus Katmanı](#consensus-katmanı-_signalmultipletrader) |
| 59 | `ConfirmingMultipleTrader::Reset()` | public method | [Kurulum](#kurulum) |
| 60 | `ConfirmingMultipleTrader::Init()` | public method | [Kurulum](#kurulum) |
| 63 | `ConfirmingMultipleTrader::ResolveConfirmedSignal(i)` | private method | [Run() — Konfirmasyon Akışı](#run--konfirmasyon-akışı) |
| 64 | `ConfirmingMultipleTrader::Run(i)` | public method | [Run() — Konfirmasyon Akışı](#run--konfirmasyon-akışı) |
| 67 | `ConfirmingMultipleTrader::Finalize()` | public method | [Finalize()](#finalize) |
| 70 | `ConfirmingMultipleTrader::GetMainTrader()` | public method | [Main/Signal Trader Access](#mainsignal-trader-access-getmaintrader--getsignalmultipletrader--setcallbacks--stop) |
| 71 | `ConfirmingMultipleTrader::GetSignalMultipleTrader()` | public method | [Main/Signal Trader Access](#mainsignal-trader-access-getmaintrader--getsignalmultipletrader--setcallbacks--stop) |
| 72 | `ConfirmingMultipleTrader::SetCallbacks(...)` | public method | [Main/Signal Trader Access](#mainsignal-trader-access-getmaintrader--getsignalmultipletrader--setcallbacks--stop) |
| 78 | `ConfirmingMultipleTrader::Stop()` | public method | [Main/Signal Trader Access](#mainsignal-trader-access-getmaintrader--getsignalmultipletrader--setcallbacks--stop) |
| 81-92 | `WriteConfirmingMultipleTraderListsToFiles`…`GetSinyal` (12 dosya-yazma/okuma yardımcısı) | method | [Lists Export](#lists-export-writeconfirmingmultipletraderliststofiles) |
| 95 | `ConfirmingMultipleTrader::Dispose()` | public method | [Kurulum](#kurulum) |

## Public API

### Kurulum

- `ConfirmingMultipleTrader(id, data, indicators, logger)` — constructor: `_signalMultipleTrader
  = new MultipleTrader(id, data, indicators, logger)` (id, `-1` DEĞİL — signal katmanının kendi
  `id`'si bu), `_mainTrader = new SingleTrader(-1, "mainTrader", ...) { RunMode = TradeOnly }`.
  `IsInitialized = true`.
- `AddTrader(trader)` → sadece `_signalMultipleTrader.AddTrader(trader)` — çağıran taraf child'ı
  `AddTrader`'dan ÖNCE tamamen Reset/configure/Init etmiş olmalı (`MultipleTrader.AddTrader` ile
  aynı sözleşme, bkz. [MultipleTrader § Kimlik ve Kurulum](03-multipletrader.md#kimlik-ve-kurulum)).
- `Reset()` → `_signalMultipleTrader.Reset()` + **`_signalMultipleTrader.GetMainTrader().Reset()`**
  (kod içi yorum: "`MultipleTrader.Reset()` kendi mainTrader'ını resetlemiyor" — bu satır
  olmasaydı signal katmanının mainTrader'ı hiç resetlenmezdi) + `_mainTrader.Reset()` +
  `_confirmer.Reset()` + state flag'leri.
- `Init()` (`156-178`) — `signalMain = _signalMultipleTrader.GetMainTrader()`; `signalMain.RunMode
  = TradeOnly`; **`signalMain.Init()`** (yine "`MultipleTrader.Init()` kendi mainTrader'ını init
  etmiyor" gerekçesiyle elle çağrılıyor); ardından **`signalMain.signals.AlEnabled/SatEnabled/
  FlatOlEnabled = true`** elle set ediliyor — bkz. aşağıdaki Not (kod içinde belgelenmiş, ÇÖZÜLMÜŞ
  bir geçmiş hata). Son olarak `_signalMultipleTrader.Init()` + `_mainTrader.Init()` +
  `_virtualYonHistory`/`_confirmedHistory` dizilerinin ayrılması.

> **Not — kod içinde belgelenmiş, ÇÖZÜLMÜŞ bir geçmiş hata (referans için bırakılmış):**
> `ConfirmingMultipleTrader.cs:162-168`'deki yorum: *"KRİTİK: `SingleTrader.signals.AlEnabled/
> SatEnabled/FlatOlEnabled` varsayılan olarak FALSE (`ConfigureUserFlagsOnce()`/`Signals.Reset()`
> ile) — normal `MultipleTrader` akışında bunlar `ApplySingleTraderFlagsConfigs(mainTrader)` ile
> AppConfig'den açılıyor, ama burada `signalMain`'i biz kendimiz kuruyoruz, o çağrı hiç
> yapılmıyor. Açılmazsa `MapStrategyCommandsToTradeCommands()` consensus Buy/Sell'i sessizce yok
> sayar ... `signalMain` `SonYon`'u sonsuza kadar 'F' kalır, konfirmasyon hiç tetiklenmez.
> (Gerçek veride bulunmuş bir hata — bkz. `docs/todo.md`.)"* — bu artık DÜZELTİLMİŞ (satır
> 169-171'de 3 bayrak elle `true` yapılıyor); yorum, benzer bir "signalMain'i elle kurma"
> deseni yazacak biri için bırakılmış bir uyarı/ders niteliğinde. Aktif bir bulgu DEĞİL, ama
> `signalMultipleTrader.GetMainTrader()`'ı manuel kuran her yeni kod yolu aynı tuzağa düşebilir
> — [ConfirmingSingleTrader](04-confirmingsingletrader.md)'da bu sorun yok çünkü
> `_signalTrader.SetStrategy(strategy)` çağrılan `_signalTrader` `AppConfigApplier`'ın
> `ApplyConfirmingSignalTraderFlagsConfigs`'i ile normal yoldan kuruluyor.

### Consensus Katmanı: `_signalMultipleTrader`

`_signalMultipleTrader`, [MultipleTrader](03-multipletrader.md)'ın KENDİSİ — hiç değiştirilmeden
reuse ediliyor. `ConsensusMode`/`ConsensusMinNetCount` bu instance'a doğrudan pass-through
(`get => _signalMultipleTrader.ConsensusMode` vb.) — [MultipleTrader'daki 4 hazır
mod](03-multipletrader.md#consensus--buildconsensussignal) (`Net`/`Majority`/`All`/`Any`) burada
da AYNEN geçerli. `CustomConsensusFunc` (MultipleTrader'ın script-only genişletme noktası)
BURADAN dışarı açılmıyor — `ConfirmingMultipleTrader` sadece `ConsensusMode`/`ConsensusMinNetCount`'u
sarıyor, `_signalMultipleTrader.CustomConsensusFunc`'a erişmek istersen `GetSignalMultipleTrader().CustomConsensusFunc
= ...` ile DOĞRUDAN alt nesneye inmen gerekir (script'ten mümkün, ama sınıfın kendi API'sinde
bir kısayol yok).

### Run() — Konfirmasyon Akışı

```csharp linenums="1"
public void Run(int i)
{
    if (i >= Data.Count)
        return;

    _signalMultipleTrader.Run(i);   // ← GERÇEK MultipleTrader.Run(): her child + consensus + signalMain, hepsi TAM çalışır

    _mainTrader.ExecutePreOrderMethods(i);

    if (i < 1)
        return;

    TradeSignals signalForMainTrader = ResolveConfirmedSignal(i);

    _virtualYonHistory[i] = _confirmer.VirtualYon ?? "-";
    _confirmedHistory[i]  = _confirmer.IsConfirmed;

    _mainTrader.strategySignal = signalForMainTrader;
    _mainTrader.MapStrategyCommandsToTradeCommands(_mainTrader.strategySignal);
    _mainTrader.ApplyTimingFilters(i);
    _mainTrader.ApplyEquityCurveFilter(i);
    _mainTrader.ResolveFilterDecisions(i);
    _mainTrader.ExecutePostOrderMethods(i);

    OnProgress?.Invoke(this, i + 1, Data.Count);
}
```

- `_signalMultipleTrader.Run(i)` — [MultipleTrader'ın TAM `Run()` pipeline'ı](03-multipletrader.md#run--çocuk-traderları--maintrader-pipeline)
  çalışır: her child GERÇEKTEN trade eder, consensus üretilir, `signalMain` (MultipleTrader'ın
  kendi mainTrader'ı) da kendi elle-tekrarlanmış pipeline'ıyla çalışır. Bu katmanda ÜÇ seviye
  "gerçek trade simülasyonu" var: her child (kendi defterinde), `signalMain` (consensus'u
  kendi gerçek pozisyonuna çeviriyor, ama bu pozisyon SADECE `signalMultipleTrader`'ın kendi
  istatistikleri için — bizim asıl `mainTrader`'ımıza hiç yansımıyor), ve bizim `_mainTrader`
  (konfirme edilmiş sinyalle, gerçek "asıl" sonuç).
- `ResolveConfirmedSignal(i)` (`188-196`) — `signalMain.SonYon`/`signalMain.strategySignal`
  (`signalMain = _signalMultipleTrader.GetMainTrader()`) + `Data[i].Close`'u `_confirmer.Resolve(...)`'a
  geçirir — [ConfirmingSingleTrader'daki `ResolveConfirmedSignal`](04-confirmingsingletrader.md#run--konfirmasyon-akışı)
  ile TEK FARKI: kaynak `_signalTrader` değil `signalMain` (`_signalMultipleTrader.GetMainTrader()`).
- `_mainTrader`'ın pipeline'ı [ConfirmingSingleTrader'ınkiyle](04-confirmingsingletrader.md#run--konfirmasyon-akışı)
  BİREBİR aynı (`ExecutePreOrderMethods` → `MapStrategyCommandsToTradeCommands` → `ApplyTimingFilters`
  → `ApplyEquityCurveFilter` → `ResolveFilterDecisions` → `ExecutePostOrderMethods`, `SingleTrader.Run()`
  hiç çağrılmadan) — `mainTrader.OnRun` burada da ASLA tetiklenmez.
- `OnProgress?.Invoke(this, i + 1, Data.Count)` — burada [ConfirmingSingleTrader'daki `_ =
  percentage;` kalıntısı](04-confirmingsingletrader.md#run--konfirmasyon-akışı) YOK, kod biraz
  daha temiz (hesaplanmayan bir `percentage` değişkeni yok) ama imza yine de yüzdelik almıyor.

### Finalize()

`ConfirmingSingleTrader.Finalize()` ile AYNI yapı — tek fark `_signalMultipleTrader.Finalize()`
çağrısı (child'ları finalize eder + [MultipleTrader'ın kendi
`Finalize()`'ı](03-multipletrader.md#finalize) gibi `signalMain.CalculateStatistics()`/
`CalculatePerformances()`'ı da tetikler), sonra `_mainTrader.CalculateStatistics()`/
`CalculatePerformances()`, sonra (`SaveStatisticsToFile` ise) `WriteConfirmingMultipleTraderListsToFiles()`.

### Main/Signal Trader Access: `GetMainTrader()` / `GetSignalMultipleTrader()` / `SetCallbacks()` / `Stop()`

- `GetMainTrader()` → `_mainTrader`. `GetSignalMultipleTrader()` → `_signalMultipleTrader` — sinyal
  katmanına DOĞRUDAN erişim (child'ların kendi verisi, consensus ayarları, ve istenirse
  `signalMultipleTrader`'ın kendi composite lists dosyalarını yazmak için, bkz. [Dönüş/Sonuç](#dönüş--sonuç--global-state)).
- `SetCallbacks(...)` — hem `_mainTrader` hem `_signalMultipleTrader`'a (yani DOLAYLI olarak
  onun mainTrader'ına VE tüm child'larına, [MultipleTrader::SetCallbacks](03-multipletrader.md#maintrader-yardımcıları-getmaintrader--setcallbacks--stop)
  üzerinden) aynı callback setini bağlar. Console akışı bunu YİNE kullanmıyor (bkz. aşağıdaki Not
  — [ConfirmingSingleTrader'daki aynı desenle](04-confirmingsingletrader.md#runconfirmingsingletraderwithprogressasync--tam-kaynak-algotradercs2160-2394) tutarlı).
- `Stop()` — `IsRunning` ise `IsStopRequested = true` + log.

### Lists Export: `WriteConfirmingMultipleTraderListsToFiles(...)`

- [ConfirmingSingleTrader'ın kendi Lists
  Export'uyla](04-confirmingsingletrader.md#lists-export-writeconfirmingsingletraderliststofiles)
  AYNI yapı — TEK fark: `signalMain` (`_signalMultipleTrader.GetMainTrader()`) kolonları
  `signalTrader` yerine geçiyor, CSV header'ı `SignalConsensus_Yon`/`Seviye`/`Sinyal` (tekil
  `SignalTrader_*` değil — çünkü kaynak artık N child'ın consensus'u).
- TXT dosyasının başlığında EK bir satır var: `ConsensusMode`/`ConsensusMinNetCount`/`ChildCount`
  (`_signalMultipleTrader.Traders.Count`) — hangi consensus ayarlarıyla üretildiğini gösterir.
- Dosyanın hangi KLASÖRE yazıldığı konusunda önemli bir bulgu var — bkz. [Dönüş/Sonuç § Global
  State](#dönüş--sonuç--global-state).

## Çağrı Zinciri — Menüden Çağrılma (Program.cs → AlgoTrader → ConfirmingMultipleTrader)

1. `handleConfirmingMultipleTrader()` (`Program.cs:3170-`) — aynı desen: `reloadAppConfig()` →
   `showModeConfigSummary(...)` → `[ENTER]/[E]/[R]/[B]` → `showConfirmingMultipleTraderRunPreview()`
   → `[ENTER]/[E]/[R]/[B]` → `runConfirmingMultipleTraderAlgoTrade()`.
2. `runConfirmingMultipleTraderAlgoTrade()` (`Program.cs:955-1014`) — `stockDataReader`/`IsDataReady`
   kontrolü → `new AlgoTrader(...)` + logger/timer + `SetData(...)` → **`AppConfigApplier.ApplyConfirmingMultipleTrader(algoTrader,
   appConfig.ConfirmingMultipleTrader, AppSettings.ConfigsDir)`** (bkz. [AppConfig
   Kaynağı](#appconfig-kaynağı--confirmingmultipletraderconfig)) → `Initialize()` → **`await
   algoTrader.RunConfirmingMultipleTraderWithProgressAsync()`** → `WriteTraderDataToFilesAsync(algoTrader.ConfirmingMultipleTrader!)`
   → `mainTrader.PlotEnabled` ise `SetupPython()` + `PlotSingleTraderData(mainTrader)` (AYNI
   "VirtualSignals overlay henüz yok" notuyla, bkz. [ConfirmingSingleTrader'daki aynı
   davranış](04-confirmingsingletrader.md#çağrı-zinciri--menüden-çağrılma-programcs--algotrader--confirmingsingletrader)).
3. `AlgoTrader.RunConfirmingMultipleTraderWithProgressAsync()` (`AlgoTrader.cs:2442-2634`) —
   `createConfirmingChildTraders(confirmingMultipleTrader)` (`AlgoTrader.cs:1759-1869`) çağrılır —
   [`createChildTraders()`'ın](02-singletrader.md#çağrı-zinciri--menüden-çağrılma-programcs--algotrader--singletrader)
   birebir kopyası, tek fark hedefin `multipleTrader.AddTrader(...)` yerine
   `confirmingMultipleTrader.AddTrader(...)` olması (kod içi yorumda da açıkça belirtilmiş:
   "createChildTraders()'ın ConfirmingMultipleTrader karşılığı — birebir aynı desen").

Notable fark [ConfirmingSingleTrader'dan](04-confirmingsingletrader.md#çağrı-zinciri--menüden-çağrılma-programcs--algotrader--confirmingsingletrader):
burada AYRI bir "SignalTrader flags" adımı YOK — child'lar `AppConfig.ConfirmingMultipleTrader.ChildTraders[i]`'den
(MultipleTrader'ın `ChildTraders` şemasıyla BİREBİR aynı) geliyor, `signalMain`'in bayrakları ise
`Init()`'in kendi içinde elle `true` yapılıyor (bkz. yukarıdaki Not) — `ApplyConfirmingSignalTraderFlagsConfigs`
gibi ayrı bir config-application fonksiyonu bu sınıf için YOK.

## AppConfig Kaynağı — `ConfirmingMultipleTraderConfig`

```csharp linenums="1"
public class ConfirmingMultipleTraderConfig
{
    public string RunMode { get; set; } = "TradeOnly";   // Şimdilik sadece TradeOnly desteklenir
    public ConfirmingMultipleTraderSaveConfig Save         { get; set; } = new();
    public ConsensusConfig                    Consensus    { get; set; } = new();   // MultipleTrader.Consensus ile aynı şema
    public ConfirmationConfig                 Confirmation { get; set; } = new();   // ConfirmingSingleTrader.Confirmation ile aynı şema
    public ConfirmingMainTraderConfig         MainTrader   { get; set; } = new();
    public List<ChildTraderEntry>             ChildTraders { get; set; } = new();   // MultipleTrader.ChildTraders ile aynı şema
}

public class ConfirmingMultipleTraderSaveConfig
{
    public bool   SaveStatisticsToFile                        { get; set; } = true;
    public bool   SaveConfirmingMultipleTraderListsTxtEnabled { get; set; } = true;
    public bool   SaveConfirmingMultipleTraderListsCsvEnabled { get; set; } = true;
    public string ConfirmingMultipleTraderListsTxtFileName    { get; set; } = "ConfirmingMultipleTraderLists.txt";
    public string ConfirmingMultipleTraderListsCsvFileName    { get; set; } = "ConfirmingMultipleTraderLists.csv";
    public string FilePrefix { get; set; } = "ConfirmingMultipleTrader";   // SignalChild{i} → {FilePrefix}_SignalChild{i}_{FileName} | MainTrader → {FilePrefix}_Main_{FileName}
    public bool WriteSignalMultipleTraderListsToFiles { get; set; } = false;   // true → signal katmanının kendi composite lists dosyası da yazılır
    public bool WriteSignalChildTradersDataToFiles    { get; set; } = false;   // true → signal katmanındaki child istatistikleri de dosyaya yazılır
}
```

`AppConfig.json`'daki gerçek karşılığı (`inputs/configs/AppConfig/AppConfig.json:499-`,
kısaltılmış — `TradeParams`/`ChildTraders` şemaları [MultipleTrader § AppConfig
Kaynağı](03-multipletrader.md#appconfig-kaynağı--multipletraderconfig)'nda birebir aynı):

```json linenums="1"
"ConfirmingMultipleTrader": {
    "RunMode": "TradeOnly",
    "Save": {
      "SaveStatisticsToFile": true,
      "SaveConfirmingMultipleTraderListsTxtEnabled": true,
      "SaveConfirmingMultipleTraderListsCsvEnabled": true,
      "ConfirmingMultipleTraderListsTxtFileName": "ConfirmingMultipleTraderLists.txt",
      "ConfirmingMultipleTraderListsCsvFileName": "ConfirmingMultipleTraderLists.csv",
      "FilePrefix": "ConfirmingMultipleTrader",
      "WriteSignalMultipleTraderListsToFiles": false,
      "WriteSignalChildTradersDataToFiles": false
    },
    "Consensus": { "Mode": "Net", "MinNetCount": 1 },
    "Confirmation": {
      "ThresholdIsPercentage": false,
      "ProfitThreshold": 5000.0,
      "LossThreshold": -3000.0,
      "Trigger": "Both",
      "ConflictMode": "CancelAndRestart",
      "FlattenImmediatelyOnFlatSignal": true
    },
    "MainTrader": {
      "EquityCurveFilter": { "ConfigFile": "EquityCurveFilterConfig.txt", "Name": "", "Version": "v1" },
      "TradeParams": { "MarketType": "FxCrypto", "IlkBakiye": 100000.0, "...": "..." },
      "Signals": { "...": "... (SingleTrader ile aynı şema)" },
      "Save": { "...": "... (12 flag + 12 dosya adı)" }
    },
    "ChildTraders": [
      { "ChildId": 0, "Strategy": { "ConfigFile": "StrategyConfig.txt", "Name": "SimpleMostStrategy", "Version": "v1" }, "...": "..." },
      { "ChildId": 1, "Strategy": { "...": "... (farklı Version, örn. v2)" } }
    ]
}
```

- `WriteSignalMultipleTraderListsToFiles`/`WriteSignalChildTradersDataToFiles` — ÖDÜNÇ ALINMIŞ
  gibi görünse de GERÇEKTEN kullanılıyor: `WriteTraderDataToFilesAsync(ConfirmingMultipleTrader)`
  (`AlgoTrader.cs:2682-2699`) bu iki bayrağı okuyup, `true` ise `signalMultipleTrader.WriteMultipleTraderListsToFiles(...)`/
  `WriteMultipleTraderStatistics(...)` + (ikincisi de `true` ise) her child'ın kendi
  `WriteStatisticsToFile(...)`'ını tetikliyor — [MultipleTrader'ın kendi dosya
  yazımı](03-multipletrader.md#bar-bar-liste-çıktısı-writemultipletraderliststofiles) BİREBİR
  reuse ediliyor.

## Callback'lerin Gerçek Gövdeleri

`mainTrader`'a bağlanan callback seti [SingleTrader/MultipleTrader ile TAMAMEN AYNI](02-singletrader.md#callbacklerin-gerçek-gövdeleri-algotradercs158-223)
— `RunConfirmingMultipleTraderWithProgressAsync()`'te `mainTrader.ClearCallbacks().SetCallbacks(...)`
doğrudan çağrılıyor. Sinyal katmanı (`_signalMultipleTrader`'ın child'ları + kendi mainTrader'ı)
`createConfirmingChildTraders(...)` içinde AYNI callback setiyle (her child için ayrı ayrı)
bağlanıyor — [SingleTrader'ın `createChildTraders()`'taki callback
bağlama](02-singletrader.md#çağrı-zinciri--menüden-çağrılma-programcs--algotrader--singletrader)
ile aynı desen.

## Dönüş / Sonuç — Global State

| Değişken/Erişim | Tip | Kaynak |
|---|---|---|
| `algoTrader.ConfirmingMultipleTrader` | `ConfirmingMultipleTrader` (public getter, `private set`) | `RunConfirmingMultipleTraderWithProgressAsync()` içinde yaratılan `confirmingMultipleTrader` |
| `.GetMainTrader()`/`.GetSignalMultipleTrader()` | `SingleTrader`/`MultipleTrader` | mainTrader (gerçek trade) / signal katmanı (consensus üreten `MultipleTrader`) |
| `{FilePrefix}_Main_*` (mainTrader dosyaları) | dosya | `WriteTraderDataToFilesAsync` → `AppSettings.LogsDir` |
| `{FilePrefix}_SignalChild{i}_*`, signal katmanının `MultipleTraderLists`/`MultipleTraderStatistics` dosyaları | dosya (opsiyonel) | `WriteSignalMultipleTraderListsToFiles`/`WriteSignalChildTradersDataToFiles` `true` ise, `AppSettings.LogsDir` |
| `ConfirmingMultipleTraderLists.txt`/`.csv` | dosya | `confirmingMultipleTrader.Finalize()` içinde — bkz. AŞAĞIDAKİ Not, FARKLI (yanlış) klasöre yazılıyor |

> **Not — `ConfirmingMultipleTraderLists.txt`/`.csv` de [ConfirmingSingleTrader'daki AYNI
> hatayı](04-confirmingsingletrader.md#dönüş--sonuç--global-state) taşıyor:**
> `WriteConfirmingMultipleTraderListsToTxt()`/`ToCsv()` (`ConfirmingMultipleTrader.cs:312`, `375`)
> BİREBİR aynı satırı kullanıyor:
> ```csharp linenums="1"
> var logDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
> ```
> — proje genelinde her yerde kullanılan `AppSettings.LogsDir` (`outputs/logs/`) DEĞİL,
> derlenmiş exe'nin bulunduğu klasördeki bir `logs/` alt klasörü. Aynı bulgu
> [ConfirmingSingleTrader'da](04-confirmingsingletrader.md#dönüş--sonuç--global-state) da var —
> muhtemelen `ConfirmingMultipleTrader.cs`, `ConfirmingSingleTrader.cs`'ten kopyala-yapıştırla
> türetilirken bu satır da birlikte kopyalanmış. `mainTrader`/signal katmanının dosyaları
> (`WriteTraderDataToFilesAsync` üzerinden, `AppSettings.LogsDir` kullanıyor) doğru yere giderken,
> bu sınıfın EN ÖZGÜN çıktısı (bar-bar signal-consensus/sanal/mainTrader karşılaştırması) yine
> farklı, kolay gözden kaçan bir klasöre düşüyor.

## Tipik Kullanım — Script'ten Çağrılma

- Gerçek örnek: `inputs/scripts/07_RunConfirmingMultipleTraderWithProgressAsync.csx`.
- Desen [ConfirmingSingleTrader'ın script'iyle](04-confirmingsingletrader.md#tipik-kullanım--scriptten-çağrılma)
  neredeyse aynı — TEK fark, tek strateji yerine N child + consensus kurulması.

**1) Kurulum + Consensus + Confirmation ayarları**

```csharp linenums="1"
var confirmingMultipleTrader = new ConfirmingMultipleTrader(0, data, indicators, null);
confirmingMultipleTrader.Reset();

confirmingMultipleTrader.ConsensusMode = consensusMode;
confirmingMultipleTrader.ConsensusMinNetCount = consensusMinNetCount;

confirmingMultipleTrader.ThresholdIsPercentage = thresholdIsPercentage;
confirmingMultipleTrader.ProfitThreshold = profitThreshold;
confirmingMultipleTrader.LossThreshold = lossThreshold;
confirmingMultipleTrader.Trigger = confirmationTrigger;
confirmingMultipleTrader.ConflictMode = conflictMode;
confirmingMultipleTrader.FlattenImmediatelyOnFlatSignal = flattenImmediatelyOnFlatSignal;
```

**2) mainTrader kurulumu**

```csharp linenums="1"
var mainTrader = confirmingMultipleTrader.GetMainTrader();
mainTrader.initialTradeParams!.Reset()
    .SetBakiyeParams(ilkBakiye: ilkBakiye)
    .SetKontratParamsFxCrypto(lotSayisi: lotSayisi)
    .SetKomisyonParams(komisyonCarpan: komisyonCarpan)
    .SetKaymaParams(kaymaMiktari: kaymaMiktari);
mainTrader.RunMode = TraderRunMode.TradeOnly;
mainTrader.ConfigureUserFlagsOnce();
mainTrader.signals.AlEnabled = true;
mainTrader.signals.SatEnabled = true;
mainTrader.signals.FlatOlEnabled = true;
```

**3) Child'ları oluştur ve signal katmanına ekle** (`AddChild` yardımcı fonksiyonu, [MultipleTrader'ın
script örneğindeki](03-multipletrader.md#tipik-kullanım--scriptten-çağrılma-customconsensusfunc-örneği)
`AddChild`'a çok benzer — tek fark `confirmingMultipleTrader.AddTrader(child)` çağırması)

```csharp linenums="1"
void AddChild(int childId, IStrategy strategy)
{
    var child = new SingleTrader(childId, $"childTrader_{childId}", data, indicators, null);
    child.SetStrategy(strategy);
    child.RunMode = TraderRunMode.TradeOnly;
    child.Reset();
    child.initialTradeParams!.Reset()
        .SetBakiyeParams(ilkBakiye: ilkBakiye)
        .SetKontratParamsFxCrypto(lotSayisi: lotSayisi)
        .SetKomisyonParams(komisyonCarpan: komisyonCarpan)
        .SetKaymaParams(kaymaMiktari: kaymaMiktari);
    child.ConfigureUserFlagsOnce();
    child.signals.AlEnabled = true;
    child.signals.SatEnabled = true;
    child.signals.FlatOlEnabled = true;
    child.Init();
    confirmingMultipleTrader.AddTrader(child);
}

AddChild(0, childStrategy0);
AddChild(1, childStrategy1);

confirmingMultipleTrader.Init();   // signalMain + tüm child'lar + gerçek mainTrader burada init edilir
```

**4) Bar-bar çalıştır + Finalize**

```csharp linenums="1"
confirmingMultipleTrader.IsStarted = true;
confirmingMultipleTrader.IsRunning = true;

for (int i = 0; i < totalBars; i++)
    confirmingMultipleTrader.Run(i);

confirmingMultipleTrader.Finalize();
```

## Console/JSON Eşleşmesi

1. `inputs/configs/AppConfig/AppConfig.json` dosyasını aç.
2. `"ConfirmingMultipleTrader"` bölümünü düzenle (bkz. yukarıdaki [AppConfig
   Kaynağı](#appconfig-kaynağı--confirmingmultipletraderconfig) tam örnek): `ChildTraders`
   dizisine her child için `Strategy`, `Consensus.Mode` ile birleştirme kuralını,
   `Confirmation` ile eşik/mod ayarlarını seç.
3. Kaydet, Console'u çalıştır, menüden `[24] ConfirmingMultipleTrader` (veya `[25]` "Read Data +
   ConfirmingMultipleTrader") seç.

## Kimler Kullanıyor — Instantiation Noktaları

`new ConfirmingMultipleTrader(...)` için tüm kod tabanında grep taraması — **2 çağırım noktası**:

| Dosya | Bağlam | Satır |
|---|---|---|
| `AlgoTrade.Core/Trading/AlgoTrader.cs` | `RunConfirmingMultipleTraderWithProgressAsync()` — `confirmingMultipleTrader` (id=0) | 2485 |
| `inputs/scripts/07_RunConfirmingMultipleTraderWithProgressAsync.csx` | top-level akış — `confirmingMultipleTrader` | 92 |

## Kullanım Haritası

| Üye | Durum | Nerede |
|---|---|---|
| Constructor, `AddTrader`, `Reset`, `Init`, `Run`, `Finalize`, `GetMainTrader`, `GetSignalMultipleTrader`, `Dispose` | ✅ | `RunConfirmingMultipleTraderWithProgressAsync()` (yukarıda tam kaynağıyla var) |
| `ConsensusMode`/`ConsensusMinNetCount` | ✅ | `AppConfig.ConfirmingMultipleTrader.Consensus` üzerinden |
| `ThresholdIsPercentage`/`ProfitThreshold`/`LossThreshold`/`Trigger`/`ConflictMode`/`FlattenImmediatelyOnFlatSignal` | ✅ | `AppConfig.ConfirmingMultipleTrader.Confirmation` üzerinden |
| `WriteSignalMultipleTraderListsToFiles`/`WriteSignalChildTradersDataToFiles` (Save config alanları) | ✅ | `WriteTraderDataToFilesAsync(ConfirmingMultipleTrader)` |
| `VirtualYon`/`VirtualEntryPrice`/`IsConfirmed` | ✅ (dolaylı) | `Run(i)`'nin diagnostic geçmişini doldurmasında |
| `VirtualSignals`/`Signals` | ⚠️ | [ConfirmingSingleTrader'daki aynı durum](04-confirmingsingletrader.md#kullanım-haritası) — plot overlay için tasarlanmış, henüz kullanılmıyor |
| `SetCallbacks(...)` (sınıfın kendi toplu metodu) | ❌ (Console akışında) | Console `mainTrader`'a ve child'lara AYRI AYRI bağlıyor, bu toplu metodu çağırmıyor |
| `Stop()` | ❌ | Hiçbir yerden çağrılmıyor |
| `_signalMultipleTrader.CustomConsensusFunc` | ❌ (bu sınıfın API'sinden) | `ConfirmingMultipleTrader` bunu dışarı açmıyor — `GetSignalMultipleTrader().CustomConsensusFunc` ile dolaylı erişilebilir ama hiçbir Console/script akışı bunu yapmıyor |

## İlgili Dosyalar

- [01-class-reference.md § 4. ConfirmingSingleTrader / ConfirmingMultipleTrader /
  VirtualPositionConfirmer](../01-class-reference.md#8-virtualpositionconfirmer--ortak-konfirmasyon-motoru) —
  bu sayfanın ait olduğu index.
- [04-confirmingsingletrader.md](04-confirmingsingletrader.md) — `VirtualPositionConfirmer`'ın
  tam olarak ele alındığı kardeş sayfa.
- [03-multipletrader.md](03-multipletrader.md) — `_signalMultipleTrader`'ın gerçek tipi.
- [02-singletrader.md](02-singletrader.md) — `_mainTrader`'ın gerçek tipi.
- [python-plotter.md](python-plotter.md) — `mainTrader.PlotEnabled` ile tetiklenen plot akışı.
- [06-class-doc-method.md](../06-class-doc-method.md) — bu sayfanın yazıldığı yöntem.
- `docs/todo.md` — "Getiri Eğrisi / KarZarar Eğrisi Konfirmasyonu (Madde 3)" tasarım tartışması.
