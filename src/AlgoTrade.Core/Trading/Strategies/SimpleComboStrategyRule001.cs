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
    /// SimpleComboStrategy'den TUREYEN Opt Denemesi #1 - MA1 x MA2 kesisimi (Golden/Death
    /// cross), SimpleComboStrategy'nin ruleModeIndex==0 dalinin (MA5 x MA8) periyotlari GERCEK
    /// constructor parametresi yapilmis hali. SimpleComboStrategy'nin sinif basi doc
    /// comment'indeki "VERSIYONLAMA KARARI"na gore acildi: ruleModeIndex/BuildSignals()
    /// yaklasimi periyot gibi surekli/sayisal parametreleri taramak icin uygun degil (optimizer
    /// sonuclari "hangi ruleModeIndex" bazinda anlamsiz kaliyor) - bu yuzden yeni bir kural
    /// denerken sinif elle degistirilmiyor, boyle turemis (SimpleComboStrategyRule001,
    /// SimpleComboStrategyRule002, ...) yeni bir sinif aciliyor; ma1Period/ma2Period burada
    /// dogrudan tarama icin SingleTraderOptimizer'a acik.
    ///
    /// ruleModeIndex burada YOK - SimpleComboStrategy'deki gibi birden fazla kural arasindan
    /// secim yapan bir eksen degil, bu sinif zaten TEK bir kurali (MA1 x MA2) sabitliyor; bos bir
    /// placeholder eklemek anlamsiz olurdu.
    ///
    /// signalModeIndex/exitModeIndex/flatModeIndex/skipModeIndex: SimpleMostStrategy/
    /// SimpleComboStrategy ile AYNI standart (2026-08-27 karari) - OnStep'in urettigi 4 sinyal
    /// kategorisine (buy/sell, takeProfit/stopLoss, flat, skip) birebir karsilik gelen dispatch
    /// parametreleri. signalModeIndex AKTIF (0: siralama/seviye, 1: kesisim), digerleri PLACEHOLDER.
    ///
    /// SimpleMostStrategy'deki gibi (Combo'nun aksine): burada sadece TEK bir sabit seri cifti
    /// (MA1 vs MA2) karsilastirildigi icin BuildSignals benzeri bir onceden-hesaplama gecisine
    /// gerek yok - OnStep icinde currentIndex ve currentIndex-1 icin dogrudan Buyuk/Kucuk cagriliyor.
    /// </summary>
    public class SimpleComboStrategyRule001 : BaseStrategy
    {
        public override string Name => "Simple Combo Strategy Rule001 (MA1 x MA2)";

        private readonly int ma1Period;
        private readonly int ma2Period;
        private readonly int signalModeIndex; // 0: siralama (level) bazli, 1: kesisim (crossover) bazli (ZAMANLAMA)
        private readonly int exitModeIndex;   // takeProfit/stopLoss yöntemi (0-5, Trader.karAlZararKes üzerinden) - AKTIF
        private readonly int flatModeIndex;   // PLACEHOLDER - henuz okunmuyor, bkz. sinif basi doc comment
        private readonly int skipModeIndex;   // PLACEHOLDER - henuz okunmuyor, bkz. sinif basi doc comment
        private double[]? ma1;
        private double[]? ma2;

        public SimpleComboStrategyRule001(List<StockData> data, IndicatorManager indicators,
            int ma1Period = 5, int ma2Period = 8,
            int signalModeIndex = 0, int exitModeIndex = 0, int flatModeIndex = 0, int skipModeIndex = 0)
        {
            this.ma1Period       = ma1Period;
            this.ma2Period       = ma2Period;
            this.signalModeIndex = signalModeIndex;
            this.exitModeIndex   = exitModeIndex;
            this.flatModeIndex   = flatModeIndex;
            this.skipModeIndex   = skipModeIndex;

            Parameters["Ma1Period"]       = ma1Period;
            Parameters["Ma2Period"]       = ma2Period;
            Parameters["SignalModeIndex"] = signalModeIndex;
            Parameters["ExitModeIndex"]   = exitModeIndex;
            Parameters["FlatModeIndex"]   = flatModeIndex;
            Parameters["SkipModeIndex"]   = skipModeIndex;

            Initialize(data, indicators);
        }

        public override void OnInit()
        {
            if (!IsInitialized)
                return;

            var closes = Indicators.GetClosePrices();
            ma1 = Indicators.MA.SMA(closes, ma1Period);
            ma2 = Indicators.MA.SMA(closes, ma2Period);
        }

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
            if (currentIndex < Math.Max(ma1Period, ma2Period) || currentIndex < 1)
                return TradeSignals.None;

            // OnInit henuz calismamissa sinyal uretme
            if (ma1 == null || ma2 == null)
                return TradeSignals.None;
            // ************************************************************************************************************************

            // MA1 x MA2 kesisimi - seviye (level) kosulu, ZAMANLAMA asagida signalModeIndex'e gore uygulanir
            bool buyLevel      = Buyuk(currentIndex, ma1, ma2);
            bool sellLevel     = Kucuk(currentIndex, ma1, ma2);
            bool prevBuyLevel  = Buyuk(currentIndex - 1, ma1, ma2);
            bool prevSellLevel = Kucuk(currentIndex - 1, ma1, ma2);

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

            if (ma1 != null && ma1.Length > 0)
                indicators["MA1"] = ma1;

            if (ma2 != null && ma2.Length > 0)
                indicators["MA2"] = ma2;

            return indicators.Count > 0 ? indicators : null;
        }
    }
}
