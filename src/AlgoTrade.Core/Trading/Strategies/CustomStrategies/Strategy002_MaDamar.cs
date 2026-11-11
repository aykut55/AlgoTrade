using AlgoTrade.Core;
using AlgoTrade.Core.Trading.Core;
using AlgoTrade.Core.Trading.Indicators;
using AlgoTrade.Core.Trading.Indicators.Base;
using AlgoTrade.Core.Trading.Strategy;
using System;
using System.Collections.Generic;

namespace AlgoTrade.Core.Trading.Strategies
{
    /// <summary>
    /// Strategy002_MaDamar - Faz 1 "damar" (cok sinyal ureten ham sinyal) adayi: MA ailesi.
    /// Bkz. docs/damar-strateji-sinyal-katalogu.md, bolum 1 (MA) ve "Faz 1: Damar secimi".
    ///
    /// Amac kaliteli sinyal degil, olculebilir/karsilastirilabilir HAM frekans. Bu yuzden:
    /// - Filtre yok (TP/SL/flat/skip kapali, saat/gun penceresi kapali).
    /// - Tum modlar STATE bicimindedir: kosul surdukce her bar ayni yonde sinyal uretilir.
    ///   Trader zaten pozisyondayken ayni yonlu sinyalde yeni islem acmadigi icin islem, yalnizca
    ///   yon DONDUGUNDE olur. Boylece modlar arasindaki tek fark "yonun neye gore belirlendigi"
    ///   olur ve islem sayilari adil karsilastirilir. Gun sonu poz kapatma (gunSonuPozKapatEnabled)
    ///   aciksa, ertesi gunun ilk barinda kosul hala gecerliyse yeniden girilir.
    ///
    /// buySignalModeIndex / sellSignalModeIndex (ayni mod kumesi, AYRI secilebilir):
    ///   0: Fiyat / FastMA state   - fiyat fastMA'nin ustundeyse AL, altindaysa SAT (en sik)
    ///   1: Fiyat / SlowMA state   - fiyat slowMA'nin ustundeyse AL, altindaysa SAT
    ///   2: Fast / Slow state      - fastMA > slowMA ise AL, fastMA &lt; slowMA ise SAT
    ///   3: FastMA egim state      - fastMA, slopeLookback bar oncesinden yuksekse AL, dusukse SAT
    ///   4: SlowMA egim state      - slowMA, slopeLookback bar oncesinden yuksekse AL, dusukse SAT
    ///   5: Uc MA dizilimi (stack) - fast > mid > slow ise AL, fast &lt; mid &lt; slow ise SAT;
    ///                               dizilim karisiksa sinyal yok (pozisyon korunur)
    ///
    /// Parametreler:
    ///   signalModeIndex : >= 0 ise buy ve sell ayni moda ayarlanir (opt'ta modlari tek range ile taramak icin)
    ///   fastPeriod / midPeriod / slowPeriod : MA periyotlari (midPeriod sadece mod 5'te kullanilir;
    ///                   midPeriod &lt;= 0 ise fast ile slow'un geometrik ortalamasi alinir)
    ///   maMethod      : uc MA icin ortak hareketli ortalama tipi (varsayilan EMA)
    ///   priceSource   : MA'larin ve fiyat kiyaslamasinin kaynagi (varsayilan Close)
    ///   slopeLookback : egim modlarinda (3/4) kac bar geriye bakilacagi (varsayilan 1)
    ///   firstEntryModeIndex : pozisyon BOSKEN nasil girilecegi
    ///     0: state - kosul gecerliyse hemen girilir (gun sonu kapanisindan sonra ertesi sabah
    ///        ilk barda, gap'li acilis fiyatindan)
    ///     1: kesisim - giris yalnizca ayni gunun iki bari arasinda yonun degistigi barda yapilir
    ///        (gun basi, gun sonu kapanisi ve kar al / zarar kes / flat sonrasi). Gap'li acilislar ve
    ///        cikistan sonra eski kesisimle hemen yeniden giris elenir. Pozisyon aciksa state gecerli.
    ///     2: tetik - YON secilen moddan, ZAMANLAMA kisa tetik EMA'sindan: pozisyon bosken giris,
    ///        fiyatin triggerPeriod'luk MA'yi ayni gun icinde ana yon dogrultusunda kestigi barda
    ///        yapilir. Uzun periyodun kalitesi korunurken her gun daha cok giris firsati olusur.
    ///   triggerPeriod : firstEntryModeIndex 2'deki tetik MA periyodu (varsayilan 5, maMethod ile ayni tip)
    ///
    ///   takeProfitTL : &gt; 0 ise KOMISYON SONRASI anlik kar bu TL degerine ulasinca kar al
    ///                  (takeProfitExitModeIndex 6: mevcut KarZararFiyatSeviyesindenKarAlHesapla,
    ///                  esik = takeProfitTL + gidis-donus komisyon).
    ///                  0 = kar al kapali. Zarar kes YOK. firstEntryModeIndex=1 ile birlikte kar al
    ///                  sonrasi yeniden giris ancak yeni bir kesisimle olur.
    ///
    /// TP/SL/flat/skip dispatch'leri SimpleMAStrategy ile ayni standarttadir (+ TP mod 6) ama bu
    /// stratejide takeProfitTL disinda constructor'da kapatilir.
    /// </summary>
    public class Strategy002_MaDamar : BaseStrategy
    {
        public override string Name => "Strategy002 (MA Damar)";

        private readonly int fastPeriod;
        private readonly int midPeriod;
        private readonly int slowPeriod;
        private readonly MAMethod maMethod;
        private readonly PriceSource priceSource;
        private readonly int slopeLookback;

        private readonly int firstEntryModeIndex;
        private readonly double takeProfitTL;
        private readonly int triggerPeriod;
        private double[]? triggerMA;

        private double[]? source;
        private double[]? fastMA;
        private double[]? midMA;
        private double[]? slowMA;

        public Strategy002_MaDamar(List<StockData> data, IndicatorManager indicators,
            int fastPeriod = 10, int midPeriod = 20, int slowPeriod = 40,
            MAMethod maMethod = MAMethod.EMA, PriceSource priceSource = PriceSource.Close, int slopeLookback = 1,
            int buySignalModeIndex = 2, int sellSignalModeIndex = 2, int takeProfitExitModeIndex = 0, int stopLossExitModeIndex = 0,
            int flatModeIndex = 0, int skipModeIndex = 0, int signalModeIndex = -1, int firstEntryModeIndex = 0,
            double takeProfitTL = 0.0, int triggerPeriod = 5)
        {
            this.firstEntryModeIndex = firstEntryModeIndex;
            this.takeProfitTL        = takeProfitTL;
            this.triggerPeriod       = Math.Max(1, triggerPeriod);
            // signalModeIndex >= 0: buy ve sell ayni moda ayarlanir. Opt'ta modlari TEK range ile
            // taramak icin (buy/sell ayri taranirsa 36 asimetrik kombinasyon cikar).
            if (signalModeIndex >= 0)
            {
                buySignalModeIndex  = signalModeIndex;
                sellSignalModeIndex = signalModeIndex;
            }
            // midPeriod <= 0: fast ile slow'un geometrik ortalamasi (sadece mod 5'te kullanilir;
            // opt'ta ayrica taranip diger modlarin kosusunu katlamasin diye).
            if (midPeriod <= 0)
                midPeriod = Math.Max(fastPeriod + 1, (int)Math.Round(Math.Sqrt((double)fastPeriod * slowPeriod)));

            this.fastPeriod              = fastPeriod;
            this.midPeriod               = midPeriod;
            this.slowPeriod              = slowPeriod;
            this.maMethod                = maMethod;
            this.priceSource             = priceSource;
            this.slopeLookback           = Math.Max(1, slopeLookback);
            this.buySignalModeIndex      = buySignalModeIndex;
            this.sellSignalModeIndex     = sellSignalModeIndex;
            this.takeProfitExitModeIndex = takeProfitExitModeIndex;
            this.stopLossExitModeIndex   = stopLossExitModeIndex;
            this.flatModeIndex           = flatModeIndex;
            this.skipModeIndex           = skipModeIndex;

            // Faz 1: ham sinyal - cikis/flat/skip kapali. Istisna: takeProfitTL > 0 ise komisyon
            // sonrasi net TL kar al acik (takeProfitExitModeIndex 6). Zarar kes yok.
            takeProfitExitModeEnabled = takeProfitTL > 0.0;
            if (takeProfitTL > 0.0)
                this.takeProfitExitModeIndex = 6;
            stopLossExitModeEnabled   = false;
            flatModeEnabled           = false;
            skipModeEnabled           = false;

            // Saat/gun penceresi kapali (gun ici pencere gerekiyorsa trader'in timeFiltering'i kullanilir).
            startTime            = TimeOnly.ParseExact("10:05:00", "HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
            stopTime             = TimeOnly.ParseExact("16:45:00", "HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
            triggerTime          = TimeOnly.ParseExact("10:00:00", "HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
            startDay             = default;
            stopDay              = default;
            isTimeEnabled        = false;
            isDayEnabled         = false;
            isTriggerTimeEnabled = false;

            Parameters["FastPeriod"]              = fastPeriod;
            Parameters["MidPeriod"]               = midPeriod;
            Parameters["SlowPeriod"]              = slowPeriod;
            Parameters["MaMethod"]                = maMethod;
            Parameters["PriceSource"]             = priceSource;
            Parameters["SlopeLookback"]           = this.slopeLookback;
            Parameters["BuySignalModeIndex"]      = buySignalModeIndex;
            Parameters["SellSignalModeIndex"]     = sellSignalModeIndex;
            Parameters["TakeProfitExitModeIndex"] = takeProfitExitModeIndex;
            Parameters["StopLossExitModeIndex"]   = stopLossExitModeIndex;
            Parameters["FlatModeIndex"]           = flatModeIndex;
            Parameters["SkipModeIndex"]           = skipModeIndex;
            Parameters["SignalModeIndex"]         = signalModeIndex;
            Parameters["FirstEntryModeIndex"]     = firstEntryModeIndex;
            Parameters["TakeProfitTL"]            = takeProfitTL;
            Parameters["TriggerPeriod"]           = this.triggerPeriod;
            Parameters["TakeProfitExitModeIndex"] = this.takeProfitExitModeIndex;
            Parameters["BuyModeEnabled"]          = buyModeEnabled;
            Parameters["SellModeEnabled"]         = sellModeEnabled;
            Parameters["TakeProfitExitModeEnabled"] = takeProfitExitModeEnabled;
            Parameters["StopLossExitModeEnabled"]   = stopLossExitModeEnabled;
            Parameters["FlatModeEnabled"]         = flatModeEnabled;
            Parameters["SkipModeEnabled"]         = skipModeEnabled;

            Initialize(data, indicators);
        }

        public override void OnInit()
        {
            if (!IsInitialized)
                return;

            source = Indicators.Trend.ResolvePriceSource(priceSource);
            fastMA = Indicators.MA.Calculate(source, maMethod, fastPeriod);
            midMA  = Indicators.MA.Calculate(source, maMethod, midPeriod);
            slowMA = Indicators.MA.Calculate(source, maMethod, slowPeriod);
            triggerMA = Indicators.MA.Calculate(source, maMethod, triggerPeriod);

            if (fastMA.Length != barCount || midMA.Length != barCount ||
                slowMA.Length != barCount || source.Length != barCount || triggerMA.Length != barCount)
            {
                throw new InvalidOperationException(
                    $"Seri uzunluklari uyusmuyor (barCount={barCount}): fastMA={fastMA.Length}, " +
                    $"midMA={midMA.Length}, slowMA={slowMA.Length}, source={source.Length}");
            }
        }

        /// <summary>Bu barda verilen moda gore yon: +1 AL, -1 SAT, 0 sinyal yok.</summary>
        private int DirectionFor(int mode, int i)
        {
            double price = source![i];
            double fast  = fastMA![i];
            double mid   = midMA![i];
            double slow  = slowMA![i];
            if (double.IsNaN(price) || double.IsNaN(fast) || double.IsNaN(slow))
                return 0;

            switch (mode)
            {
                case 0: return Sign(price - fast);
                case 1: return Sign(price - slow);
                case 2: return Sign(fast - slow);
                case 3: return Sign(fast - fastMA[i - slopeLookback]);
                case 4: return Sign(slow - slowMA[i - slopeLookback]);
                case 5:
                    if (double.IsNaN(mid)) return 0;
                    if (fast > mid && mid > slow) return 1;
                    if (fast < mid && mid < slow) return -1;
                    return 0;
                default:
                    return 0;
            }
        }

        private static int Sign(double value) => value > 0.0 ? 1 : value < 0.0 ? -1 : 0;

        /// <summary>
        /// Pozisyonun gidis-donus (acilis + kapanis) komisyonu, TL. SingleTrader'in komisyon
        /// formuluyle ayni: bir bacak = KomisyonCarpan * KomisyonVarlikAdedSayisi(Micro).
        /// </summary>
        private double GidisDonusKomisyonFiyat()
        {
            if (Trader?.status == null)
                return 0.0;
            bool isMicroLot = Trader.initialTradeParams?.MicroLotSizeEnabled ?? false;
            double volume = isMicroLot
                ? Trader.status.KomisyonVarlikAdedSayisiMicro
                : Trader.status.KomisyonVarlikAdedSayisi;
            return 2.0 * Trader.status.KomisyonCarpan * volume;
        }

        public override TradeSignals OnStep(int currentIndex)
        {
            bool buy        = false;
            bool sell       = false;
            bool takeProfit = false;
            bool stopLoss   = false;
            bool flat       = false;
            bool skip       = false;

            ResolveRunContext(currentIndex);

            TimeOnly currentTime    = times![currentIndex];
            DateOnly currentDate    = dates![currentIndex];
            bool isWithinTimeWindow = !isTimeEnabled || (currentTime >= startTime && currentTime <= stopTime);
            bool isWithinDayWindow  = !isDayEnabled  || (currentDate >= startDay  && currentDate <= stopDay);

            // Isinma: en uzun MA + egim geriye bakisi dolmadan sinyal yok.
            int warmup = Math.Max(Math.Max(Math.Max(fastPeriod, midPeriod), slowPeriod), triggerPeriod) + slopeLookback;
            if (currentIndex < warmup)
                return TradeSignals.None;
            if (source == null || fastMA == null || midMA == null || slowMA == null)
                return TradeSignals.None;

            int buyDir  = DirectionFor(buySignalModeIndex,  currentIndex);
            int sellDir = DirectionFor(sellSignalModeIndex, currentIndex);

            if (firstEntryModeIndex == 1 && !isSonYonA && !isSonYonS)
            {
                // Pozisyon BOSKEN (gun basi, gun sonu kapanisi, kar al / zarar kes / flat sonrasi)
                // giris yalnizca KESISIM BARINDA yapilir: yon, ayni gunun onceki barina gore bu barda
                // degismis olmali. Daha once olmus bir kesisim sayilmaz (cikistan sonra eski
                // kesisimle hemen yeniden girilmez). Dunun son bari -> bugunun ilk bari arasindaki
                // degisim de sayilmaz (onu gap yaratir). Pozisyon aciksa state (alt/ust) gecerli.
                bool sameDay   = dates![currentIndex - 1] == currentDate;
                bool buyCross  = sameDay && buyDir  > 0 && DirectionFor(buySignalModeIndex,  currentIndex - 1) <= 0;
                bool sellCross = sameDay && sellDir < 0 && DirectionFor(sellSignalModeIndex, currentIndex - 1) >= 0;
                if (!buyCross)  buyDir  = 0;
                if (!sellCross) sellDir = 0;
            }
            else if (firstEntryModeIndex == 2 && !isSonYonA && !isSonYonS && triggerMA != null)
            {
                // Pozisyon BOSKEN: YON secilen moddan (uzun periyot, kalite), ZAMANLAMA kisa tetik
                // EMA'sindan (triggerPeriod, siklik). Giris yalnizca fiyatin tetik EMA'sini AYNI GUN
                // ICINDE ana yon dogrultusunda kestigi barda yapilir (gap sayilmaz). Pozisyon aciksa
                // ana yon (state) gecerli: ana yon donerse pozisyon cevrilir.
                bool sameDay  = dates![currentIndex - 1] == currentDate;
                int trigNow   = Sign(source[currentIndex]     - triggerMA[currentIndex]);
                int trigPrev  = Sign(source[currentIndex - 1] - triggerMA[currentIndex - 1]);
                bool trigUp   = sameDay && trigNow > 0 && trigPrev <= 0;
                bool trigDown = sameDay && trigNow < 0 && trigPrev >= 0;
                if (!(buyDir  > 0 && trigUp))   buyDir  = 0;
                if (!(sellDir < 0 && trigDown)) sellDir = 0;
            }

            if (buyModeEnabled && buyDir > 0)
                buy = true;
            if (sellModeEnabled && sellDir < 0)
                sell = true;
            // Asimetrik modlarda (buy/sell farkli mod) ikisi ayni barda true olabilir; o barda
            // yon belirsiz sayilir ve pozisyon korunur.
            if (buy && sell) { buy = false; sell = false; }

            if (takeProfitExitModeEnabled && Trader != null)
            {
                if      (takeProfitExitModeIndex == 0) takeProfit = Trader.karAlZararKes.SonFiyataGoreKarAlSeviyeHesaplaSeviyeli(currentIndex, 5, 50, 1000) != 0;
                else if (takeProfitExitModeIndex == 1) takeProfit = Trader.karAlZararKes.SonFiyataGoreKarAlYuzdeHesaplaSeviyeli(currentIndex, 2, 10, 0.01) != 0;
                else if (takeProfitExitModeIndex == 2) takeProfit = Trader.karAlZararKes.SonFiyataGoreKarAlSeviyeHesapla(currentIndex, 2000.0) != 0;
                else if (takeProfitExitModeIndex == 3) takeProfit = Trader.karAlZararKes.SonFiyataGoreKarAlYuzdeHesapla(currentIndex, 2.0) != 0;
                else if (takeProfitExitModeIndex == 4) takeProfit = Trader.karAlZararKes.KarZararFiyatSeviyesindenKarAlHesapla(currentIndex, 1000.0) != 0;
                else if (takeProfitExitModeIndex == 5) takeProfit = Trader.karAlZararKes.KarZararYuzdesindenKarAlHesapla(currentIndex, 3.0) != 0;
                else if (takeProfitExitModeIndex == 6)
                {
                    // 6: Komisyon SONRASI net TL kar al. KarZararFiyatList brut oldugu icin
                    // (SingleTrader: (anlikFiyat - sonFiyat) * varlikAdedSayisi) mevcut metoda esik
                    // olarak "net hedef + gidis-donus komisyon" verilir: 1000 TL net hedef, 40 TL
                    // komisyonla brut 1040 TL'de kapatir.
                    takeProfit = Trader.karAlZararKes.KarZararFiyatSeviyesindenKarAlHesapla(
                        currentIndex, takeProfitTL + GidisDonusKomisyonFiyat()) != 0;
                }
            }

            if (stopLossExitModeEnabled && Trader != null)
            {
                if      (stopLossExitModeIndex == 0) stopLoss = Trader.karAlZararKes.SonFiyataGoreZararKesSeviyeHesaplaSeviyeli(currentIndex, -1, -10, 1000) != 0;
                else if (stopLossExitModeIndex == 1) stopLoss = Trader.karAlZararKes.SonFiyataGoreZararKesYuzdeHesaplaSeviyeli(currentIndex, -2, -10, 0.01) != 0;
                else if (stopLossExitModeIndex == 2) stopLoss = Trader.karAlZararKes.SonFiyataGoreZararKesSeviyeHesapla(currentIndex, -1000.0) != 0;
                else if (stopLossExitModeIndex == 3) stopLoss = Trader.karAlZararKes.SonFiyataGoreZararKesYuzdeHesapla(currentIndex, -1.0) != 0;
                else if (stopLossExitModeIndex == 4) stopLoss = Trader.karAlZararKes.KarZararFiyatSeviyesindenZararKesHesapla(currentIndex, -500.0) != 0;
                else if (stopLossExitModeIndex == 5) stopLoss = Trader.karAlZararKes.KarZararYuzdesindenZararKesHesapla(currentIndex, -2.0) != 0;
            }

            if (flatModeEnabled && flatModeIndex == 0) flat = false;
            if (skipModeEnabled && skipModeIndex == 0) skip = false;

            // Sinyal gate'i (SimpleMAStrategy ile ayni gerekce): trader'in kapali flag'li sinyali
            // oncelik zincirinde alttaki gecerli sinyali ezmesin.
            if (Trader?.signals?.AlEnabled       == false) buy        = false;
            if (Trader?.signals?.SatEnabled      == false) sell       = false;
            if (Trader?.signals?.KarAlEnabled    == false) takeProfit = false;
            if (Trader?.signals?.ZararKesEnabled == false) stopLoss   = false;
            if (Trader?.signals?.FlatOlEnabled   == false) flat       = false;
            if (Trader?.signals?.PasGecEnabled   == false) skip       = false;

            if (isTimeEnabled && !isWithinTimeWindow) { buy = false; sell = false; flat = true; }
            if (isDayEnabled  && !isWithinDayWindow)  { buy = false; sell = false; flat = true; }

            if (skip)       return TradeSignals.Skip;
            if (flat)       return TradeSignals.Flat;
            if (takeProfit) return TradeSignals.TakeProfit;
            if (stopLoss)   return TradeSignals.StopLoss;
            if (buy)        return TradeSignals.Buy;
            if (sell)       return TradeSignals.Sell;
            return TradeSignals.None;
        }

        /// <summary>
        /// Opt'ta anlamsiz kombinasyonlari backtest'siz atlar: fast/slow kullanan modlarda
        /// (1, 2, 4, 5) fast &lt; slow; uc MA modunda (5) ayrica fast &lt; mid &lt; slow.
        /// </summary>
        public override bool IsValidParameterCombination()
        {
            bool usesStack = buySignalModeIndex == 5 || sellSignalModeIndex == 5;
            bool usesFastSlowPair = buySignalModeIndex == 2 || sellSignalModeIndex == 2 || usesStack;
            if (usesFastSlowPair && fastPeriod >= slowPeriod)
                return false;
            if (usesStack && !(fastPeriod < midPeriod && midPeriod < slowPeriod))
                return false;
            return true;
        }

        public override Dictionary<string, double[]>? GetPlotIndicators()
        {
            var indicators = new Dictionary<string, double[]>();
            bool usesStack = buySignalModeIndex == 5 || sellSignalModeIndex == 5;
            if (fastMA != null) indicators[$"Fast MA ({fastPeriod})"] = fastMA;
            if (usesStack && midMA != null) indicators[$"Mid MA ({midPeriod})"] = midMA;
            if (slowMA != null) indicators[$"Slow MA ({slowPeriod})"] = slowMA;
            return indicators.Count > 0 ? indicators : null;
        }
    }
}
