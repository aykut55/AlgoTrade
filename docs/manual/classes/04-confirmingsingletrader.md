# ConfirmingSingleTrader — Sanal Pozisyon Konfirmasyonu (Menü [22])

> [Class Reference](../01-class-reference.md) setinin bir parçası — [SingleTrader](02-singletrader.md)/
> [MultipleTrader](03-multipletrader.md) gibi aynı derinlikte işlendi. Kardeş sayfa:
> [ConfirmingMultipleTrader](04-confirmingmultipletrader.md) (aynı konfirmasyon motorunu — bu
> sayfadaki `VirtualPositionConfirmer` — bir `MultipleTrader` consensus'u üzerinde kullanır).
> Yöntem: [06-class-doc-method.md](../06-class-doc-method.md).

### Dosyalar

- `src/AlgoTrade.Core/Trading/Traders/ConfirmingSingleTrader.cs` (469 satır)
- `src/AlgoTrade.Core/Trading/Core/VirtualPositionConfirmer.cs` (175 satır) — asıl konfirmasyon
  state machine'i, `ConfirmingMultipleTrader` ile PAYLAŞILAN, bu sayfada tam olarak ele alınıyor.
- `src/AlgoTrade.Core/Trading/Traders/SingleTrader.cs` — hem `_signalTrader` hem `_mainTrader`
  birer `SingleTrader` instance'ı, bkz. [SingleTrader dokümanı](02-singletrader.md).

### Rolü

- Tek bir stratejinin Al/Sat sinyallerini **gerçek pozisyona çevirmeden önce** sanal (paper) bir
  pozisyonla "konfirme" eder. Strateji Al/Sat dediğinde gerçek emir AÇILMAZ — o bar'ın
  fiyatından sanal bir pozisyon takip edilmeye başlanır. Sanal pozisyonun anlık K/Z'i
  `ProfitThreshold`/`LossThreshold` eşiğini (`Trigger`'a göre) geçtiği ANDA — orijinal sinyal
  fiyatından değil, o barın fiyatından — gerçek sinyal `mainTrader`'a iletilir.
- Yön HİÇBİR ZAMAN ters çevrilmez, mekanizma sadece **giriş zamanlamasını geciktirir**.
  Konfirmasyondan sonra çıkış tamamen normal trade yönetimine bırakılır — bu katman bir daha
  filtre uygulamaz.
- `SymbolScanner` gibi `AlgoTrader`'dan bağımsız, kendi başına yeten bir sınıf.
  [MultipleTrader](03-multipletrader.md)'ın yapısına benziyor (bir sinyal-kaynağı `SingleTrader`
  + ayrı bir `mainTrader`, dışarıdan çözümlenmiş sinyal alır) ama consensus yerine sanal pozisyon
  + eşik konfirmasyonu var.
- `SingleTrader.ApplyEquityCurveFilter`'dan **FARKLI bir mekanizma** (bkz. [SingleTrader §
  Equity Curve Filter](02-singletrader.md#equity-curve-filter-configureequitycurvefilter--applyequitycurvefilter)) —
  o equity-curve tabanlı bir soft-block, bu ise sinyal-bazlı bir virtual-then-real state machine.

### Ne zaman kullanılır

- Ham stratejinin ürettiği her sinyali hemen trade etmek yerine, "önce biraz kâr/zarar
  potansiyelini gör, sonra karar ver" davranışı istediğinde. Console `[22]`-`[23]`.
- Tasarım tartışması ve eski projeyle karşılaştırma için bkz. `docs/todo.md`, "Getiri Eğrisi /
  KarZarar Eğrisi Konfirmasyonu (Madde 3)".

### Sınıf İskeleti (ilk bakış)

```csharp linenums="1"
public class ConfirmingSingleTrader
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

    private SingleTrader _signalTrader;
    private SingleTrader _mainTrader;

    public Action<ConfirmingSingleTrader, int, int>? OnProgress { get; set; }

    public bool SaveStatisticsToFile { get; set; } = true;

    // ---- Output file settings ----
    public string ConfirmingSingleTraderListsTxtFileName { get; set; } = "ConfirmingSingleTraderLists.txt";
    public string ConfirmingSingleTraderListsCsvFileName { get; set; } = "ConfirmingSingleTraderLists.csv";
    public bool SaveConfirmingSingleTraderListsTxtEnabled { get; set; } = true;
    public bool SaveConfirmingSingleTraderListsCsvEnabled { get; set; } = true;

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
    public List<double> VirtualSignals => _signalTrader.lists.SinyalList;
    public List<double> Signals => _mainTrader.lists.SinyalList;

    // ---- Kurulum ----
    public ConfirmingSingleTrader(int id, List<StockData> data, IndicatorManager indicators, LogManager? logger);

    public void SetStrategy(IStrategy strategy);

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
    public SingleTrader GetSignalTrader();
    public void SetCallbacks(
        Action<SingleTrader, int>? onReset = null, Action<SingleTrader, int>? onInit = null,
        Action<SingleTrader, int>? onRun = null, Action<SingleTrader, int>? onFinal = null,
        Action<SingleTrader, int>? onBeforeOrders = null, Action<SingleTrader, string, int>? onNotifySignal = null,
        Action<SingleTrader, int>? onAfterOrders = null, Action<SingleTrader, int, int, double>? onProgress = null,
        Action<SingleTrader>? onApplyUserFlags = null);
    public void Stop();

    // ---- Lists Export ----
    private void WriteConfirmingSingleTraderListsToFiles();
    private void WriteConfirmingSingleTraderListsToTxt();
    private void WriteHeaderTxt(System.IO.StreamWriter writer);
    private void WriteBarDataTxt(System.IO.StreamWriter writer, int barIndex);
    private void WriteConfirmingSingleTraderListsToCsv();
    private void WriteHeaderCsv(System.IO.StreamWriter writer);
    private void WriteBarDataCsv(System.IO.StreamWriter writer, int barIndex);
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
| 3 | `ConfirmingSingleTrader::Id` | public property | [Kurulum](#kurulum) |
| 4 | `ConfirmingSingleTrader::Data` | public property | [Kurulum](#kurulum) |
| 5 | `ConfirmingSingleTrader::Indicators` | public property | [Kurulum](#kurulum) |
| 6 | `ConfirmingSingleTrader::Logger` | public property | [Kurulum](#kurulum) |
| 8 | `ConfirmingSingleTrader::IsInitialized` | public property | [Kurulum](#kurulum) |
| 11 | `ConfirmingSingleTrader::IsStarted` | public property | [Run() — Konfirmasyon Akışı](#run--konfirmasyon-akışı) |
| 12 | `ConfirmingSingleTrader::IsRunning` | public property | [Run() — Konfirmasyon Akışı](#run--konfirmasyon-akışı) |
| 13 | `ConfirmingSingleTrader::IsStopped` | public property | [Run() — Konfirmasyon Akışı](#run--konfirmasyon-akışı) |
| 14 | `ConfirmingSingleTrader::IsStopRequested` | public property | [mainTrader/signalTrader Erişimi](#maintradersignaltrader-erişimi-getmaintrader--getsignaltrader--setcallbacks--stop) — `Stop()` ile set edilir |
| 16 | `ConfirmingSingleTrader::_signalTrader` | private field | [Kurulum](#kurulum) |
| 17 | `ConfirmingSingleTrader::_mainTrader` | private field | [Kurulum](#kurulum) |
| 19 | `ConfirmingSingleTrader::OnProgress` | public property (delegate) | [Run() — Konfirmasyon Akışı](#run--konfirmasyon-akışı) — bkz. Not, `percentage` hesaplanıp atılıyor |
| 21 | `ConfirmingSingleTrader::SaveStatisticsToFile` | public property | [Finalize()](#finalize) |
| 24 | `ConfirmingSingleTrader::ConfirmingSingleTraderListsTxtFileName` | public property | [Lists Export](#lists-export-writeconfirmingsingletraderliststofiles) |
| 25 | `ConfirmingSingleTrader::ConfirmingSingleTraderListsCsvFileName` | public property | [Lists Export](#lists-export-writeconfirmingsingletraderliststofiles) |
| 26 | `ConfirmingSingleTrader::SaveConfirmingSingleTraderListsTxtEnabled` | public property | [Lists Export](#lists-export-writeconfirmingsingletraderliststofiles) |
| 27 | `ConfirmingSingleTrader::SaveConfirmingSingleTraderListsCsvEnabled` | public property | [Lists Export](#lists-export-writeconfirmingsingletraderliststofiles) |
| 30 | `ConfirmingSingleTrader::_confirmer` | private field | [VirtualPositionConfirmer](#virtualpositionconfirmer--ortak-konfirmasyon-motoru) |
| 31 | `ConfirmingSingleTrader::ThresholdIsPercentage` | public property (pass-through) | [VirtualPositionConfirmer](#virtualpositionconfirmer--ortak-konfirmasyon-motoru) |
| 32 | `ConfirmingSingleTrader::ProfitThreshold` | public property (pass-through) | [VirtualPositionConfirmer](#virtualpositionconfirmer--ortak-konfirmasyon-motoru) |
| 33 | `ConfirmingSingleTrader::LossThreshold` | public property (pass-through) | [VirtualPositionConfirmer](#virtualpositionconfirmer--ortak-konfirmasyon-motoru) |
| 34 | `ConfirmingSingleTrader::Trigger` | public property (pass-through) | [VirtualPositionConfirmer](#virtualpositionconfirmer--ortak-konfirmasyon-motoru) |
| 35 | `ConfirmingSingleTrader::ConflictMode` | public property (pass-through) | [VirtualPositionConfirmer](#virtualpositionconfirmer--ortak-konfirmasyon-motoru) |
| 36 | `ConfirmingSingleTrader::FlattenImmediatelyOnFlatSignal` | public property (pass-through) | [VirtualPositionConfirmer](#virtualpositionconfirmer--ortak-konfirmasyon-motoru) |
| 39 | `ConfirmingSingleTrader::_virtualYonHistory` | private field | [Lists Export](#lists-export-writeconfirmingsingletraderliststofiles) — bar-bar diagnostic geçmişi |
| 40 | `ConfirmingSingleTrader::_confirmedHistory` | private field | [Lists Export](#lists-export-writeconfirmingsingletraderliststofiles) — bar-bar diagnostic geçmişi |
| 41 | `ConfirmingSingleTrader::VirtualYon` | public property (pass-through) | [VirtualPositionConfirmer](#virtualpositionconfirmer--ortak-konfirmasyon-motoru) |
| 42 | `ConfirmingSingleTrader::VirtualEntryPrice` | public property (pass-through) | [VirtualPositionConfirmer](#virtualpositionconfirmer--ortak-konfirmasyon-motoru) |
| 43 | `ConfirmingSingleTrader::IsConfirmed` | public property (pass-through) | [VirtualPositionConfirmer](#virtualpositionconfirmer--ortak-konfirmasyon-motoru) |
| 46 | `ConfirmingSingleTrader::VirtualSignals` | public property (computed) | [Dönüş/Sonuç](#dönüş--sonuç--global-state) — ham sinyal timeline'ı |
| 47 | `ConfirmingSingleTrader::Signals` | public property (computed) | [Dönüş/Sonuç](#dönüş--sonuç--global-state) — konfirme edilmiş sinyal timeline'ı |
| 50 | `ConfirmingSingleTrader::ConfirmingSingleTrader(...)` | constructor | [Kurulum](#kurulum) |
| 52 | `ConfirmingSingleTrader::SetStrategy(strategy)` | public method | [Kurulum](#kurulum) |
| 55 | `ConfirmingSingleTrader::Reset()` | public method | [Kurulum](#kurulum) |
| 56 | `ConfirmingSingleTrader::Init()` | public method | [Kurulum](#kurulum) |
| 59 | `ConfirmingSingleTrader::ResolveConfirmedSignal(i)` | private method | [Run() — Konfirmasyon Akışı](#run--konfirmasyon-akışı) |
| 60 | `ConfirmingSingleTrader::Run(i)` | public method | [Run() — Konfirmasyon Akışı](#run--konfirmasyon-akışı) |
| 63 | `ConfirmingSingleTrader::Finalize()` | public method | [Finalize()](#finalize) |
| 66 | `ConfirmingSingleTrader::GetMainTrader()` | public method | [mainTrader/signalTrader Erişimi](#maintradersignaltrader-erişimi-getmaintrader--getsignaltrader--setcallbacks--stop) |
| 67 | `ConfirmingSingleTrader::GetSignalTrader()` | public method | [mainTrader/signalTrader Erişimi](#maintradersignaltrader-erişimi-getmaintrader--getsignaltrader--setcallbacks--stop) |
| 68 | `ConfirmingSingleTrader::SetCallbacks(...)` | public method | [mainTrader/signalTrader Erişimi](#maintradersignaltrader-erişimi-getmaintrader--getsignaltrader--setcallbacks--stop) |
| 74 | `ConfirmingSingleTrader::Stop()` | public method | [mainTrader/signalTrader Erişimi](#maintradersignaltrader-erişimi-getmaintrader--getsignaltrader--setcallbacks--stop) |
| 77-88 | `WriteConfirmingSingleTraderListsToFiles`…`GetSinyal` (12 dosya-yazma/okuma yardımcısı) | method | [Lists Export](#lists-export-writeconfirmingsingletraderliststofiles) |
| 91 | `ConfirmingSingleTrader::Dispose()` | public method | [Kurulum](#kurulum) |

## Public API

### Kurulum

- `ConfirmingSingleTrader(id, data, indicators, logger)` — constructor: `_signalTrader = new
  SingleTrader(id, "signalTrader", data, indicators, logger) { RunMode = TradeOnly }`,
  `_mainTrader = new SingleTrader(-1, "mainTrader", ...) { RunMode = TradeOnly }` — mainTrader'a
  [MultipleTrader konvansiyonuyla](03-multipletrader.md#kimlik-ve-kurulum) tutarlı olarak `Id=-1`
  veriliyor. `IsInitialized = true`.
- `SetStrategy(strategy)` → sadece `_signalTrader.SetStrategy(strategy)` — mainTrader kendi
  stratejisini HİÇ çalıştırmaz, sadece konfirme edilmiş sinyalleri alır (`SetStrategy`
  `_mainTrader`'a hiç dokunmuyor).
- `Reset()` → `_signalTrader.Reset()` + `_mainTrader.Reset()` + `_confirmer.Reset()` + state
  flag'lerini sıfırlar.
- `Init()` → `_signalTrader.Init()` + `_mainTrader.Init()` + `_virtualYonHistory`/`_confirmedHistory`
  dizilerini `Data.Count` boyutunda yeniden ayırır (diagnostic geçmiş — bar-bar dosya çıktısı
  için).
- `Dispose()` → `_signalTrader?.Dispose()` + `_mainTrader?.Dispose()`, ikisini de `null`'a çeker.

### Run() — Konfirmasyon Akışı

```csharp linenums="1"
public void Run(int i)
{
    if (i >= Data.Count)
        return;

    _signalTrader.Run(i);   // ← GERÇEK SingleTrader.Run(), ham strateji sinyalini üretir (ama trade ETMEZ, RunMode=TradeOnly olsa da signalTrader'ın "trade"i sanaldır — bkz. Not)

    _mainTrader.ExecutePreOrderMethods(i);

    if (i < 1)
        return;

    TradeSignals signalForMainTrader = ResolveConfirmedSignal(i);   // ← VirtualPositionConfirmer.Resolve(...)

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

- `_signalTrader.Run(i)` — [MultipleTrader](03-multipletrader.md#run--çocuk-traderları--maintrader-pipeline)'ın
  child'larından FARKLI olarak, burada `signalTrader`'ın kendi `Run()`'ı GERÇEKTEN çağrılıyor
  (aynı MultipleTrader'daki gibi) — yani `signalTrader` de kendi defterinde "trade" yapıyor
  (kendi `SingleTrader.ExecuteOrders`'ı çalışıyor). Ama bu "trade" YALNIZCA `signalTrader`'ın
  KENDİ istatistikleri için anlamlı — `mainTrader`'a giden asıl karar `ResolveConfirmedSignal`
  tarafından ayrıca üretiliyor, `signalTrader`'ın pozisyonu/K-Z'i gerçek işlem akışına hiç
  girmiyor (`signalTrader.SaveStatisticsToFile` varsayılan `false`, bkz. [AppConfig
  Kaynağı](#appconfig-kaynağı--confirmingsingletraderconfig)).
- `mainTrader` da (MultipleTrader'ın mainTrader'ı gibi) kendi `SingleTrader.Run()`'ı ÇAĞRILMADAN,
  pipeline'ın 6 adımının stratejisiz kısmı elle tekrarlanıyor — `mainTrader.OnRun` event'i de bu
  yüzden ASLA tetiklenmiyor (bkz. [MultipleTrader'daki aynı
  bulgu](03-multipletrader.md#run--çocuk-traderları--maintrader-pipeline)).
- `ResolveConfirmedSignal(i)` (`181-188`) — `_signalTrader.SonYon`/`_signalTrader.strategySignal`/
  `Data[i].Close`'u `_confirmer.Resolve(...)`'a geçirir, asıl mantık orada (bkz.
  [VirtualPositionConfirmer](#virtualpositionconfirmer--ortak-konfirmasyon-motoru)).

> **Not — `OnProgress` her bar hesaplanan `percentage`'ı hiç kullanmıyor:** `Run(i)`'nin sonunda
> `double percentage = (i + 1) / (double)totalBars * 100.0;` hesaplanıyor ama `OnProgress?.Invoke(this,
> i + 1, totalBars)` çağrısına HİÇ geçirilmiyor (imza sadece `(ConfirmingSingleTrader, int, int)`
> — yüzdelik parametre almıyor). Kod içinde `_ = percentage;` satırı bile var (derleyicinin
> "unused variable" uyarısını bilerek bastırmak için) — hesaplama muhtemelen ileride event
> imzasına eklenmesi planlanan ama henüz yapılmamış bir alan.

### `VirtualPositionConfirmer` — Ortak Konfirmasyon Motoru

`ConfirmingSingleTrader` VE [ConfirmingMultipleTrader](04-confirmingmultipletrader.md) arasında
PAYLAŞILAN bir state machine — mantık ikisinde BİREBİR AYNI, sadece "ham sinyal kaynağı" farklı
(biri tek bir `SingleTrader`'ın stratejisi, diğeri bir `MultipleTrader`'ın consensus'u). Kod
tekrarını önlemek için ayrı dosyaya çıkarılmış.

```csharp linenums="1"
public enum SignalConflictMode
{
    CancelAndRestart = 0,   // Çakışan sinyal, bekleyen sanal pozisyonu iptal edip yeni yönde sıfırdan başlatır
    LockAndIgnore = 1       // Çakışan sinyal tamamen görmezden gelinir — sanal pozisyon orijinal yönünde bekler
}

public class VirtualPositionConfirmer
{
    public bool ThresholdIsPercentage { get; set; } = false;
    public double ProfitThreshold { get; set; } = 5000.0;
    public double LossThreshold { get; set; } = -3000.0;
    public ConfirmationTrigger Trigger { get; set; } = ConfirmationTrigger.Both;
    public SignalConflictMode ConflictMode { get; set; } = SignalConflictMode.CancelAndRestart;
    public bool FlattenImmediatelyOnFlatSignal { get; set; } = true;

    private string? _virtualYon;          // "A" / "S" / null (bekleyen sanal pozisyon yok)
    private double _virtualEntryPrice;
    private bool _confirmed;

    public string? VirtualYon => _virtualYon;
    public double VirtualEntryPrice => _virtualEntryPrice;
    public bool IsConfirmed => _confirmed;

    public void Reset();
    public TradeSignals Resolve(string currentYon, TradeSignals rawSignal, double currentPrice);
    private bool IsThresholdReached(string yon, double entryPrice, double currentPrice);
}
```

`Resolve(currentYon, rawSignal, currentPrice)` — her bar çağrılır, kaynağın o barki yönü
(`"A"`/`"S"`/`"F"`), ham komutu ve o barki fiyat verilir; mainTrader'a gönderilecek sinyali döner:

1. **Zaten konfirme** (`_confirmed == true`) — bu katman artık devrede değil, `rawSignal` OLDUĞU
   GİBİ geçer. `currentYon == "F"` olursa `_confirmed`/`_virtualYon` sıfırlanır (yeni bir
   konfirmasyon döngüsü başlayabilsin diye).
2. **Henüz konfirme değil, `currentYon == "F"`** — bekleyen sanal pozisyon varsa ve
   `FlattenImmediatelyOnFlatSignal == false` ise sanal pozisyon DEĞİŞMEDEN bekler (`None` döner);
   aksi halde (`true`, varsayılan) sanal pozisyon anında iptal edilir (`_virtualYon = null`).
3. **`currentYon` `A`/`S`, bekleyen sanal pozisyon yok** (`_virtualYon == null`) — yeni sanal
   pozisyon başlatılır (`_virtualYon = currentYon`, `_virtualEntryPrice = currentPrice`), `None`
   döner (henüz gerçek sinyal yok).
4. **Çakışan yön değişikliği** (`_virtualYon != currentYon`, ikisi de A/S) —
   `ConflictMode == CancelAndRestart` ise sanal pozisyon YENİ yönde sıfırdan başlar (giriş
   fiyatı da güncellenir); `LockAndIgnore` ise HİÇBİR ŞEY değişmez, sanal pozisyon orijinal
   yönünde beklemeye devam eder. İkisinde de `None` döner.
5. **Aynı yönde bekliyor — eşik kontrolü** (`IsThresholdReached(...)`) — eşik geçildiyse
   `_confirmed = true` ve **`rawSignal` DEĞİL**, `_virtualYon`'a göre YENİDEN üretilmiş
   `Buy`/`Sell` döner (kod içi yorum: "sanal pozisyon genelde birkaç bar önce açıldığı için
   kaynağın konfirme anındaki ham komutu genelde None/tekrar eden bir değer oluyor ... Gerçek
   veride bulunmuş bir hatanın düzeltmesi").

`IsThresholdReached(yon, entryPrice, currentPrice)` — `yon=="A"` ise `currentPrice - entryPrice`,
`"S"` ise `entryPrice - currentPrice` (K/Z fiyatı); `ThresholdIsPercentage` açıksa yüzdeye çevrilir
(`entryPrice==0` ise `0.0`); `Trigger` (`ProfitOnly`/`LossOnly`/`Both`) hangi eşiğin/eşiklerin
kontrol edileceğini belirler.

### Finalize()

```csharp linenums="1"
public void Finalize()
{
    if (!IsInitialized)
        throw new InvalidOperationException("ConfirmingSingleTrader not initialized");

    _signalTrader.Finalize();

    _mainTrader.CalculateStatistics();
    _mainTrader.GetPerformansParams(out double bakiyePuan, out double lotSayisi, out double varlikAdedCarpani);
    _mainTrader.CalculatePerformances(bakiyePuan, lotSayisi, varlikAdedCarpani);

    if (SaveStatisticsToFile)
        WriteConfirmingSingleTraderListsToFiles();
}
```

- `_signalTrader.Finalize()` GERÇEKTEN çağrılıyor — [SingleTrader'ın kendi
  `Finalize()`](02-singletrader.md#yaşam-döngüsü) zinciri (`CalculateStatistics`/
  `CalculatePerformances`) `signalTrader` için de çalışır, `signalTrader.SaveStatisticsToFile`
  (varsayılan `false`) `true` ise kendi tam istatistik dosyalarını da üretir.
- `_mainTrader` için `Finalize()` DEĞİL, `CalculateStatistics()`/`GetPerformansParams()`/
  `CalculatePerformances()` DOĞRUDAN çağrılıyor — `SingleTrader.Finalize()`'ın kendisi hiç
  çağrılmıyor (`OnFinal` event'i bu yüzden `mainTrader` için de ASLA tetiklenmiyor, `Run()`'daki
  `OnRun` bulgusuyla aynı desen).
- `#pragma warning disable/restore CS0465` — [SingleTrader'daki ile aynı
  sebep](02-singletrader.md#yaşam-döngüsü) (metod adı `Finalize`, CLR finalizer isim çakışması).

### mainTrader/signalTrader Erişimi: `GetMainTrader()` / `GetSignalTrader()` / `SetCallbacks()` / `Stop()`

- `GetMainTrader()` → `_mainTrader`, `GetSignalTrader()` → `_signalTrader` — Console/script'in bu
  iki iç trader'a erişim yolu.
- `SetCallbacks(...)` — hem `_mainTrader` hem `_signalTrader`'a AYNI callback setini bağlar
  (`_mainTrader.SetCallbacks(...)` + `_signalTrader.SetCallbacks(...)`). Console akışı
  (`RunConfirmingSingleTraderWithProgressAsync()`) bu toplu metodu KULLANMIYOR — bkz. aşağıdaki
  [Tam Kaynak](#runconfirmingsingletraderwithprogressasync--tam-kaynak-algotradercs2160-2394)
  bölümündeki Not (`signalTrader`'a hiç callback bağlanmıyor).
- `Stop()` — `IsRunning` ise `IsStopRequested = true` + log. `_signalTrader`/`_mainTrader`'ın
  kendi `IsStopRequested`'ını ayrıca set etmiyor (bar döngüsü zaten `confirmingSingleTrader.IsStopRequested`'a
  bakıyor, [MultipleTrader'daki `Stop()` ile aynı davranış](03-multipletrader.md#maintrader-yardımcıları-getmaintrader--setcallbacks--stop)).

### Lists Export: `WriteConfirmingSingleTraderListsToFiles(...)`

- `WriteConfirmingSingleTraderListsToFiles()` — `SaveConfirmingSingleTraderListsTxtEnabled`/`CsvEnabled`
  bayraklarına göre `WriteConfirmingSingleTraderListsToTxt`/`ToCsv`'yi çağırır. **Bar-bar** rapor
  — `signalTrader`/sanal pozisyon/`mainTrader` kolonlarını yan yana yazar, **performans raporu
  DEĞİL** ([MultipleTrader'ın kendi bar-bar
  raporuyla](03-multipletrader.md#bar-bar-liste-çıktısı-writemultipletraderliststofiles) aynı
  felsefe).
- TXT formatı sabit-genişlik kolonlu (`BarNo|Date|Time|Close|SigYon|SigSvy|SigSny|VirYon|Confrm|MainYon|MainSvy|MainSny`),
  CSV `;`-ayraçlı aynı kolonlarla (`SignalTrader_Yon` vb. tam isimlerle). `GetVirtualYon`/
  `GetConfirmed` — `_virtualYonHistory`/`_confirmedHistory`'den (`Init()`'te ayrılan diagnostic
  diziler) okur; `GetYon`/`GetSeviye`/`GetSinyal` — [MultipleTrader'ın aynı isimli
  yardımcılarıyla](03-multipletrader.md#bar-bar-liste-çıktısı-writemultipletraderliststofiles)
  birebir aynı (`trader.lists.YonList`/`SeviyeList`/`SinyalList`'ten okur, sınır/null kontrolü).
- Dosyanın hangi KLASÖRE yazıldığı konusunda önemli bir bulgu var — bkz. [Dönüş/Sonuç § Global
  State](#dönüş--sonuç--global-state).

## Çağrı Zinciri — Menüden Çağrılma (Program.cs → AlgoTrader → ConfirmingSingleTrader)

1. `handleConfirmingSingleTrader()` (`Program.cs:3115-`) — [SingleTrader'daki
   `handleSingleTrader()`](02-singletrader.md#çağrı-zinciri--menüden-çağrılma-programcs--algotrader--singletrader)
   ile birebir aynı desen: `reloadAppConfig()` → `showModeConfigSummary(...)` →
   `[ENTER]/[E]/[R]/[B]` → `showConfirmingSingleTraderRunPreview()` → `[ENTER]/[E]/[R]/[B]` →
   `runConfirmingSingleTraderAlgoTrade()`.
2. `runConfirmingSingleTraderAlgoTrade()` (`Program.cs:894-953`) — `stockDataReader`/`IsDataReady`
   kontrolü (başarısızsa özel bir hata mesajı: "run [1] Read Data first...") → `new AlgoTrader(...)`
   + logger/timer + `SetData(...)` + `SymbolName`/`SymbolPeriod` → **`AppConfigApplier.ApplyConfirmingSingleTrader(algoTrader,
   appConfig.ConfirmingSingleTrader, AppSettings.ConfigsDir)`** (bkz. [AppConfig
   Kaynağı](#appconfig-kaynağı--confirmingsingletraderconfig)) → `Initialize()` → **`await
   algoTrader.RunConfirmingSingleTraderWithProgressAsync()`** → `WriteTraderDataToFilesAsync(algoTrader.ConfirmingSingleTrader!)`
   → `mainTrader.PlotEnabled` ise `SetupPython()` + `PlotSingleTraderData(mainTrader)` (bkz.
   [PythonPlotter](python-plotter.md) — burada özel bir log notu var: *"VirtualSignals overlay
   henüz yok"*, yani sadece mainTrader'ın konfirme edilmiş sinyalleri çiziliyor, `VirtualSignals`
   (ham sinyal timeline'ı) plot'a hiç eklenmiyor).
3. `AlgoTrader.RunConfirmingSingleTraderWithProgressAsync()` (`AlgoTrader.cs:2160-2394`) — gerçek
   `ConfirmingSingleTrader` burada yaratılıp konfigüre ediliyor, `SingleTrader`/`MultipleTrader`'ın
   orkestrasyon fonksiyonlarıyla AYNI iskelet (indicators → strategy → trader → callbacks →
   attributes → TradeParams → flags → Init → bar döngüsü → tarama özeti → Finalize).

Notable fark: `signalTrader` VE `mainTrader` **AYNI** `_singleTraderTradeParamsConfig`'i paylaşır
(`AlgoTrader.cs:2281-2285` — "MultipleTrader'daki 'TradeParams MainTrader'dan alınır'
konvansiyonuyla tutarlı" kod içi yorum), ama `Signals`/`Save` config'leri AYRI slotlardan gelir:
`signalTrader` → `_confirmingSignalTraderSignalsConfig`/`_confirmingSignalTraderSaveConfig`
(`ApplyConfirmingSignalTraderFlagsConfigs`), `mainTrader` → SingleTrader/MultipleTrader ile
PAYLAŞILAN `_singleTraderSignalsConfig`/`_singleTraderSaveConfig` (`ApplySingleTraderFlagsConfigs`).

## AppConfig Kaynağı — `ConfirmingSingleTraderConfig`

```csharp linenums="1"
public class ConfirmingSingleTraderConfig
{
    public string RunMode { get; set; } = "TradeOnly";   // Şimdilik sadece TradeOnly desteklenir (Query kavramı yok)
    public ConfirmingSingleTraderSaveConfig Save         { get; set; } = new();
    public SignalTraderConfig               SignalTrader { get; set; } = new();
    public ConfirmationConfig               Confirmation { get; set; } = new();
    public ConfirmingMainTraderConfig       MainTrader   { get; set; } = new();
}

public class ConfirmingSingleTraderSaveConfig
{
    public bool   SaveStatisticsToFile                      { get; set; } = true;
    public bool   SaveConfirmingSingleTraderListsTxtEnabled { get; set; } = true;
    public bool   SaveConfirmingSingleTraderListsCsvEnabled { get; set; } = true;
    public string ConfirmingSingleTraderListsTxtFileName    { get; set; } = "ConfirmingSingleTraderLists.txt";
    public string ConfirmingSingleTraderListsCsvFileName    { get; set; } = "ConfirmingSingleTraderLists.csv";
    // FilePrefix: SignalTrader → {FilePrefix}_Signal_{FileName}  |  MainTrader → {FilePrefix}_Main_{FileName}
    public string FilePrefix { get; set; } = "ConfirmingSingleTrader";
}

/// <summary>Sinyal-kaynağı trader (ham Al/Sat/Flat sinyalini üreten strateji) konfigürasyonu.</summary>
public class SignalTraderConfig
{
    public StrategyRef              Strategy { get; set; } = new();
    public TraderSignalsConfig      Signals  { get; set; } = new();
    public TraderPlotConfig         Plot     { get; set; } = new();
    public TraderSaveConfig         Save     { get; set; } = new();
    public TraderExportConfig?      Export   { get; set; }
}

public class ConfirmationConfig   // Trigger: ProfitOnly|LossOnly|Both. ConflictMode: CancelAndRestart|LockAndIgnore.
{
    public bool   ThresholdIsPercentage         { get; set; } = false;
    public double ProfitThreshold               { get; set; } = 5000.0;
    public double LossThreshold                 { get; set; } = -3000.0;
    public string Trigger                       { get; set; } = "Both";
    public string ConflictMode                  { get; set; } = "CancelAndRestart";
    public bool   FlattenImmediatelyOnFlatSignal { get; set; } = true;
}

/// <summary>mainTrader — konfirme edilmiş sinyal üzerinde gerçek işlem yapar. Strategy/Query yok.</summary>
public class ConfirmingMainTraderConfig
{
    public EcfRef?                  EquityCurveFilter { get; set; }
    public TradeParamsConfig        TradeParams       { get; set; } = new();
    public TraderSignalsConfig      Signals           { get; set; } = new();
    public TraderPlotConfig         Plot              { get; set; } = new();
    public TraderSaveConfig         Save              { get; set; } = new();
    public TraderExportConfig?      Export            { get; set; }
}
```

`AppConfig.json`'daki gerçek karşılığı (`inputs/configs/AppConfig/AppConfig.json:351-`,
kısaltılmış — `TradeParams`/`Signals`/`Save` alt-nesneleri [SingleTrader § AppConfig
Kaynağı](02-singletrader.md#appconfig-kaynağı--singletraderconfig)'nda birebir aynı şema):

```json linenums="1"
"ConfirmingSingleTrader": {
    "RunMode": "TradeOnly",
    "Save": {
      "SaveStatisticsToFile": true,
      "SaveConfirmingSingleTraderListsTxtEnabled": true,
      "SaveConfirmingSingleTraderListsCsvEnabled": true,
      "ConfirmingSingleTraderListsTxtFileName": "ConfirmingSingleTraderLists.txt",
      "ConfirmingSingleTraderListsCsvFileName": "ConfirmingSingleTraderLists.csv",
      "FilePrefix": "ConfirmingSingleTrader"
    },
    "SignalTrader": {
      "Strategy": { "ConfigFile": "StrategyConfig.txt", "Name": "SimpleMostStrategy", "Version": "v1" },
      "Signals": { "AlEnabled": true, "SatEnabled": true, "...": "... (12 alan, SingleTrader ile aynı şema)" },
      "Plot": { "PlotEnabled": false },
      "Save": { "SaveStatisticsToFile": false, "...": "... (varsayılan KAPALI — bar-by-bar Yon/Seviye/Sinyal zaten ConfirmingSingleTraderLists içinde)" }
    },
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
      "TradeParams": {
        "MarketType": "FxCrypto", "IlkBakiye": 100000.0, "KontratSayisi": 1,
        "LotSayisi": 0.01, "HisseSayisi": 1000.0, "KomisyonCarpan": 0.0,
        "KaymaMiktari": 0.0, "PyramidingEnabled": false
      },
      "Signals": { "...": "... (SingleTrader ile aynı şema)" },
      "Save": { "...": "... (12 flag + 12 dosya adı)" }
    }
}
```

- `SignalTrader.Save.SaveStatisticsToFile` varsayılan **`false`** — bilinçli seçim, JSON'daki
  yorum satırı gerekçeyi açıklıyor: "bar-by-bar Yon/Seviye/Sinyal zaten
  `ConfirmingSingleTraderLists` içinde" (tekrar tam istatistik dosyası üretmenin gereksiz olduğu
  düşünülmüş).
- `Confirmation.LossThreshold` NEGATİF girilmeli (varsayılan `-3000.0`) — `IsThresholdReached`'ın
  `karZarar <= LossThreshold` kontrolü bunu varsayıyor, pozitif bir değer girilirse zarar eşiği
  fiilen hiç tetiklenmez (`karZarar` her zaman pozitif bir eşikten büyük/küçük olamayacağı bir
  aralıkta kalır — kanıtlı bir "kesin" bug değil ama config'i yanlış dolduran biri için sessiz
  bir yanlış yapılandırma tuzağı).

## `RunConfirmingSingleTraderWithProgressAsync()` — Tam Kaynak (`AlgoTrader.cs:2160-2394`)

Yapısı [SingleTrader'ın orkestrasyon fonksiyonuyla](02-singletrader.md#runsingletraderwithprogressasync--tam-kaynak-algotradercs1252-1530)
neredeyse birebir aynı (indicators → strategy → trader → callbacks → attributes → TradeParams →
flags → Init → bar döngüsü → tarama özeti → Finalize) — burada sadece FARKLI/ÖZEL kısımlar
gösteriliyor, ortak iskelet için oraya bakın:

```csharp linenums="1" hl_lines="6 21 24 33"
strategy = _strategyRegistry.CreateStrategy(this.Data, indicators, _logger, _currentStrategyName, _currentStrategyParams);   // ← signalTrader'ın stratejisi

confirmingSingleTrader = new ConfirmingSingleTrader(0, this.Data, indicators, _logger);
confirmingSingleTrader.Reset();

// ConfirmingSingleTrader nesnesi kayıt ayarları (AppConfig.ConfirmingSingleTrader.Save)
if (_confirmingSingleTraderSaveConfig is { } css)
{
    confirmingSingleTrader.SaveStatisticsToFile                      = css.SaveStatisticsToFile;
    confirmingSingleTrader.SaveConfirmingSingleTraderListsTxtEnabled = css.SaveConfirmingSingleTraderListsTxtEnabled;
    confirmingSingleTrader.SaveConfirmingSingleTraderListsCsvEnabled = css.SaveConfirmingSingleTraderListsCsvEnabled;
    confirmingSingleTrader.ConfirmingSingleTraderListsTxtFileName    = css.ConfirmingSingleTraderListsTxtFileName;
    confirmingSingleTrader.ConfirmingSingleTraderListsCsvFileName    = css.ConfirmingSingleTraderListsCsvFileName;
}
else
{
    confirmingSingleTrader.SaveStatisticsToFile = true;
}

// Sanal pozisyon konfirmasyon ayarları (AppConfig.ConfirmingSingleTrader.Confirmation)
if (_confirmingSingleTraderConfirmationConfig is { } cc)
{
    confirmingSingleTrader.ThresholdIsPercentage = cc.ThresholdIsPercentage;
    confirmingSingleTrader.ProfitThreshold       = cc.ProfitThreshold;
    confirmingSingleTrader.LossThreshold         = cc.LossThreshold;
    confirmingSingleTrader.Trigger      = Enum.TryParse<ConfirmationTrigger>(cc.Trigger, ignoreCase: true, out var trig)
        ? trig : ConfirmationTrigger.Both;
    confirmingSingleTrader.ConflictMode = Enum.TryParse<SignalConflictMode>(cc.ConflictMode, ignoreCase: true, out var cm)
        ? cm : SignalConflictMode.CancelAndRestart;
    confirmingSingleTrader.FlattenImmediatelyOnFlatSignal = cc.FlattenImmediatelyOnFlatSignal;
}

var signalTrader = confirmingSingleTrader.GetSignalTrader();
var mainTrader   = confirmingSingleTrader.GetMainTrader();

// mainTrader callback — SingleTrader/MultipleTrader'ın mainTrader'ıyla AYNI callback seti
mainTrader.ClearCallbacks()
          .SetCallbacks(OnSingleTraderReset, OnSingleTraderInit, OnSingleTraderRun, OnSingleTraderFinal,
                        OnSingleTraderBeforeOrder, OnSingleTraderNotifySignal, OnSingleTraderAfterOrder, OnSingleTraderProgress);
// ⚠️ Dikkat: signalTrader'a callback BAĞLANMIYOR — signalTrader kendi haline bırakılıyor

// TradeParams — signalTrader VE mainTrader AYNI parametreleri kullanır
if (_singleTraderTradeParamsConfig != null)
{
    signalTrader.initialTradeParams!.ApplyFrom(_singleTraderTradeParamsConfig);
    mainTrader.initialTradeParams!.ApplyFrom(_singleTraderTradeParamsConfig);
}

// SignalTrader flags — KENDİ ayrı slotlarından
ApplyConfirmingSignalTraderFlagsConfigs(signalTrader);
signalTrader.RunMode = TraderRunMode.TradeOnly;
signalTrader.SetStrategy(strategy);

// MainTrader flags — SingleTrader/MultipleTrader ile PAYLAŞILAN slotlardan
ApplySingleTraderFlagsConfigs(mainTrader);
SetSingleTraderConfigureEquityCurveFilter(mainTrader);
mainTrader.RunMode = TraderRunMode.TradeOnly;

confirmingSingleTrader.Init();
```

> **Not — `signalTrader`'a hiçbir callback bağlanmıyor:** `mainTrader.ClearCallbacks().SetCallbacks(...)`
> çağrılıyor ama `signalTrader.ClearCallbacks().SetCallbacks(...)` YOK — `SingleTrader/MultipleTrader`
> desenlerinin aksine (ikisinde de hem mainTrader hem child'lara aynı callback seti bağlanıyordu),
> burada `signalTrader`'ın `OnRun`/`OnBeforeOrder`/vb. event'leri tamamen boş kalıyor (constructor'da
> hiç set edilmedikleri için `null`). Pratik etkisi muhtemelen sıfır (bağlı olsalardı da hepsi
> boş gövdeli olurdu, bkz. [SingleTrader § Callback'lerin Gerçek
> Gövdeleri](02-singletrader.md#callbacklerin-gerçek-gövdeleri-algotradercs158-223)), ama tutarsız
> bir desen — `ConfirmingSingleTrader::SetCallbacks(...)` (sınıfın kendi metodu, iskelet satır 68)
> HER İKİSİNE de bağlardı, ama Console akışı o metodu hiç çağırmıyor, elle sadece mainTrader'a
> bağlıyor.

## Callback'lerin Gerçek Gövdeleri

`mainTrader`'a bağlanan callback seti [SingleTrader/MultipleTrader ile TAMAMEN AYNI](02-singletrader.md#callbacklerin-gerçek-gövdeleri-algotradercs158-223)
(`OnSingleTraderReset`/`Init`/`Run`/`Final`/`BeforeOrder`/`NotifySignal`/`AfterOrder` boş,
`OnSingleTraderProgress` dolu) — ayrı bir `OnConfirmingSingleTraderXxx` callback ailesi YOK.
`signalTrader`'a ise (yukarıdaki Not'ta açıklandığı gibi) hiçbir callback bağlanmıyor.

## Dönüş / Sonuç — Global State

| Değişken/Erişim | Tip | Kaynak |
|---|---|---|
| `algoTrader.ConfirmingSingleTrader` | `ConfirmingSingleTrader` (public getter, `private set`) | `RunConfirmingSingleTraderWithProgressAsync()` içinde yaratılan `confirmingSingleTrader` |
| `.GetMainTrader()`/`.GetSignalTrader()` | `SingleTrader` | mainTrader (gerçek trade) / signalTrader (ham sinyal) |
| `.Signals`/`.VirtualSignals` | `List<double>` | Konfirme edilmiş / ham sinyal timeline'ı — plot overlay için tasarlanmış ama henüz kullanılmıyor (bkz. yukarıdaki [Çağrı Zinciri](#çağrı-zinciri--menüden-çağrılma-programcs--algotrader--confirmingsingletrader) notu) |
| `{FilePrefix}_Main_*`, `{FilePrefix}_Signal_*` (12+12 dosya) | dosya | `WriteTraderDataToFilesAsync(ConfirmingSingleTrader)` → `mainTrader`/`signalTrader.WriteStatisticsToFile(AppSettings.LogsDir, ...)` — `AppSettings.LogsDir` (`outputs/logs/`) kullanır |
| `ConfirmingSingleTraderLists.txt`/`.csv` | dosya | `confirmingSingleTrader.Finalize()` içinde (`SaveStatisticsToFile` ise) — bkz. AŞAĞIDAKİ KRİTİK NOT, FARKLI bir klasöre yazılıyor |

> **Not — `ConfirmingSingleTraderLists.txt`/`.csv`, diğer TÜM çıktı dosyalarından FARKLI bir
> klasöre yazılıyor:** `WriteConfirmingSingleTraderListsToTxt()`/`ToCsv()`
> (`ConfirmingSingleTrader.cs:301`, `362`) klasörü şöyle hesaplıyor:
> ```csharp linenums="1"
> var logDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
> ```
> `AppDomain.CurrentDomain.BaseDirectory` derlenmiş exe'nin bulunduğu klasördür (örn.
> `AlgoTrade.Console/bin/Debug/net8.0/`) — proje genelinde HER YERDE kullanılan `AppSettings.LogsDir`
> (`Path.Combine(AppSettings.RootDir, "outputs", "logs")`, `RootDir` `AppContext.BaseDirectory`'den
> **4 seviye yukarı** çıkarak proje kökünü bulur) İLE AYNI DEĞİL. Sonuç: `mainTrader`/`signalTrader`'ın
> tam istatistik dosyaları (`WriteTraderDataToFilesAsync` üzerinden) `outputs/logs/`'a giderken,
> AYNI koşumun `ConfirmingSingleTraderLists.txt`/`.csv`'si (bar-bar Yon/Seviye/Sinyal + sanal
> pozisyon karşılaştırması — bu sınıfın ürettiği EN ÖZGÜN çıktı) sessizce `{exe_klasörü}/logs/`'a
> yazılıyor. Kullanıcı `outputs/logs/`'a bakıp bu dosyayı bulamayabilir — kanıtlı, yüksek
> güvenilirlikli bir bulgu (kaynağın kendisi bunu açıkça gösteriyor, `AppSettings.LogsDir`'in
> proje genelindeki her diğer kullanımıyla doğrudan karşılaştırılabilir).

## Tipik Kullanım — Script'ten Çağrılma

- Gerçek örnek: `inputs/scripts/06_RunConfirmingSingleTraderWithProgressAsync.csx`.
- `algoTrader.GetStrategy(0)` kullanıyor — yani `algoTrader.AddStrategyConfig(0, ...)`'un daha
  önce (script'in `#load` ettiği bir config dosyasında) çağrılmış olmasını bekliyor; script
  `new ConfirmingSingleTrader(...)`'ı MANUEL kuruyor ama stratejiyi yine `AlgoTrader`'ın
  registry'sinden (factory olarak) alıyor — [SingleTrader/MultipleTrader'daki Seviye
  B](02-singletrader.md#tipik-kullanım--scriptten-çağrılma-manuel-kurulum) ile aynı karma desen.

**1) Kurulum + Confirmation ayarları**

```csharp linenums="1"
var confirmingSingleTrader = new ConfirmingSingleTrader(0, data, indicators, null);
confirmingSingleTrader.Reset();

confirmingSingleTrader.ThresholdIsPercentage = thresholdIsPercentage;
confirmingSingleTrader.ProfitThreshold = profitThreshold;
confirmingSingleTrader.LossThreshold = lossThreshold;
confirmingSingleTrader.Trigger = confirmationTrigger;
confirmingSingleTrader.ConflictMode = conflictMode;
confirmingSingleTrader.FlattenImmediatelyOnFlatSignal = flattenImmediatelyOnFlatSignal;
```

**2) mainTrader + signalTrader — aynı TradeParams, farklı sinyal bayrakları**

```csharp linenums="1"
var mainTrader = confirmingSingleTrader.GetMainTrader();
var signalTrader = confirmingSingleTrader.GetSignalTrader();

mainTrader.initialTradeParams!.Reset()
    .SetBakiyeParams(ilkBakiye: ilkBakiye)
    .SetKontratParamsFxCrypto(lotSayisi: lotSayisi)
    .SetKomisyonParams(komisyonCarpan: komisyonCarpan)
    .SetKaymaParams(kaymaMiktari: kaymaMiktari);
signalTrader.initialTradeParams!.Reset()
    .SetBakiyeParams(ilkBakiye: ilkBakiye)
    .SetKontratParamsFxCrypto(lotSayisi: lotSayisi)
    .SetKomisyonParams(komisyonCarpan: komisyonCarpan)
    .SetKaymaParams(kaymaMiktari: kaymaMiktari);

mainTrader.RunMode = TraderRunMode.TradeOnly;
mainTrader.ConfigureUserFlagsOnce();
mainTrader.signals.AlEnabled = true;
mainTrader.signals.SatEnabled = true;
mainTrader.signals.FlatOlEnabled = true;
algoTrader.SetSingleTraderConfigureEquityCurveFilter(mainTrader);
mainTrader.SaveStatisticsToFile = saveMainTraderStatistics;

signalTrader.RunMode = TraderRunMode.TradeOnly;
signalTrader.ConfigureUserFlagsOnce();
signalTrader.signals.AlEnabled = true;
signalTrader.signals.SatEnabled = true;
signalTrader.signals.FlatOlEnabled = true;
signalTrader.SaveStatisticsToFile = false;
```

**3) Strateji ata + Init**

```csharp linenums="1"
var strategy = algoTrader.GetStrategy(0);
confirmingSingleTrader.SetStrategy(strategy);

confirmingSingleTrader.SaveStatisticsToFile = saveConfirmingSingleTraderLists;
confirmingSingleTrader.Init();
```

**4) Bar-bar çalıştır + Finalize**

```csharp linenums="1"
confirmingSingleTrader.IsStarted = true;
confirmingSingleTrader.IsRunning = true;

for (int i = 0; i < totalBars; i++)
    confirmingSingleTrader.Run(i);

confirmingSingleTrader.Finalize();
```

## Console/JSON Eşleşmesi

1. `inputs/configs/AppConfig/AppConfig.json` dosyasını aç.
2. `"ConfirmingSingleTrader"` bölümünü düzenle (bkz. yukarıdaki [AppConfig
   Kaynağı](#appconfig-kaynağı--confirmingsingletraderconfig) tam örnek): `SignalTrader.Strategy`
   ile ham sinyal kaynağı stratejiyi, `Confirmation` ile eşik/mod ayarlarını, `MainTrader.TradeParams`
   ile pozisyon büyüklüğünü seç.
3. Kaydet, Console'u çalıştır, menüden `[22] ConfirmingSingleTrader` (veya `[23]` "Read Data +
   ConfirmingSingleTrader") seç.

## Kimler Kullanıyor — Instantiation Noktaları

`new ConfirmingSingleTrader(...)` için tüm kod tabanında grep taraması — **2 çağırım noktası**:

| Dosya | Bağlam | Satır |
|---|---|---|
| `AlgoTrade.Core/Trading/AlgoTrader.cs` | `RunConfirmingSingleTraderWithProgressAsync()` — `confirmingSingleTrader` (id=0) | 2219 |
| `inputs/scripts/06_RunConfirmingSingleTraderWithProgressAsync.csx` | top-level akış — `confirmingSingleTrader` | 91 |

## Kullanım Haritası

| Üye | Durum | Nerede |
|---|---|---|
| Constructor, `SetStrategy`, `Reset`, `Init`, `Run`, `Finalize`, `GetMainTrader`, `GetSignalTrader`, `Dispose` | ✅ | `RunConfirmingSingleTraderWithProgressAsync()` (yukarıda tam kaynağıyla var) |
| `ThresholdIsPercentage`/`ProfitThreshold`/`LossThreshold`/`Trigger`/`ConflictMode`/`FlattenImmediatelyOnFlatSignal` | ✅ | `AppConfig.ConfirmingSingleTrader.Confirmation` üzerinden |
| `SaveStatisticsToFile`, 4 dosya adı/bayrak property'si | ✅ | `AppConfig.ConfirmingSingleTrader.Save` üzerinden |
| `VirtualYon`/`VirtualEntryPrice`/`IsConfirmed` | ✅ (dolaylı) | `Run(i)`'nin `_virtualYonHistory`/`_confirmedHistory`'yi doldurmasında, bar-bar dosya çıktısında |
| `VirtualSignals`/`Signals` | ⚠️ | Tanımlı (plot overlay için), ama `runConfirmingSingleTraderAlgoTrade()`'in plot çağrısı sadece `Signals`'ı (mainTrader'ın kendi `PlotSingleTraderData`'sı üzerinden dolaylı) kullanıyor — `VirtualSignals` hiçbir yerde OKUNMUYOR |
| `SetCallbacks(...)` (sınıfın kendi toplu metodu) | ❌ (Console akışında) | Console `signalTrader`'a callback bağlamıyor, mainTrader'a `SingleTrader.SetCallbacks` doğrudan — bkz. [Not](#runconfirmingsingletraderwithprogressasync--tam-kaynak-algotradercs2160-2394) |
| `Stop()` | ❌ | Hiçbir yerden çağrılmıyor |
| `OnProgress` (delegate) | ✅ (ama `percentage` parametresiz) | `Run(i)`'de tetikleniyor ama hesaplanan yüzde hiç geçirilmiyor — bkz. [Not](#run--konfirmasyon-akışı) |

## İlgili Dosyalar

- [01-class-reference.md § 4. ConfirmingSingleTrader / ConfirmingMultipleTrader /
  VirtualPositionConfirmer](../01-class-reference.md#8-virtualpositionconfirmer--ortak-konfirmasyon-motoru) —
  bu sayfanın ait olduğu index.
- [04-confirmingmultipletrader.md](04-confirmingmultipletrader.md) — aynı `VirtualPositionConfirmer`
  motorunu bir `MultipleTrader` consensus'u üzerinde kullanan kardeş sayfa.
- [02-singletrader.md](02-singletrader.md) — `signalTrader`/`mainTrader`'ın gerçek tipi.
- [03-multipletrader.md](03-multipletrader.md) — benzer "sinyal-kaynağı + ayrı mainTrader" yapısı,
  consensus yerine burada eşik konfirmasyonu var.
- [python-plotter.md](python-plotter.md) — `mainTrader.PlotEnabled` ile tetiklenen plot akışı.
- [06-class-doc-method.md](../06-class-doc-method.md) — bu sayfanın yazıldığı yöntem.
- `docs/todo.md` — "Getiri Eğrisi / KarZarar Eğrisi Konfirmasyonu (Madde 3)" tasarım tartışması.
