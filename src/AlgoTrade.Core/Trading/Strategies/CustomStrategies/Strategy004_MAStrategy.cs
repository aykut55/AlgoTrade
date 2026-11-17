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
    /// Strategy004_MAStrategy - 4 EMA dizilim stratejisi.
    ///
    /// Kurallar:
    /// - AL : close > ma1 > ma2 > ma3 > ma4   (kosul surdukce her bar - state)
    /// - SAT: close &lt; ma1 &lt; ma2 &lt; ma3 &lt; ma4
    /// - Ikisi de degilse sinyal yok: acik pozisyon ters dizilim olusana kadar tasinir.
    ///   Trader ayni yonde tekrar islem acmaz.
    /// - Filtreler (0 = kapali; gecmezse sinyal yok, pozisyon tasinir):
    ///   confirmBars    : dizilim ust uste en az N bar surmeli.
    ///   minCloseMa1Pct : close ile ma1 arasi en az %X olmali.
    ///   minSpreadPct   : ma1 ile ma4 arasi en az %X olmali (ortalamalar acilmis olmali).
    ///
    /// Saat penceresi, kar al, zarar kes, flat strateji icinde YOK; disaridan
    /// (01_RunSingleTraderWithProgressAsync.csx OnApplyUserFlags: TimeFilteringEnabled,
    /// KarAlEnabled, ...) yonetilir.
    /// </summary>
    public class Strategy004_MAStrategy : BaseStrategy
    {
        public override string Name => "Strategy004 (4 EMA Dizilim)";

        private readonly int ma1Period;
        private readonly int ma2Period;
        private readonly int ma3Period;
        private readonly int ma4Period;
        private readonly MAMethod maMethod;
        private readonly PriceSource priceSource;

        // Filtreler (0 = kapali). Filtre gecmezse sinyal yok -> acik pozisyon tasinir.
        private readonly int confirmBars;         // dizilim ust uste en az N bar surmeli
        private readonly double minCloseMa1Pct;   // |close - ma1| / ma1 * 100 >= bu deger
        private readonly double minSpreadPct;     // |ma1 - ma4| / ma4 * 100 >= bu deger

        private double[]? source;
        private double[]? ma1;
        private double[]? ma2;
        private double[]? ma3;
        private double[]? ma4;

        public Strategy004_MAStrategy(List<StockData> data, IndicatorManager indicators,
            int ma1Period = 21, int ma2Period = 50, int ma3Period = 100, int ma4Period = 200,
            MAMethod maMethod = MAMethod.EMA, PriceSource priceSource = PriceSource.Close,
            int confirmBars = 0, double minCloseMa1Pct = 0.0, double minSpreadPct = 0.0)
        {
            this.ma1Period      = ma1Period;
            this.ma2Period      = ma2Period;
            this.ma3Period      = ma3Period;
            this.ma4Period      = ma4Period;
            this.maMethod       = maMethod;
            this.priceSource    = priceSource;
            this.confirmBars    = Math.Max(0, confirmBars);
            this.minCloseMa1Pct = Math.Max(0.0, minCloseMa1Pct);
            this.minSpreadPct   = Math.Max(0.0, minSpreadPct);

            // Strateji sadece AL/SAT uretir; cikis/flat/skip/pencere disaridan yonetilir.
            takeProfitExitModeEnabled = false;
            stopLossExitModeEnabled   = false;
            flatModeEnabled           = false;
            skipModeEnabled           = false;
            isTimeEnabled             = false;
            isDayEnabled              = false;
            isTriggerTimeEnabled      = false;

            Parameters["Ma1Period"]   = ma1Period;
            Parameters["Ma2Period"]   = ma2Period;
            Parameters["Ma3Period"]   = ma3Period;
            Parameters["Ma4Period"]   = ma4Period;
            Parameters["MaMethod"]    = maMethod;
            Parameters["PriceSource"] = priceSource;
            Parameters["ConfirmBars"]    = this.confirmBars;
            Parameters["MinCloseMa1Pct"] = this.minCloseMa1Pct;
            Parameters["MinSpreadPct"]   = this.minSpreadPct;

            Initialize(data, indicators);
        }

        public override void OnInit()
        {
            if (!IsInitialized)
                return;

            source = Indicators.Trend.ResolvePriceSource(priceSource);
            ma1 = Indicators.MA.Calculate(source, maMethod, ma1Period);
            ma2 = Indicators.MA.Calculate(source, maMethod, ma2Period);
            ma3 = Indicators.MA.Calculate(source, maMethod, ma3Period);
            ma4 = Indicators.MA.Calculate(source, maMethod, ma4Period);

            if (source.Length != barCount || ma1.Length != barCount || ma2.Length != barCount ||
                ma3.Length != barCount || ma4.Length != barCount)
            {
                throw new InvalidOperationException(
                    $"Seri uzunluklari uyusmuyor (barCount={barCount}): source={source.Length}, " +
                    $"ma1={ma1.Length}, ma2={ma2.Length}, ma3={ma3.Length}, ma4={ma4.Length}");
            }
        }

        /// <summary>i barindaki dizilim: +1 (close > ma1 > ma2 > ma3 > ma4), -1 (tersi), 0 (yok).
        /// NaN (isinma donemi) karsilastirmalari false doner -> 0.</summary>
        private int AlignmentAt(int i)
        {
            double c = source![i];
            if (c > ma1![i] && ma1[i] > ma2![i] && ma2[i] > ma3![i] && ma3[i] > ma4![i]) return 1;
            if (c < ma1[i]  && ma1[i] < ma2[i]  && ma2[i] < ma3[i]  && ma3[i] < ma4[i])  return -1;
            return 0;
        }

        public override TradeSignals OnStep(int currentIndex)
        {
            ResolveRunContext(currentIndex);

            if (source == null || ma1 == null || ma2 == null || ma3 == null || ma4 == null)
                return TradeSignals.None;

            int i = currentIndex;
            int dir = AlignmentAt(i);

            // Filtre 1 - teyit: dizilim son confirmBars bar boyunca (bu bar dahil) kesintisiz surmeli.
            if (dir != 0 && confirmBars > 1)
            {
                if (i - confirmBars + 1 < 0)
                    dir = 0;
                else
                    for (int k = 1; k < confirmBars; k++)
                        if (AlignmentAt(i - k) != dir) { dir = 0; break; }
            }

            // Filtre 2 - mesafe: ortalamalar birbirine yapisikken (yatay piyasa) sinyal yok.
            if (dir != 0 && minCloseMa1Pct > 0.0 &&
                Math.Abs(source[i] - ma1[i]) / ma1[i] * 100.0 < minCloseMa1Pct)
                dir = 0;
            if (dir != 0 && minSpreadPct > 0.0 &&
                Math.Abs(ma1[i] - ma4[i]) / ma4[i] * 100.0 < minSpreadPct)
                dir = 0;

            bool buy  = dir > 0;
            bool sell = dir < 0;

            // Sinyal gate'i (diger stratejilerle ayni).
            if (Trader?.signals?.AlEnabled  == false) buy  = false;
            if (Trader?.signals?.SatEnabled == false) sell = false;

            if (buy)  return TradeSignals.Buy;
            if (sell) return TradeSignals.Sell;
            return TradeSignals.None;
        }

        public override Dictionary<string, double[]>? GetPlotIndicators()
        {
            var plots = new Dictionary<string, double[]>();
            if (ma1 != null) plots[$"MA1 ({ma1Period})"] = ma1;
            if (ma2 != null) plots[$"MA2 ({ma2Period})"] = ma2;
            if (ma3 != null) plots[$"MA3 ({ma3Period})"] = ma3;
            if (ma4 != null) plots[$"MA4 ({ma4Period})"] = ma4;
            return plots.Count > 0 ? plots : null;
        }
    }
}
