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
    /// SimpleComboStrategy'den TUREYEN Opt Denemesi #2 - MA1 > MA2 > MA3 siralamasi + RSI'de
    /// yukselen/dusen momentum, SimpleComboStrategy'nin ruleModeIndex==1 dalinin (MA5 > MA8 >
    /// MA13 + RSI momentum) periyotlari GERCEK constructor parametresi yapilmis hali. Bkz.
    /// SimpleComboStrategyRule001'in ve SimpleComboStrategy'nin sinif basi doc comment'lerindeki
    /// "VERSIYONLAMA KARARI".
    ///
    /// RSI momentum kosulu (rsi[i-3] vs rsi[i-1]) SimpleComboStrategy'deki orijinal kurala sadik
    /// kalinarak SABIT birakildi (lag=3 parametrelestirilmedi) - sadece periyotlar (ma1Period/
    /// ma2Period/ma3Period/rsiPeriod) taranabilir yapildi. NOT (2026-08-31): bu "sadik kalindi"
    /// iddiasi bir sure yanlisti - karsilastirma yonu SimpleComboStrategy'nin ruleModeIndex==1
    /// dalinin TERSIYDI, simdi duzeltildi (bkz. BuyLevel/SellLevel'daki yorum).
    ///
    /// ruleModeIndex burada YOK (bkz. SimpleComboStrategyRule001'deki ayni gerekce - tek kural sabit).
    ///
    /// signalModeIndex/exitModeIndex/flatModeIndex/skipModeIndex: SimpleMostStrategy/
    /// SimpleComboStrategy ile AYNI standart (2026-08-27 karari). signalModeIndex AKTIF
    /// (0: siralama/seviye, 1: kesisim - MA siralamasi+RSI momentum kosulunun TAMAMI icin), digerleri
    /// PLACEHOLDER.
    ///
    /// SimpleMostStrategy'deki gibi (Combo'nun aksine): burada sabit sayida seri (MA1/MA2/MA3/RSI)
    /// karsilastirildigi icin BuildSignals benzeri bir onceden-hesaplama gecisine gerek yok -
    /// OnStep icinde currentIndex ve currentIndex-1 icin dogrudan hesaplaniyor.
    /// </summary>
    public class SimpleComboStrategyRule002 : BaseStrategy
    {
        public override string Name => "Simple Combo Strategy Rule002 (MA1>MA2>MA3 + RSI)";

        private readonly int ma1Period;
        private readonly int ma2Period;
        private readonly int ma3Period;
        private readonly int rsiPeriod;
        private readonly int signalModeIndex; // 0: siralama (level) bazli, 1: kesisim (crossover) bazli (ZAMANLAMA)
        private readonly int exitModeIndex;   // takeProfit/stopLoss yöntemi (0-5, Trader.karAlZararKes üzerinden) - AKTIF
        private readonly int flatModeIndex;   // PLACEHOLDER - henuz okunmuyor, bkz. sinif basi doc comment
        private readonly int skipModeIndex;   // PLACEHOLDER - henuz okunmuyor, bkz. sinif basi doc comment
        private double[]? ma1;
        private double[]? ma2;
        private double[]? ma3;
        private double[]? rsi;

        public SimpleComboStrategyRule002(List<StockData> data, IndicatorManager indicators,
            int ma1Period = 5, int ma2Period = 8, int ma3Period = 13, int rsiPeriod = 14,
            int signalModeIndex = 0, int exitModeIndex = 0, int flatModeIndex = 0, int skipModeIndex = 0)
        {
            this.ma1Period       = ma1Period;
            this.ma2Period       = ma2Period;
            this.ma3Period       = ma3Period;
            this.rsiPeriod       = rsiPeriod;
            this.signalModeIndex = signalModeIndex;
            this.exitModeIndex   = exitModeIndex;
            this.flatModeIndex   = flatModeIndex;
            this.skipModeIndex   = skipModeIndex;

            Parameters["Ma1Period"]       = ma1Period;
            Parameters["Ma2Period"]       = ma2Period;
            Parameters["Ma3Period"]       = ma3Period;
            Parameters["RsiPeriod"]       = rsiPeriod;
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
            ma3 = Indicators.MA.SMA(closes, ma3Period);
            rsi = Indicators.Momentum.RSI(closes, rsiPeriod).Values;
        }

        // MA1>MA2>MA3 siralamasi + RSI[i-3] vs RSI[i-1] - SEVIYE kosulu, ZAMANLAMA OnStep'te
        // signalModeIndex'e gore uygulanir. RSI[i-3] > RSI[i-1] (RSI o aralikta DUSMUS, "sogumus
        // momentum" pullback filtresi) buy tarafinda, tersi (RSI[i-3] < RSI[i-1], RSI o aralikta
        // YUKSELMIS) sell tarafinda kullaniliyor - SimpleComboStrategy'nin ruleModeIndex==1
        // dalindaki rsiUp/rsiDown ile AYNI yon (2026-08-31'de duzeltildi - onceden ters yondeydi,
        // "orijinal kurala sadik" iddiasi dogru degildi).
        private bool BuyLevel(int i) => i >= 3 && Buyuk(i, ma1!, ma2!) && Buyuk(i, ma2!, ma3!) && rsi![i - 3] > rsi![i - 1];
        private bool SellLevel(int i) => i >= 3 && Kucuk(i, ma1!, ma2!) && Kucuk(i, ma2!, ma3!) && rsi![i - 3] < rsi![i - 1];

        public override TradeSignals OnStep(int currentIndex)
        {
            bool buy        = false;
            bool sell       = false;
            bool takeProfit = false;
            bool stopLoss   = false;
            bool flat       = false;
            bool skip       = false;
            // ************************************************************************************************************************

            // Ilk barlarda yeterli veri yok (RSI[-3] icin en az 3 bar oncesi lazim)
            if (currentIndex < Math.Max(Math.Max(ma1Period, ma2Period), Math.Max(ma3Period, rsiPeriod)) || currentIndex < 3)
                return TradeSignals.None;

            // OnInit henuz calismamissa sinyal uretme
            if (ma1 == null || ma2 == null || ma3 == null || rsi == null)
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

            if (ma3 != null && ma3.Length > 0)
                indicators["MA3"] = ma3;

            if (rsi != null && rsi.Length > 0)
                indicators["RSI"] = rsi;

            return indicators.Count > 0 ? indicators : null;
        }
    }
}
