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
    /// </summary>
    public class SimpleComboStrategyRule003 : BaseStrategy
    {
        public override string Name => "Simple Combo Strategy Rule003 (MACD + SuperTrend)";

        private readonly int macdFastPeriod;
        private readonly int macdSlowPeriod;
        private readonly int macdSignalPeriod;
        private readonly int superTrendPeriod;
        private readonly double superTrendMultiplier;
        private readonly int signalModeIndex; // 0: siralama (level) bazli, 1: kesisim (crossover) bazli (ZAMANLAMA)
        private readonly int exitModeIndex;   // PLACEHOLDER - henuz okunmuyor, bkz. sinif basi doc comment
        private readonly int flatModeIndex;   // PLACEHOLDER - henuz okunmuyor, bkz. sinif basi doc comment
        private readonly int skipModeIndex;   // PLACEHOLDER - henuz okunmuyor, bkz. sinif basi doc comment
        private double[]? macd;
        private double[]? macdSignal;
        private double[]? superTrend;
        private double[]? close;

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

            // ORNEK: Trader referansini kullanarak kar al / zarar kes hesaplama
            if (Trader != null)
            {
                if (exitModeIndex == 0)
                {
                    // Trader.flags.KarAlSeviyeHesaplaEnabled kapaliysa metod iceride 0 doner (takeProfit hep false kalir)
                    takeProfit = Trader.karAlZararKes.SonFiyataGoreKarAlSeviyeHesaplaSeviyeli(currentIndex, 5, 50, 1000) != 0;
                }
            }

            if (Trader != null)
            {
                if (exitModeIndex == 0)
                {
                    // Trader.flags.ZararKesSeviyeHesaplaEnabled kapaliysa metod iceride 0 doner (stopLoss hep false kalir)
                    stopLoss = Trader.karAlZararKes.SonFiyataGoreZararKesSeviyeHesaplaSeviyeli(currentIndex, -1, -10, 1000) != 0;
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
