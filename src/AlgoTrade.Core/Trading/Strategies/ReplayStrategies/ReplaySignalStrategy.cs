using AlgoTrade.Core;
using AlgoTrade.Core.Trading.Core;
using AlgoTrade.Core.Trading.Indicators;
using AlgoTrade.Core.Trading.Strategy;
using System;
using System.Collections.Generic;

namespace AlgoTrade.Core.Trading.Strategies.ReplayStrategies
{
    /// <summary>
    /// Sinyal uretmez - disaridan (ornegin Python plotter'da filtrelenmis/duzenlenmis
    /// veya yeniden hesaplanmis) hazir bir TradeSignals dizisini bar bar geri oynatir.
    ///
    /// Amac: equity/bakiye/trade istatistikleri gibi turetilmis veriler HER ZAMAN
    /// gercek SingleTrader/MultipleTrader execution motorundan gelsin (komisyon,
    /// pozisyon state-machine, kar-al/zarar-kes akisi aynen calisir) - sinyal
    /// uretimi disarida degismis olsa bile "tek dogru kaynak" execution tarafinda
    /// kalir, ayrica bir python/manuel equity hesaplamasi YAZILMAZ.
    ///
    /// signals dizisinin kaynagi onemli degil: orijinal stratejinin sinyallerinin
    /// filtrelenmis hali de olabilir, tamamen baska bir mantiktan (orn. export edilen
    /// indikator serilerinden Python'da turetilmis) yeni bir sinyal dizisi de olabilir -
    /// bu strateji sadece verilen diziyi index'e gore dondurur.
    /// </summary>
    public class ReplaySignalStrategy : BaseStrategy
    {
        public override string Name => "Replay Signal Strategy";

        private readonly TradeSignals[] signals;
        private readonly Func<SingleTrader, int, TradeSignals>? exitRule;

        public ReplaySignalStrategy(List<StockData> data, IndicatorManager indicators, TradeSignals[] signals)
            : this(data, indicators, signals, null)
        {
        }

        public ReplaySignalStrategy(List<StockData> data, IndicatorManager indicators, TradeSignals[] signals,
            Func<SingleTrader, int, TradeSignals>? exitRule)
        {
            if (signals is null)
                throw new ArgumentNullException(nameof(signals));

            this.signals = signals;
            this.exitRule = exitRule;

            Parameters["SignalCount"] = signals.Length;

            // The optional exit rule is supplied by the caller.
            takeProfitExitModeEnabled = false;
            stopLossExitModeEnabled   = false;
            flatModeEnabled           = false;
            skipModeEnabled           = false;

            // Initialize base strategy - barCount/openPrices/... burada dolar (LoadCommonSeries).
            Initialize(data, indicators);
        }

        public override void OnInit()
        {
            if (!IsInitialized)
                return;

            if (signals.Length != barCount)
            {
                throw new InvalidOperationException(
                    $"ReplaySignalStrategy: sinyal dizisi uzunlugu ({signals.Length}) " +
                    $"barCount ({barCount}) ile uyusmuyor.");
            }
        }

        public override TradeSignals OnStep(int currentIndex)
        {
            if (currentIndex < 0 || currentIndex >= signals.Length)
                return TradeSignals.None;

            TradeSignals signal = signals[currentIndex];

            // Sinyal gate'i - diger stratejilerle AYNI kural (bkz. SimpleMAStrategy):
            // Trader.signals.*Enabled ACIKCA false ise disaridan gelen sinyal de
            // sessizce elenir, ust kademedeki enable/disable davranisindan replay de ayrik kalmasin.
            if (signal == TradeSignals.Buy        && Trader?.signals?.AlEnabled       == false) signal = TradeSignals.None;
            if (signal == TradeSignals.Sell       && Trader?.signals?.SatEnabled      == false) signal = TradeSignals.None;
            if (signal == TradeSignals.TakeProfit && Trader?.signals?.KarAlEnabled    == false) return TradeSignals.None;
            if (signal == TradeSignals.StopLoss   && Trader?.signals?.ZararKesEnabled == false) return TradeSignals.None;
            if (signal == TradeSignals.Flat       && Trader?.signals?.FlatOlEnabled   == false) return TradeSignals.None;
            if (signal == TradeSignals.Skip       && Trader?.signals?.PasGecEnabled   == false) return TradeSignals.None;

            if (exitRule != null && Trader != null &&
                (signal == TradeSignals.None || signal == TradeSignals.Buy || signal == TradeSignals.Sell))
            {
                TradeSignals exit = exitRule(Trader, currentIndex);
                if (exit == TradeSignals.TakeProfit && Trader.signals.KarAlEnabled)
                    return exit;
                if (exit == TradeSignals.StopLoss && Trader.signals.ZararKesEnabled)
                    return exit;
            }

            return signal;
        }

        public override Dictionary<string, double[]>? GetPlotIndicators() => null;
    }
}
