using AlgoTrade.Core;
using AlgoTrade.Core.DataProvider;
using AlgoTrade.Core.Logging;
using AlgoTrade.Core.Trading.Core;
using AlgoTrade.Core.Trading.Indicators;
using MathNet.Numerics.Statistics;

namespace AlgoTrade.Core.Trading;

public class SingleTrader : MarketDataProvider, IDisposable
{
    #region Properties

    // Identification
    public int Id { get; private set; }
    public void SetId(int id) => Id = id;
    public int GetId() => Id;

    public string Name { get; private set; }
    public void SetName(string name) => Name = name;
    public string GetName() => Name;

    public void SetData(List<StockData> data)
    {
        _data = data;
    }

    // Symbol and System Id
    public string SymbolName { get; set; }
    public string SymbolPeriod { get; set; }
    public string SystemId { get; set; }
    public string SystemName { get; set; }
    public string StrategyId { get; set; }
    public string StrategyName { get; set; }

    // Execution Time Tracking
    public string LastExecutionId { get; set; }
    public string LastExecutionTime { get; set; }
    public string LastExecutionTimeStart { get; set; }
    public string LastExecutionTimeStop { get; set; }
    public string LastExecutionTimeInMSec { get; set; }
    public string LastResetTime { get; set; }
    public string LastStatisticsCalculationTime { get; set; }

    // Logger
    private LogManager? _logger;
    public void SetLogger(LogManager? logger)
    {
        _logger = logger;
    }

    private IndicatorManager? _indicators;
    public void SetIndicators(IndicatorManager? indicators)
    {
        _indicators = indicators;
    }

    public InitialTradeParams initialTradeParams { get; private set; }
    public Signals signals { get; private set; }
    public Status status { get; private set; }
    public Flags flags { get; private set; }
    public Lists lists { get; private set; }

    public TradeSignals strategySignal { get; set; }

    #endregion

    public event Action<SingleTrader, int>? OnReset;
    public event Action<SingleTrader, int>? OnInit;
    public event Action<SingleTrader, int>? OnRun;
    public event Action<SingleTrader, int>? OnFinal;
    public event Action<SingleTrader, int>? OnBeforeOrder;
    public event Action<SingleTrader, string, int>? OnNotifySignal;
    public event Action<SingleTrader, int>? OnAfterOrder;
    public event Action<SingleTrader, int, int, double>? OnProgress;
    public event Action<SingleTrader>? OnApplyUserFlags;    

    public SingleTrader(int id, string name, List<StockData> data, IndicatorManager indicators, LogManager? logger = null)
    {
        SetId(id);
        SetName(name);
        SetData(data);
        SetIndicators(indicators);

        _logger = null;
        if (logger is not null)
            SetLogger(logger);

        CreateModules();
    }
    public SingleTrader SetCallbacks(
        Action<SingleTrader, int>? onReset = null,
        Action<SingleTrader, int>? onInit = null,
        Action<SingleTrader, int>? onRun = null,
        Action<SingleTrader, int>? onFinal = null,
        Action<SingleTrader, int>? onBeforeOrders = null,
        Action<SingleTrader, string, int>? onNotifySignal = null,
        Action<SingleTrader, int>? onAfterOrders = null,
        Action<SingleTrader, int, int, double>? onProgress = null,
        Action<SingleTrader>? onApplyUserFlags = null)
    {
        if (onReset != null) OnReset = onReset;
        if (onInit != null) OnInit = onInit;
        if (onRun != null) OnRun = onRun;
        if (onFinal != null) OnFinal = onFinal;
        if (onBeforeOrders != null) OnBeforeOrder = onBeforeOrders;
        if (onAfterOrders != null) OnAfterOrder = onAfterOrders;
        if (onNotifySignal != null) OnNotifySignal = onNotifySignal;
        if (onProgress != null) OnProgress = onProgress;
        if (onApplyUserFlags != null) OnApplyUserFlags = onApplyUserFlags;

        return this;
    }

    public void Reset()
    {
        OnReset?.Invoke(this, 0);

        // Reset internal modules (state only)
        ResetModules();

        OnReset?.Invoke(this, 1);
    }

    public void Init()
    {
        OnInit?.Invoke(this, 0);

        InitModules();

        OnInit?.Invoke(this, 1);
    }

    public void Run(int barIndex)
    {
        int i = barIndex;

        if (!IsInitialized)
            //throw new InvalidOperationException("Trader not initialized");

        if (i >= Data.Count)
            return;

        OnRun?.Invoke(this, 0);

        // emir_oncesi_dongu_foksiyonlarini_calistir  - beg

        //dongu_basi_degiskenleri_resetle(i);

        //dongu_basi_degiskenleri_guncelle(i);

        //anlik_kar_zarar_hesapla(i);

        //emirleri_resetle(i);
        this.signals.None = this.signals.Al = this.signals.Sat = this.signals.FlatOl = this.signals.KarAl = this.signals.ZararKes = this.signals.PasGec = false;
        this.signals.Sinyal = "";

        if (this.signals.IsTradeEnabled)
            this.signals.IsTradeEnabled = false;

        if (this.signals.IsPozKapatEnabled)
            this.signals.IsPozKapatEnabled = false;

        if (this.signals.GunSonuPozKapatildi)
            this.signals.GunSonuPozKapatildi = false;

        if (this.signals.KarAlindi || this.signals.ZararKesildi || this.signals.FlatOlundu)
        {
            this.signals.KarAlindi = false;
            this.signals.ZararKesildi = false;
            this.signals.FlatOlundu = false;
            this.signals.PozAcilabilir = false;
        }

        if (this.signals.PozAcilabilir == false)
        {
            this.signals.PozAcilabilir = true;
            this.signals.PozAcildi = false;
        }

        this.signals.IsTradeEnabled = true;

        // emir_oncesi_dongu_foksiyonlarini_calistir  - end

        if (i < 1)
            return;

        this.strategySignal = ExecuteStrategy(i);

        MapStrategyCommandsToTradeCommands(this.strategySignal);

        OnBeforeOrder?.Invoke(this, barIndex);
        ExecuteOrders(i);   // TODO: Strategy evaluate, sinyal üret, emir uygula
        OnAfterOrder?.Invoke(this, barIndex);

        OnRun?.Invoke(this, 1);

        int totalBars = GetDataCount();
        double percentage = (i + 1) / (double)totalBars * 100.0;
        OnProgress?.Invoke(this, i+1, totalBars, percentage);
    }

    public void Finalize(bool dispose)
    {
        OnFinal?.Invoke(this, dispose ? 1 : 0);
    }
    public SingleTrader CreateModules()
    {
        /*signals = new Signals();
        status = new Status();
        flags = new Flags();
        lists = new Lists();
        timeUtils = new TimeUtils();
        timeUtils.SetTrader(this);
        karZarar = new KarZarar(this);
        karAlZararKes = new KarAlZararKes();
        karAlZararKes.SetTrader(this);
        komisyon = new Komisyon();
        komisyon.SetTrader(this);
        Bakiye = new Bakiye();
        Bakiye.SetTrader(this);
        bakiye = new Bakiye();
        bakiye.SetTrader(this);
        pozisyonBuyuklugu = new PozisyonBuyuklugu();
        Position = new Position();
        statistics = new AlgoTradeWithOptimizationSupportWinFormsApp.Trading.Statistics.Statistics();*/

        initialTradeParams = new InitialTradeParams();

        signals = new Signals();

        status = new Status();

        flags = new Flags();

        lists = new Lists();

        return this;
    }
    public SingleTrader ResetModules()
    {
        initialTradeParams.Reset();

        signals.Reset();

        status.Reset();

        flags.Reset();

        lists.Reset();

        return this;
    }
    public SingleTrader InitModules()
    {
        initialTradeParams.Init();

        signals.Init();

        status.Init();

        flags.Init();

        lists.InitOrReuse(_data.Count);

        return this;
    }
    public SingleTrader DeleteModules()
    {
        initialTradeParams = null;

        signals = null;

        status = null;

        flags = null;

        lists = null;

        return this;
    }

    public TradeSignals ExecuteStrategy(int barIndex)
    {
        return TradeSignals.None;
    }
    public void MapStrategyCommandsToTradeCommands(TradeSignals strategySignal)
    {
        if (strategySignal == TradeSignals.None)
        {
            this.signals.None = true;
        }

        if (strategySignal == TradeSignals.Buy)
        {
            if (this.signals.AlEnabled)
                this.signals.Al = true;
        }

        if (strategySignal == TradeSignals.Sell)
        {
            if (this.signals.SatEnabled)
                this.signals.Sat = true;
        }

        if (strategySignal == TradeSignals.TakeProfit)
        {
            if (this.signals.KarAlEnabled)
                this.signals.KarAl = true;
        }

        if (strategySignal == TradeSignals.StopLoss)
        {
            if (this.signals.ZararKesEnabled)
                this.signals.ZararKes = true;
        }

        if (strategySignal == TradeSignals.Flat)
        {
            if (this.signals.FlatOlEnabled)
                this.signals.FlatOl = true;
        }

        if (strategySignal == TradeSignals.Skip)
        {
            if (this.signals.PasGecEnabled)
                this.signals.PasGec = true;
        }
    }

    public int ExecuteOrders(int barIndex)
    {
        int result = 0;

        int i = barIndex;

        // ------------------------------------------------------------------------------
        this.flags.AGerceklesti = false;
        this.flags.SGerceklesti = false;
        this.flags.FGerceklesti = false;
        this.flags.PGerceklesti = false;

        double AnlikKapanisFiyati = this.Data[i].Close;
        double AnlikYuksekFiyati = this.Data[i].High;
        double AnlikDusukFiyati = this.Data[i].Low;

        // ------------------------------------------------------------------------------
        if (this.signals.None)
        {

        }
        if (this.signals.Al)
        {
            this.signals.Sinyal = "A";
            this.signals.EmirKomut = 1;
            this.status.AlKomutSayisi += 1;
        }
        if (this.signals.Sat)
        {
            this.signals.Sinyal = "S";
            this.signals.EmirKomut = 2;
            this.status.SatKomutSayisi += 1;
        }
        if (this.signals.PasGec)
        {
            this.signals.Sinyal = "P";
            this.signals.EmirKomut = 3;
            this.status.PasGecKomutSayisi += 1;
        }
        if (this.signals.KarAl)
        {
            this.signals.Sinyal = "F";
            this.signals.EmirKomut = 4;
            this.status.KarAlKomutSayisi += 1;
        }
        if (this.signals.ZararKes)
        {
            this.signals.Sinyal = "F";
            this.signals.EmirKomut = 5;
            this.status.ZararKesKomutSayisi += 1;
        }
        if (this.signals.FlatOl)
        {
            this.signals.Sinyal = "F";
            this.signals.EmirKomut = 6;
            this.status.FlatOlKomutSayisi += 1;
        }

        // ------------------------------------------------------------------------------
        this.status.KarAlSayisi = this.status.KarAlKomutSayisi;
        this.status.ZararKesSayisi = this.status.ZararKesKomutSayisi;

        // ------------------------------------------------------------------------------
        // Process "A" (Al/Buy) signal
        if (this.signals.Sinyal == "A" && this.signals.SonYon != "A")
        {
            this.signals.PrevAFiyat = this.signals.SonAFiyat;
            this.signals.PrevABarNo = this.signals.SonABarNo;
            this.signals.PrevYon    = this.signals.SonYon;
            this.signals.PrevFiyat  = this.signals.SonFiyat;
            this.signals.PrevBarNo  = this.signals.SonBarNo;

            // Pozisyon büyüklüğünü kaydet (dinamik lot desteği için)
            this.signals.PrevVarlikAdedSayisi = this.signals.SonVarlikAdedSayisi;
            this.signals.PrevVarlikAdedSayisiMicro = this.signals.SonVarlikAdedSayisiMicro;

            if (this.signals.PrevYon == "F")
            {
                // pass
            }
            if (this.signals.PrevYon == "S")
            {
                // pass
            }

            this.lists.YonList[i] = "A";
            this.signals.SonYon = this.lists.YonList[i];
            this.signals.SonFiyat = AnlikKapanisFiyati;

            if (this.flags.KaymayiDahilEt)
            {
                this.signals.SonFiyat = AnlikYuksekFiyati;
            }

            this.lists.SeviyeList[i] = this.signals.SonFiyat;
            this.signals.SonBarNo = i;
            this.signals.SonAFiyat = this.signals.SonFiyat;
            this.signals.SonABarNo = this.signals.SonBarNo;

            // Yeni pozisyon büyüklüğünü kaydet (hem normal hem micro)
            this.signals.SonVarlikAdedSayisi = this.initialTradeParams.VarlikAdedSayisi;
            this.signals.SonVarlikAdedSayisiMicro = this.initialTradeParams.VarlikAdedSayisiMicro;

            this.lists.SonVarlikAdedSayisiList[i] = this.signals.SonVarlikAdedSayisi;
            this.lists.SonVarlikAdedSayisiMicroList[i] = this.signals.SonVarlikAdedSayisiMicro;

            if (this.signals.PrevYon == "F")
            {
                // F → A: Yeni pozisyon açma (1 işlem)
                // İşlem hacmi: SonVarlikAdedSayisi
                this.status.KomisyonIslemSayisi += 1;
                this.signals.EmirStatus = 1;
            }
            if (this.signals.PrevYon == "S")
            {
                // S → A: Ters yön değişimi (2 ayrı işlem)
                // İşlem 1: Short pozisyonu KAPAT (PrevVarlikAdedSayisi lot)
                // İşlem 2: Long pozisyon AÇ (SonVarlikAdedSayisi lot)
                // Toplam işlem hacmi: PrevVarlikAdedSayisi + SonVarlikAdedSayisi

                double fark = this.signals.SonFiyat - this.signals.SonSFiyat;
                if (fark < 0)
                {
                    this.status.KazandiranSatisSayisi += 1;
                }
                else if (fark > 0)
                {
                    this.status.KaybettirenSatisSayisi += 1;
                }
                else
                {
                    this.status.NotrSatisSayisi += 1;
                }

                // 2 işlem: Kapatma + Açma
                this.status.KomisyonIslemSayisi += 2;
                this.signals.EmirStatus = 2;
            }

            this.flags.BakiyeGuncelle = true;
            this.flags.KomisyonGuncelle = true;
            this.flags.DonguSonuIstatistikGuncelle = true;
            this.status.IslemSayisi += 1;
            this.status.AlisSayisi += 1;
            this.flags.AGerceklesti = true;

            //OnNotifyStrategySignal?.Invoke(this, this.signals.Sinyal, i);

        }
        // Process "S" (Sat/Sell) signal
        else if (this.signals.Sinyal == "S" && this.signals.SonYon != "S")
        {
            this.signals.PrevSFiyat = this.signals.SonSFiyat;
            this.signals.PrevSBarNo = this.signals.SonSBarNo;
            this.signals.PrevYon    = this.signals.SonYon;
            this.signals.PrevFiyat  = this.signals.SonFiyat;
            this.signals.PrevBarNo  = this.signals.SonBarNo;

            // Pozisyon büyüklüğünü kaydet (dinamik lot desteği için)
            this.signals.PrevVarlikAdedSayisi = this.signals.SonVarlikAdedSayisi;
            this.signals.PrevVarlikAdedSayisiMicro = this.signals.SonVarlikAdedSayisiMicro;

            if (this.signals.PrevYon == "F")
            {
                // pass
            }
            if (this.signals.PrevYon == "A")
            {
                // pass
            }

            this.lists.YonList[i] = "S";
            this.signals.SonYon = this.lists.YonList[i];
            this.signals.SonFiyat = AnlikKapanisFiyati;

            if (this.flags.KaymayiDahilEt)
            {
                this.signals.SonFiyat = AnlikDusukFiyati;
            }

            this.lists.SeviyeList[i] = this.signals.SonFiyat;
            this.signals.SonBarNo = i;
            this.signals.SonSFiyat = this.signals.SonFiyat;
            this.signals.SonSBarNo = this.signals.SonSBarNo;

            // Yeni pozisyon büyüklüğünü kaydet (hem normal hem micro)
            this.signals.SonVarlikAdedSayisi = this.initialTradeParams.VarlikAdedSayisi;
            this.signals.SonVarlikAdedSayisiMicro = this.initialTradeParams.VarlikAdedSayisiMicro;

            this.lists.SonVarlikAdedSayisiList[i] = this.signals.SonVarlikAdedSayisi;
            this.lists.SonVarlikAdedSayisiMicroList[i] = this.signals.SonVarlikAdedSayisiMicro;

            if (this.signals.PrevYon == "F")
            {
                // F → S: Yeni pozisyon açma (1 işlem)
                // İşlem hacmi: SonVarlikAdedSayisi
                this.status.KomisyonIslemSayisi += 1;
                this.signals.EmirStatus = 3;
            }
            if (this.signals.PrevYon == "A")
            {
                // A → S: Ters yön değişimi (2 ayrı işlem)
                // İşlem 1: Long pozisyonu KAPAT (PrevVarlikAdedSayisi lot)
                // İşlem 2: Short pozisyon AÇ (SonVarlikAdedSayisi lot)
                // Toplam işlem hacmi: PrevVarlikAdedSayisi + SonVarlikAdedSayisi

                double fark = this.signals.SonFiyat - this.signals.SonAFiyat;
                if (fark > 0)
                {
                    this.status.KazandiranAlisSayisi += 1;
                }
                else if (fark < 0)
                {
                    this.status.KaybettirenAlisSayisi += 1;
                }
                else
                {
                    this.status.NotrAlisSayisi += 1;
                }

                // 2 işlem: Kapatma + Açma
                this.status.KomisyonIslemSayisi += 2;
                this.signals.EmirStatus = 4;
            }

            this.flags.BakiyeGuncelle = true;
            this.flags.KomisyonGuncelle = true;
            this.flags.DonguSonuIstatistikGuncelle = true;
            this.status.IslemSayisi += 1;
            this.status.SatisSayisi += 1;
            this.flags.SGerceklesti = true;

            //OnNotifyStrategySignal?.Invoke(this, this.signals.Sinyal, i);
        }
        // Process "F" (Flat) signal
        else if (this.signals.Sinyal == "F" && this.signals.SonYon != "F")
        {
            this.signals.PrevFFiyat = this.signals.SonFFiyat;
            this.signals.PrevFBarNo = this.signals.SonFBarNo;
            this.signals.PrevYon    = this.signals.SonYon;
            this.signals.PrevFiyat  = this.signals.SonFiyat;
            this.signals.PrevBarNo  = this.signals.SonBarNo;

            // Pozisyon büyüklüğünü kaydet (dinamik lot desteği için)
            this.signals.PrevVarlikAdedSayisi = this.signals.SonVarlikAdedSayisi;
            this.signals.PrevVarlikAdedSayisiMicro = this.signals.SonVarlikAdedSayisiMicro;

            if (this.signals.PrevYon == "A")
            {
                // pass
            }
            if (this.signals.PrevYon == "S")
            {
                // pass
            }

            this.lists.YonList[i] = "F";
            this.signals.SonYon = this.lists.YonList[i];
            this.signals.SonFiyat = AnlikKapanisFiyati;

            if (this.flags.KaymayiDahilEt)
            {
                if (this.signals.PrevYon == "A")
                {
                    this.signals.SonFiyat = AnlikDusukFiyati;
                }
                if (this.signals.PrevYon == "S")
                {
                    this.signals.SonFiyat = AnlikYuksekFiyati;
                }
            }

            this.lists.SeviyeList[i] = this.signals.SonFiyat;
            this.signals.SonBarNo = i;
            this.signals.SonFFiyat = this.signals.SonFiyat;
            this.signals.SonFBarNo = this.signals.SonFBarNo;

            // Flat durumunda pozisyon yok (hem normal hem micro)
            this.signals.SonVarlikAdedSayisi = 0.0;
            this.signals.SonVarlikAdedSayisiMicro = 0.0;

            this.lists.SonVarlikAdedSayisiList[i] = this.signals.SonVarlikAdedSayisi;
            this.lists.SonVarlikAdedSayisiMicroList[i] = this.signals.SonVarlikAdedSayisiMicro;

            if (this.signals.PrevYon == "A")
            {
                // A → F: Long pozisyonu KAPAT (1 işlem)
                // İşlem hacmi: PrevVarlikAdedSayisi

                double fark = this.signals.SonFiyat - this.signals.SonAFiyat;
                if (fark > 0)
                {
                    this.status.KazandiranAlisSayisi += 1;
                }
                else if (fark < 0)
                {
                    this.status.KaybettirenAlisSayisi += 1;
                }
                else
                {
                    this.status.NotrAlisSayisi += 1;
                }

                // 1 işlem: Kapatma
                this.status.KomisyonIslemSayisi += 1;
                this.signals.EmirStatus = 5;
            }
            if (this.signals.PrevYon == "S")
            {
                // S → F: Short pozisyonu KAPAT (1 işlem)
                // İşlem hacmi: PrevVarlikAdedSayisi

                double fark = this.signals.SonFiyat - this.signals.SonSFiyat;
                if (fark < 0)
                {
                    this.status.KazandiranSatisSayisi += 1;
                }
                else if (fark > 0)
                {
                    this.status.KaybettirenSatisSayisi += 1;
                }
                else
                {
                    this.status.NotrSatisSayisi += 1;
                }

                // 1 işlem: Kapatma
                this.status.KomisyonIslemSayisi += 1;
                this.signals.EmirStatus = 6;
            }

            this.flags.BakiyeGuncelle = true;
            this.flags.KomisyonGuncelle = true;
            this.flags.DonguSonuIstatistikGuncelle = true;
            this.status.IslemSayisi += 1;
            this.status.FlatSayisi += 1;
            this.flags.FGerceklesti = true;

            //OnNotifyStrategySignal?.Invoke(this, this.signals.Sinyal, i);

        }
        // Process "P" (PasGec/Skip) or empty signal
        else if (this.signals.Sinyal == "P" || this.signals.Sinyal == "")
        {
            this.signals.PrevPFiyat = this.signals.SonPFiyat;
            this.signals.PrevPBarNo = this.signals.SonPBarNo;
            this.signals.SonPFiyat = AnlikKapanisFiyati;
            this.signals.SonPBarNo = i;

            if (this.signals.SonYon == "A")
            {
                this.signals.EmirStatus = 7;
            }
            if (this.signals.SonYon == "S")
            {
                this.signals.EmirStatus = 8;
            }
            if (this.signals.SonYon == "F")
            {
                this.signals.EmirStatus = 9;
            }

            this.flags.BakiyeGuncelle = false; // "P" sinyali bakiye güncellemez, aksi halde yüzeysel kâr mükerrer eklenir (double-counting)
            this.flags.KomisyonGuncelle = true;
            this.flags.DonguSonuIstatistikGuncelle = true;
            this.status.PassSayisi += 1;
            this.flags.PGerceklesti = true;
        }
        // Process "A" (Al/Buy) signal - Pyramiding (Long pozisyon artırma)
        else if (this.signals.Sinyal == "A" && this.signals.SonYon == "A")
        {
            // Pyramiding etkin mi kontrol et
            if (!this.initialTradeParams.PyramidingEnabled)
            {
                // Pyramiding kapalı - işlem yapma, sinyali göz ardı et
                return result;
            }

            bool isMicroLot = this.initialTradeParams.MicroLotSizeEnabled;

            // Mevcut pozisyon büyüklüğü
            double mevcutLot = isMicroLot
                ? this.signals.SonVarlikAdedSayisiMicro
                : this.signals.SonVarlikAdedSayisi;

            // Eklenecek lot büyüklüğü
            double yeniLot = isMicroLot
                ? this.initialTradeParams.VarlikAdedSayisiMicro
                : this.initialTradeParams.VarlikAdedSayisi;

            // Maksimum pozisyon kontrolü
            if (this.initialTradeParams.MaxPositionSizeEnabled)
            {
                double maxLot = isMicroLot
                    ? this.initialTradeParams.MaxPositionSizeMicro
                    : this.initialTradeParams.MaxPositionSize;

                if (mevcutLot + yeniLot > maxLot)
                {
                    // Limit aşıldı - işlem yapma
                    return result;
                }
            }

            this.signals.PrevAFiyat = this.signals.SonAFiyat;
            this.signals.PrevABarNo = this.signals.SonABarNo;
            this.signals.PrevYon = this.signals.SonYon;
            this.signals.PrevFiyat = this.signals.SonFiyat;
            this.signals.PrevBarNo = this.signals.SonBarNo;

            // Prev değerlerini kaydet (komisyon hesabı için)
            this.signals.PrevVarlikAdedSayisi = this.signals.SonVarlikAdedSayisi;
            this.signals.PrevVarlikAdedSayisiMicro = this.signals.SonVarlikAdedSayisiMicro;

            // Yön ve sinyal bilgilerini güncelle
            this.lists.YonList[i] = "A";
            this.signals.SonYon = this.lists.YonList[i];

            // Ağırlıklı ortalama giriş fiyatı hesapla
            double eskiFiyat = this.signals.SonAFiyat;
            double yeniFiyat = AnlikKapanisFiyati;

            // Kayma (slippage) kontrolü - Long için yüksek fiyat (daha kötü)
            if (this.flags.KaymayiDahilEt)
            {
                yeniFiyat = AnlikYuksekFiyati;
            }

            double toplamLot = mevcutLot + yeniLot;
            double agirlikliOrtalamaFiyat = (mevcutLot * eskiFiyat + yeniLot * yeniFiyat) / toplamLot;

            // Pozisyon büyüklüğünü güncelle (toplama)
            if (isMicroLot)
                this.signals.SonVarlikAdedSayisiMicro = toplamLot;
            else
                this.signals.SonVarlikAdedSayisi = toplamLot;

            this.lists.SonVarlikAdedSayisiList[i] = this.signals.SonVarlikAdedSayisi;
            this.lists.SonVarlikAdedSayisiMicroList[i] = this.signals.SonVarlikAdedSayisiMicro;

            // Giriş fiyatını ağırlıklı ortalama ile güncelle
            this.signals.SonAFiyat = agirlikliOrtalamaFiyat;
            this.signals.SonABarNo = i;
            this.signals.SonFiyat = agirlikliOrtalamaFiyat;
            this.signals.SonBarNo = i;

            // Seviye listesini güncelle
            this.lists.SeviyeList[i] = this.signals.SonFiyat;

            // Komisyon işlem sayısı (sadece 1 işlem - ekleme)
            this.status.KomisyonIslemSayisi += 1;

            // EmirStatus = 10 (A→A: Long pozisyon artırma)
            this.signals.EmirStatus = 10;

            // Flags ve Status güncellemeleri
            this.flags.BakiyeGuncelle = false;  // Pozisyon kapatılmadı, kar/zarar gerçekleşmedi
            this.flags.KomisyonGuncelle = true;  // Komisyon ödendi
            this.flags.DonguSonuIstatistikGuncelle = true;  // İstatistik güncelle
            this.status.IslemSayisi += 1;  // Yeni işlem yapıldı
            this.status.AlisSayisi += 1;  // Alış işlemi
            this.flags.AGerceklesti = true;

            //OnNotifyStrategySignal?.Invoke(this, this.signals.Sinyal, i);

        }
        // Process "S" (Sat/Sell) signal - Pyramiding (Short pozisyon artırma)
        else if (this.signals.Sinyal == "S" && this.signals.SonYon == "S")
        {
            // Pyramiding etkin mi kontrol et
            if (!this.initialTradeParams.PyramidingEnabled)
            {
                // Pyramiding kapalı - işlem yapma, sinyali göz ardı et
                return result;
            }

            bool isMicroLot = this.initialTradeParams.MicroLotSizeEnabled;

            // Mevcut pozisyon büyüklüğü
            double mevcutLot = isMicroLot
                ? this.signals.SonVarlikAdedSayisiMicro
                : this.signals.SonVarlikAdedSayisi;

            // Eklenecek lot büyüklüğü
            double yeniLot = isMicroLot
                ? this.initialTradeParams.VarlikAdedSayisiMicro
                : this.initialTradeParams.VarlikAdedSayisi;

            // Maksimum pozisyon kontrolü
            if (this.initialTradeParams.MaxPositionSizeEnabled)
            {
                double maxLot = isMicroLot
                    ? this.initialTradeParams.MaxPositionSizeMicro
                    : this.initialTradeParams.MaxPositionSize;

                if (mevcutLot + yeniLot > maxLot)
                {
                    // Limit aşıldı - işlem yapma
                    return result;
                }
            }

            this.signals.PrevSFiyat = this.signals.SonSFiyat;
            this.signals.PrevSBarNo = this.signals.SonSBarNo;
            this.signals.PrevYon = this.signals.SonYon;
            this.signals.PrevFiyat = this.signals.SonFiyat;
            this.signals.PrevBarNo = this.signals.SonBarNo;

            // Prev değerlerini kaydet (komisyon hesabı için)
            this.signals.PrevVarlikAdedSayisi = this.signals.SonVarlikAdedSayisi;
            this.signals.PrevVarlikAdedSayisiMicro = this.signals.SonVarlikAdedSayisiMicro;

            // Yön ve sinyal bilgilerini güncelle
            this.lists.YonList[i] = "S";
            this.signals.SonYon = this.lists.YonList[i];

            // Ağırlıklı ortalama giriş fiyatı hesapla
            double eskiFiyat = this.signals.SonSFiyat;
            double yeniFiyat = AnlikKapanisFiyati;

            // Kayma (slippage) kontrolü - Short için düşük fiyat (daha kötü)
            if (this.flags.KaymayiDahilEt)
            {
                yeniFiyat = AnlikDusukFiyati;
            }

            double toplamLot = mevcutLot + yeniLot;
            double agirlikliOrtalamaFiyat = (mevcutLot * eskiFiyat + yeniLot * yeniFiyat) / toplamLot;

            // Pozisyon büyüklüğünü güncelle (toplama)
            if (isMicroLot)
                this.signals.SonVarlikAdedSayisiMicro = toplamLot;
            else
                this.signals.SonVarlikAdedSayisi = toplamLot;

            this.lists.SonVarlikAdedSayisiList[i] = this.signals.SonVarlikAdedSayisi;
            this.lists.SonVarlikAdedSayisiMicroList[i] = this.signals.SonVarlikAdedSayisiMicro;

            // Giriş fiyatını ağırlıklı ortalama ile güncelle
            this.signals.SonSFiyat = agirlikliOrtalamaFiyat;
            this.signals.SonSBarNo = i;
            this.signals.SonFiyat = agirlikliOrtalamaFiyat;
            this.signals.SonBarNo = i;

            // Seviye listesini güncelle
            this.lists.SeviyeList[i] = this.signals.SonFiyat;

            // Komisyon işlem sayısı (sadece 1 işlem - ekleme)
            this.status.KomisyonIslemSayisi += 1;

            // EmirStatus = 11 (S→S: Short pozisyon artırma)
            this.signals.EmirStatus = 11;

            // Flags ve Status güncellemeleri
            this.flags.BakiyeGuncelle = false;  // Pozisyon kapatılmadı, kar/zarar gerçekleşmedi
            this.flags.KomisyonGuncelle = true;  // Komisyon ödendi
            this.flags.DonguSonuIstatistikGuncelle = true;  // İstatistik güncelle
            this.status.IslemSayisi += 1;  // Yeni işlem yapıldı
            this.status.SatisSayisi += 1;  // Satış işlemi
            this.flags.SGerceklesti = true;

            //OnNotifyStrategySignal?.Invoke(this, this.signals.Sinyal, i);
        }
        // Process "F" (Flat) signal - Zaten Flat
        else if (this.signals.Sinyal == "F" && this.signals.SonYon == "F")
        {
            // Zaten Flat durumdayız ve yeni sinyal de Flat
            // Hiçbir işlem yapılmaz, EmirStatus güncellenmez
            // Bu durum normal akış içinde yer alır
        }

        // ------------------------------------------------------------------------------
        // Reset flags
        this.flags.AGerceklesti = false;
        this.flags.SGerceklesti = false;
        this.flags.FGerceklesti = false;
        this.flags.PGerceklesti = false;

        return result;
    }

    public void Dispose()
    {
        DeleteModules();
    }
}
