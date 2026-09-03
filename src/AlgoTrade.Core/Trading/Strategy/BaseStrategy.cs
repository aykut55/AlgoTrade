using System.Collections.Generic;
using AlgoTrade.Core;
using AlgoTrade.Core.Logging;
using AlgoTrade.Core.Trading.Indicators;
using AlgoTrade.Core.Trading.Core;
using AlgoTrade.Core.Trading;

namespace AlgoTrade.Core.Trading.Strategy
{
    /// <summary>
    /// Base strategy class
    /// Provides common functionality for all strategies
    /// </summary>
    public abstract class BaseStrategy : IStrategy
    {
        #region Properties

        public abstract string Name { get; }
        public Dictionary<string, object> Parameters { get; protected set; }

        /// <summary>Market data</summary>
        protected List<StockData> Data { get; set; }

        /// <summary>Indicator manager</summary>
        protected IndicatorManager Indicators { get; set; }

        /// <summary>Trader instance - allows strategy to access trader's state and methods</summary>
        protected SingleTrader Trader { get; private set; }

        /// <summary>Is initialized?</summary>
        protected bool IsInitialized { get; set; }
        protected LogManager? Logger { get; private set; }

        #endregion

        #region Common Series (OHLCV/tarih/saat)

        // Her stratejide birebir ayni sekilde Indicators.Get*() ile doldurulan ortak diziler -
        // LoadCommonSeries() tarafindan, Initialize() icinde OnInit()'ten ONCE (derived OnInit'in
        // base cagirmasina bagli olmadan garanti sirayla) bir kez doldurulur.
        protected int barCount;
        protected double[]? openPrices;
        protected double[]? highPrices;
        protected double[]? lowPrices;
        protected double[]? closePrices;
        protected long[]? volumes;
        protected long[]? lotSizes;
        protected DateTime[]? dateTimes;
        protected DateOnly[]? dates;
        protected TimeOnly[]? times;
        protected long[]? epochTimes;

        // Run baglami - ILK OnStep cagrisinda Trader'dan cozulur (OnInit'te DEGIL: OnInit
        // constructor'dan calisir, SetTrader() daha sonra SetStrategy() icinde -> OnInit'te Trader null).
        protected bool runContextResolved;
        protected int  timeframeMinutes;    // 1, 5, 15, 60, 240 ... (0 = SymbolPeriod sayisal degil/cozulemedi)
        protected bool isOptimizationRun;   // true = opt taramasi icinde (Trader.OptimizationEnabled), false = tekli kosu

        // timeframeMinutes'in türevi - run boyunca degismez, ResolveRunContext'te bir kez set edilir
        protected bool isOneMinute, isFiveMinute, isOneHour, isFourHour, isOneDay;

        // Gun ici saat penceresi / tarih penceresi / triggerTime - tanim burada, deger ataması
        // derived class'in kendi constructor'inda yapilir (readonly DEGIL, derived assign edebilsin diye).
        // isTimeEnabled/isDayEnabled false ise ilgili pencere dikkate alinmaz.
        protected TimeOnly startTime;
        protected TimeOnly stopTime;
        protected DateOnly startDay;
        protected DateOnly stopDay;
        protected bool isTimeEnabled;
        protected bool isDayEnabled;
        protected TimeOnly triggerTime;
        protected bool isTriggerTimeEnabled;

        // signalModeIndex/exitModeIndex/flatModeIndex/skipModeIndex/ruleModeIndex - jenerik dispatch
        // parametreleri, tanim burada ama deger constructor parametresi olarak derived class'tan atanir.
        // signalModeIndex'in ANLAMI (hangi sinyal mantigina dispatch ettigi) her stratejide farkli olabilir -
        // sadece alan tanimi (private readonly int -> protected int) her stratejide birebir ayni oldugu icin
        // burada; dispatch mantigi (OnStep'teki if/else zinciri) yine ilgili stratejide kalir.
        protected int signalModeIndex;
        protected int exitModeIndex;
        protected int flatModeIndex;
        protected int skipModeIndex;
        protected int ruleModeIndex;

        // isFirstOfDay/.../isSonYonF - ResolveRunContext(currentIndex) tarafindan HER OnStep cagrisinda
        // (guard'dan once) guncellenir; field olduklari icin OnStep'te dogrudan kullanilabilirler.
        protected bool isFirstOfDay, isLastOfDay, isFirstOfWeek, isFirstOfMonth;
        protected string sonYon = "F";
        protected bool isSonYonA, isSonYonS, isSonYonF;

        /// <summary>
        /// Ortak OHLCV/tarih/saat dizilerini Indicators'dan okur. Initialize() icinde OnInit()'ten
        /// once cagrilir - derived class'lar OnInit()'i override edip base.OnInit() cagirmasa bile
        /// bu diziler garanti dolu olur.
        /// </summary>
        private void LoadCommonSeries()
        {
            barCount    = Indicators.GetDataCount();
            openPrices  = Indicators.GetOpenPrices();
            highPrices  = Indicators.GetHighPrices();
            lowPrices   = Indicators.GetLowPrices();
            closePrices = Indicators.GetClosePrices();
            volumes     = Indicators.GetVolume();
            lotSizes    = Indicators.GetLotSizes();
            dateTimes   = Indicators.GetDateTimes();
            dates       = Indicators.GetDates();
            times       = Indicators.GetTimes();
            epochTimes  = Indicators.GetEpochTimes();

            bool lengthsMatch = true;
            lengthsMatch &= openPrices.Length  == barCount;
            lengthsMatch &= highPrices.Length  == barCount;
            lengthsMatch &= lowPrices.Length   == barCount;
            lengthsMatch &= closePrices.Length == barCount;
            lengthsMatch &= volumes.Length     == barCount;
            lengthsMatch &= lotSizes.Length    == barCount;
            lengthsMatch &= dateTimes.Length   == barCount;
            lengthsMatch &= dates.Length       == barCount;
            lengthsMatch &= times.Length       == barCount;
            lengthsMatch &= epochTimes.Length  == barCount;

            if (!lengthsMatch)
            {
                throw new InvalidOperationException(
                    $"Ortak seri uzunluklari uyusmuyor (barCount={barCount}): " +
                    $"open={openPrices.Length}, high={highPrices.Length}, low={lowPrices.Length}, close={closePrices.Length}, " +
                    $"volume={volumes.Length}, lot={lotSizes.Length}, dateTime={dateTimes.Length}, date={dates.Length}, " +
                    $"time={times.Length}, epoch={epochTimes.Length}");
            }
        }

        /// <summary>
        /// Run baglamini (timeframe + opt mu) Trader'dan bir kez cozer. OnInit'te yapilamiyor
        /// (orada Trader henuz null); ilk OnStep cagrisinda cagrilir.
        /// </summary>
        protected void ResolveRunContext(int currentIndex)
        {
            // Her OnStep'te (guard'dan once) taze hesaplanir - bar bar degisen degerler.
            isFirstOfDay   = IsFirstBarOfDay(currentIndex);
            isLastOfDay    = IsLastBarOfDay(currentIndex);
            isFirstOfWeek  = IsFirstBarOfWeek(currentIndex);
            isFirstOfMonth = IsFirstBarOfMonth(currentIndex);
            sonYon         = Trader?.signals?.SonYon ?? "F";
            isSonYonA      = sonYon == "A";
            isSonYonS      = sonYon == "S";
            isSonYonF      = sonYon == "F";

            if (runContextResolved)
                return;

            runContextResolved = true;

            isOptimizationRun = Trader?.OptimizationEnabled == true;

            // SymbolPeriod: intraday'de dakika sayisi string'i ("5","15","240"); A/G/H = Aylik/Gunluk/Haftalik.
            // Cozulemezse (null / "" / "N/A") timeframeMinutes = 0 -> cagiran kod "bilinmiyor" diye ele alir.
            string sp = (Trader?.SymbolPeriod ?? "").Trim().ToUpperInvariant();
            timeframeMinutes = sp switch
            {
                "G" => 1440,      // 1 gun   (takvim dk)
                "H" => 10080,     // 1 hafta
                "A" => 43200,     // ~1 ay
                "Y" => 525600,    // ~1 yil  (365 * 1440)
                _   => (int.TryParse(sp, out var tf) && tf > 0) ? tf : 0
            };

            isOneMinute  = timeframeMinutes == 1;
            isFiveMinute = timeframeMinutes == 5;
            isOneHour    = timeframeMinutes == 60;
            isFourHour   = timeframeMinutes == 240;
            isOneDay     = timeframeMinutes == 1440;

            // Opt'ta konsolu bogmasin diye sadece tekli kosuda logla
            if (!isOptimizationRun)
            {
                string tfStr = Trader?.SymbolPeriod ?? "?";
                Log($"[{Name}] timeframe={tfStr} ({timeframeMinutes}dk), optRun={isOptimizationRun}");
            }
        }

        /// <summary>
        /// Bu bar günün ilk barı mı? Periyottan bağımsız - dates[] takvim tarihini karşılaştırır,
        /// bar sayımına dayanmaz (1dk/15dk/1h/4h fark etmeksizin aynı şekilde çalışır).
        /// </summary>
        protected bool IsFirstBarOfDay(int currentIndex)
        {
            if (currentIndex <= 0)
                return true;

            return dates[currentIndex] != dates[currentIndex - 1];
        }

        /// <summary>
        /// Bu bar günün son barı mı? Periyottan bağımsız - bir sonraki barın dates[] takvim
        /// tarihine bakar (lookahead); veri setinin son barıysa da true döner.
        /// </summary>
        protected bool IsLastBarOfDay(int currentIndex)
        {
            if (currentIndex >= barCount - 1)
                return true;

            return dates[currentIndex + 1] != dates[currentIndex];
        }

        /// <summary>
        /// Bu bar haftanın ilk barı mı? Periyottan bağımsız - ISO 8601 hafta numarasını karşılaştırır
        /// (yıl sınırını da doğru ele alır, örn. 2025 hafta 52 -> 2026 hafta 1).
        /// </summary>
        protected bool IsFirstBarOfWeek(int currentIndex)
        {
            if (currentIndex <= 0)
                return true;

            var current = dates[currentIndex].ToDateTime(TimeOnly.MinValue);
            var prev = dates[currentIndex - 1].ToDateTime(TimeOnly.MinValue);

            int currentWeek = System.Globalization.ISOWeek.GetWeekOfYear(current);
            int prevWeek = System.Globalization.ISOWeek.GetWeekOfYear(prev);
            int currentIsoYear = System.Globalization.ISOWeek.GetYear(current);
            int prevIsoYear = System.Globalization.ISOWeek.GetYear(prev);

            return currentWeek != prevWeek || currentIsoYear != prevIsoYear;
        }

        /// <summary>
        /// Bu bar ayın ilk barı mı? Periyottan bağımsız - dates[] takvim ay/yılını karşılaştırır.
        /// </summary>
        protected bool IsFirstBarOfMonth(int currentIndex)
        {
            if (currentIndex <= 0)
                return true;

            var current = dates[currentIndex];
            var prev = dates[currentIndex - 1];

            return current.Month != prev.Month || current.Year != prev.Year;
        }

        #endregion

        #region Constructor

        protected BaseStrategy()
        {
            Parameters = new Dictionary<string, object>();
            IsInitialized = false;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Initialize strategy with data and indicators
        /// </summary>
        public void Initialize(List<StockData> data, IndicatorManager indicators)
        {
            Data = data;
            Indicators = indicators;
            IsInitialized = true;
            LoadCommonSeries();
            OnInit();
        }

        /// <summary>
        /// Initialize strategy (override in derived classes)
        /// </summary>
        public virtual void OnInit()
        {
            // Override in derived classes
        }

        /// <summary>
        /// Called on each bar/step
        /// </summary>
        public abstract TradeSignals OnStep(int currentIndex);

        /// <summary>
        /// Reset strategy
        /// </summary>
        public virtual void Reset()
        {
            IsInitialized = false;
        }

        /// <summary>
        /// Set parameter
        /// </summary>
        public void SetParameter(string key, object value)
        {
            Parameters[key] = value;
        }

        /// <summary>
        /// Get parameter
        /// </summary>
        public T GetParameter<T>(string key, T defaultValue = default)
        {
            if (Parameters.TryGetValue(key, out var value))
                return (T)value;
            return defaultValue;
        }

        /// <summary>
        /// Set trader instance
        /// Allows strategy to access trader's state, methods, and modules (KarAlZararKes, etc.)
        /// </summary>
        public void SetTrader(SingleTrader trader)
        {
            Trader = trader;
        }

        public void SetLogger(LogManager? logger)
        {
            Logger = logger;
        }

        protected void Log(string message)
        {
            if (Logger is not null)
            {
                Logger.LogRawInstance(message);
                return;
            }

            LogManager.LogRaw(message);
        }

        protected void LogWarning(string message)
        {
            if (Logger is not null)
            {
                Logger.LogRawInstance(message);
                return;
            }

            LogManager.LogRaw(message);
        }

        /// <summary>
        /// Get indicators for plotting (default implementation returns null)
        /// Override in derived classes to provide strategy-specific indicators
        /// </summary>
        public virtual Dictionary<string, double[]>? GetPlotIndicators()
        {
            return null;
        }

        /// <summary>
        /// Opt sırasında bu parametre kombinasyonunun anlamlı olup olmadığını bildirir (IStrategy).
        /// Default true - stratejiler kendi mantığına göre override eder (örn. fast &gt;= slow).
        /// </summary>
        public virtual bool IsValidParameterCombination() => true;

        public virtual void Dispose()
        {
            Reset();
        }

        #endregion
    }
}
