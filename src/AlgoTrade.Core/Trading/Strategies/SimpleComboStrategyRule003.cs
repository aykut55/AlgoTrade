using AlgoTrade.Core;
using AlgoTrade.Core.Trading.Indicators;
using AlgoTrade.Core.Trading.Core;
using AlgoTrade.Core.Trading.Strategy;
using static AlgoTrade.Core.Trading.Utils.Utils;
using System;
using System.Collections.Generic;

namespace AlgoTrade.Core.Trading.Strategies
{
    /// <summary>
    /// SimpleComboStrategy'den TUREYEN Opt Denemesi #3 - MACD sinyal cizgisini kesiyor +
    /// SuperTrend fiyatin altinda/ustunde, SimpleComboStrategy'nin ruleModeIndex==2 dalinin
    /// periyotlari GERCEK constructor parametresi yapilmis hali. Bkz. SimpleComboStrategyRule001'in
    /// ve SimpleComboStrategy'nin sinif basi doc comment'lerindeki "VERSIYONLAMA KARARI".
    ///
    /// ruleModeIndex burada YOK (bkz. SimpleComboStrategyRule001'deki ayni gerekce - tek kural sabit).
    ///
    /// signalModeIndex/exitModeIndex/flatModeIndex/skipModeIndex: SimpleMostStrategy/
    /// SimpleComboStrategy ile AYNI standart (2026-08-27 karari). signalModeIndex AKTIF
    /// (0: siralama/seviye, 1: kesisim - MACD+SuperTrend kosulunun TAMAMI icin), digerleri
    /// PLACEHOLDER.
    ///
    /// SimpleMostStrategy'deki gibi (Combo'nun aksine): burada sabit sayida seri (MACD/
    /// MACDSignal/SuperTrend/Close) karsilastirildigi icin BuildSignals benzeri bir
    /// onceden-hesaplama gecisine gerek yok - OnStep icinde currentIndex ve currentIndex-1 icin
    /// dogrudan hesaplaniyor.
    ///
    /// Sinyal gate'i (OnStep sonu, oncelik zincirinden hemen once):
    /// 6 sinyal (buy/sell/takeProfit/stopLoss/flat/skip) once strateji tarafindan hicbir trader
    /// flag'ine bakilmadan uretilir; sonra ilgili enable flag'i ACIKCA false olan (Trader.signals.
    /// AlEnabled/SatEnabled/KarAlEnabled/ZararKesEnabled/FlatOlEnabled/PasGecEnabled) sinyaller
    /// sifirlanir. Amac: ust-oncelikli ama uygulanmayacak bir sinyal (or. KarAlEnabled=false iken
    /// takeProfit) oncelik zincirinde alttaki gecerli sinyali (or. sell) sessizce ezmesin -
    /// zincir tek TradeSignals dondurdugu icin ezilen sinyal trader'a hic ulasmaz.
    /// MapStrategyCommandsToTradeCommands() ayni flag'leri ayrica kontrol eder (defensif ikinci katman).
    /// </summary>
    public class SimpleComboStrategyRule003 : BaseStrategy
    {
        public override string Name => "Simple Combo Strategy Rule003 (MACD + SuperTrend)";

        private readonly int macdFastPeriod;
        private readonly int macdSlowPeriod;
        private readonly int macdSignalPeriod;
        private readonly int superTrendPeriod;
        private readonly double superTrendMultiplier;

        // signalModeIndex/exitModeIndex/flatModeIndex/skipModeIndex artik BaseStrategy'de tanimli
        // (protected, readonly degil) - degerleri asagida constructor'da parametre olarak atanir.
        private double[]? macd;
        private double[]? macdSignal;
        private double[]? superTrend;
        private double[]? close;

        // runContextResolved/timeframeMinutes/isOptimizationRun/ResolveRunContext() artik
        // BaseStrategy'de (protected) - burada tekrar tanimlanmaz.


        public SimpleComboStrategyRule003(List<StockData> data, IndicatorManager indicators,
            int macdFastPeriod = 12, int macdSlowPeriod = 26, int macdSignalPeriod = 9,
            int superTrendPeriod = 10, double superTrendMultiplier = 3.0,
            int signalModeIndex = 0, int exitModeIndex = 0, int flatModeIndex = 0, int skipModeIndex = 0)
        {
            this.macdFastPeriod       = macdFastPeriod;
            this.macdSlowPeriod       = macdSlowPeriod;
            this.macdSignalPeriod     = macdSignalPeriod;
            this.superTrendPeriod     = superTrendPeriod;
            this.superTrendMultiplier = superTrendMultiplier;
            this.signalModeIndex      = signalModeIndex;
            this.exitModeIndex        = exitModeIndex;
            this.flatModeIndex        = flatModeIndex;
            this.skipModeIndex        = skipModeIndex;

            Parameters["MacdFastPeriod"]       = macdFastPeriod;
            Parameters["MacdSlowPeriod"]       = macdSlowPeriod;
            Parameters["MacdSignalPeriod"]     = macdSignalPeriod;
            Parameters["SuperTrendPeriod"]     = superTrendPeriod;
            Parameters["SuperTrendMultiplier"] = superTrendMultiplier;
            Parameters["SignalModeIndex"]      = signalModeIndex;
            Parameters["ExitModeIndex"]        = exitModeIndex;
            Parameters["FlatModeIndex"]        = flatModeIndex;
            Parameters["SkipModeIndex"]        = skipModeIndex;

            Initialize(data, indicators);
        }

        public override void OnInit()
        {
            if (!IsInitialized)
                return;

            close = Indicators.GetClosePrices();

            var macdResult = Indicators.Momentum.MACD(close, macdFastPeriod, macdSlowPeriod, macdSignalPeriod);
            macd       = macdResult.MACD;
            macdSignal = macdResult.Signal;

            superTrend = Indicators.Trend.SuperTrend(superTrendPeriod, superTrendMultiplier).SuperTrend;
        }

        // MACD sinyal cizgisini kesiyor + SuperTrend fiyatin altinda/ustunde - SEVIYE kosulu,
        // ZAMANLAMA OnStep'te signalModeIndex'e gore uygulanir.
        private bool BuyLevel(int i)  => Buyuk(i, macd!, macdSignal!) && Kucuk(i, superTrend!, close!);
        private bool SellLevel(int i) => Kucuk(i, macd!, macdSignal!) && Buyuk(i, superTrend!, close!);

        public override TradeSignals OnStep(int currentIndex)
        {
            ResolveRunContext(currentIndex);

            bool buy        = false;
            bool sell       = false;
            bool takeProfit = false;
            bool stopLoss   = false;
            bool flat       = false;
            bool skip       = false;
            // ************************************************************************************************************************

            // Ilk barlarda yeterli veri yok
            if (currentIndex < Math.Max(macdSlowPeriod + macdSignalPeriod, superTrendPeriod) || currentIndex < 1)
                return TradeSignals.None;

            // OnInit henuz calismamissa sinyal uretme
            if (macd == null || macdSignal == null || superTrend == null || close == null)
                return TradeSignals.None;
            // ************************************************************************************************************************

            bool buyLevel      = BuyLevel(currentIndex);
            bool sellLevel     = SellLevel(currentIndex);
            bool prevBuyLevel  = BuyLevel(currentIndex - 1);
            bool prevSellLevel = SellLevel(currentIndex - 1);

            if (signalModeIndex == 0)
            {
                // Siralama (level) bazli: kosul true oldugu her barda tekrarlanir
                buy  = buyLevel;
                sell = sellLevel;
            }
            else
            {
                // Kesisim (crossover) bazli: sadece false->true gecis aninda (bir kere) sinyal
                buy  = buyLevel && !prevBuyLevel;
                sell = sellLevel && !prevSellLevel;
            }
            // ************************************************************************************************************************

            if (Trader != null)
            {
                if (exitModeIndex == 0)
                {
                    // 0: Seviye, seviyeli
                    takeProfit = Trader.karAlZararKes.SonFiyataGoreKarAlSeviyeHesaplaSeviyeli(currentIndex, 5, 50, 1000) != 0;
                }
                else if (exitModeIndex == 1)
                {
                    // 1: Yüzde, seviyeli
                    takeProfit = Trader.karAlZararKes.SonFiyataGoreKarAlYuzdeHesaplaSeviyeli(currentIndex, 2, 10, 0.01) != 0;
                }
                else if (exitModeIndex == 2)
                {
                    // 2: Seviye, tek seviye
                    takeProfit = Trader.karAlZararKes.SonFiyataGoreKarAlSeviyeHesapla(currentIndex, 2000.0) != 0;
                }
                else if (exitModeIndex == 3)
                {
                    // 3: Yüzde, tek seviye
                    takeProfit = Trader.karAlZararKes.SonFiyataGoreKarAlYuzdeHesapla(currentIndex, 2.0) != 0;
                }
                else if (exitModeIndex == 4)
                {
                    // 4: Anlık kar/zarar fiyat seviyesi (pozisyon bazlı)
                    takeProfit = Trader.karAlZararKes.KarZararFiyatSeviyesindenKarAlHesapla(currentIndex, 1000.0) != 0;
                }
                else if (exitModeIndex == 5)
                {
                    // 5: Anlık kar/zarar yüzdesi (pozisyon bazlı)
                    takeProfit = Trader.karAlZararKes.KarZararYuzdesindenKarAlHesapla(currentIndex, 3.0) != 0;
                }
            }

            if (Trader != null)
            {
                if (exitModeIndex == 0)
                {
                    // 0: Seviye, seviyeli
                    stopLoss = Trader.karAlZararKes.SonFiyataGoreZararKesSeviyeHesaplaSeviyeli(currentIndex, -1, -10, 1000) != 0;
                }
                else if (exitModeIndex == 1)
                {
                    // 1: Yüzde, seviyeli
                    stopLoss = Trader.karAlZararKes.SonFiyataGoreZararKesYuzdeHesaplaSeviyeli(currentIndex, -2, -10, 0.01) != 0;
                }
                else if (exitModeIndex == 2)
                {
                    // 2: Seviye, tek seviye
                    stopLoss = Trader.karAlZararKes.SonFiyataGoreZararKesSeviyeHesapla(currentIndex, -1000.0) != 0;
                }
                else if (exitModeIndex == 3)
                {
                    // 3: Yüzde, tek seviye
                    stopLoss = Trader.karAlZararKes.SonFiyataGoreZararKesYuzdeHesapla(currentIndex, -1.0) != 0;
                }
                else if (exitModeIndex == 4)
                {
                    // 4: Anlık kar/zarar fiyat seviyesi (pozisyon bazlı)
                    stopLoss = Trader.karAlZararKes.KarZararFiyatSeviyesindenZararKesHesapla(currentIndex, -500.0) != 0;
                }
                else if (exitModeIndex == 5)
                {
                    // 5: Anlık kar/zarar yüzdesi (pozisyon bazlı)
                    stopLoss = Trader.karAlZararKes.KarZararYuzdesindenZararKesHesapla(currentIndex, -2.0) != 0;
                }
            }
            // ************************************************************************************************************************

            if (flatModeIndex == 0)
            {
                // Flat olma durumu burada incelenir ve flat flag'i setlenir
                flat = false;
            }
            // ************************************************************************************************************************

            if (skipModeIndex == 0)
            {
                // Skip olma durumu burada incelenir ve skip flag'i setlenir
                skip = false;
            }
            // ************************************************************************************************************************

            // ************************************************************************************************************************
            // ************************************************************************************************************************
            // ************************************************************************************************************************
            // Sinyal onceliklendirmesi
            // ************************************************************************************************************************
            // ************************************************************************************************************************
            // ************************************************************************************************************************
            // ------------------------------------------------------------------------------------------------------------------
            // SINYAL GATE'I - nihai önceliklendirmeden hemen ÖNCE.
            // Yukarıda 6 sinyal (buy/sell/takeProfit/stopLoss/flat/skip) strateji tarafından hiçbir trader
            // flag'ine bakılmadan, tamamen kendi mantığıyla üretildi. Burada, öncelik zincirine girmeden,
            // ilgili enable flag'i AÇIKÇA false olanları sıfırlıyoruz.
            //
            // Neden burada (zincirden önce): OnStep tek bir TradeSignals döndürür ve aşağıdaki öncelik
            // zinciri (skip > flat > TP > SL > buy > sell) ilk true olanı döndürüp gerisini atar. Üst
            // öncelikli ama trader'ın uygulamayacağı bir sinyal (ör. KarAlEnabled=false iken takeProfit)
            // true kalırsa, zincir TakeProfit döndürür; MapStrategyCommandsToTradeCommands() onu flag
            // kapalı diye düşürür - ve o barın ALTTAKİ geçerli sinyali (ör. Sell) hiç döndürülmediği için
            // SESSİZCE KAYBOLUR. Güçlü trendde bu pozisyonu kilitler (poz kâra geçince her bar
            // takeProfit=true, her death-cross yutulur, poz dönmez). Gate zincirden önce olunca kapalı
            // sinyal daha zincire girmeden elenir, zincir bir sonraki geçerli sinyale düşer.
            //
            // Neden "== false" (,"!= true" değil): Trader/signals null ise (strateji trader'sız çalışırsa,
            // ör. birim test) "== false" -> false, sinyal sıfırlanmaz; strateji ham üretici gibi davranır.
            //
            // MapStrategyCommandsToTradeCommands() aynı flag'leri bir kez daha kontrol eder (çift katman)
            // ama tek başına shadowing'i çözemez - asıl düzeltme buradaki gate. Trader.flags.
            // KarAlSeviyeHesaplaEnabled de korumaz: ilk trade'den sonra koşulsuz true olur, niyeti yansıtmaz.
            // ------------------------------------------------------------------------------------------------------------------
            if (Trader?.signals?.AlEnabled      == false) buy        = false;
            if (Trader?.signals?.SatEnabled     == false) sell       = false;
            if (Trader?.signals?.KarAlEnabled   == false) takeProfit = false;
            if (Trader?.signals?.ZararKesEnabled== false) stopLoss   = false;
            if (Trader?.signals?.FlatOlEnabled  == false) flat       = false;
            if (Trader?.signals?.PasGecEnabled  == false) skip       = false;

            if (skip)
            {
                return TradeSignals.Skip;
            }
            else if (flat)
            {
                return TradeSignals.Flat;
            }
            else if (takeProfit)
            {
                return TradeSignals.TakeProfit;
            }
            else if (stopLoss)
            {
                return TradeSignals.StopLoss;
            }
            else if (buy)
            {
                return TradeSignals.Buy;
            }
            else if (sell)
            {
                return TradeSignals.Sell;
            }
            // ************************************************************************************************************************
            // ************************************************************************************************************************
            // ************************************************************************************************************************

            return TradeSignals.None;
        }

        // ResolveRunContext() artik BaseStrategy'de (protected) - burada tekrar tanimlanmaz.

        public override Dictionary<string, double[]>? GetPlotIndicators()
        {
            var indicators = new Dictionary<string, double[]>();

            if (macd != null && macd.Length > 0)
                indicators["MACD"] = macd;

            if (macdSignal != null && macdSignal.Length > 0)
                indicators["MACDSignal"] = macdSignal;

            if (superTrend != null && superTrend.Length > 0)
                indicators["SuperTrend"] = superTrend;

            return indicators.Count > 0 ? indicators : null;
        }
    }
}
