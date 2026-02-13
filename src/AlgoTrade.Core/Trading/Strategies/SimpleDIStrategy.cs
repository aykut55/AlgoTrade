using AlgoTrade.Core;
using AlgoTrade.Core.Trading.Indicators;
using AlgoTrade.Core.Trading.Indicators.Trend.Results;
using AlgoTrade.Core.Logging;
using AlgoTrade.Core.Trading.Core;
using AlgoTrade.Core.Trading.Strategy;
using System;
using System.Collections.Generic;

namespace AlgoTrade.Core.Trading.Strategies
{
    /// <summary>
    /// DI (Directional Indicators) Stratejisi
    ///
    /// DI Mantığı:
    /// - +DI: Yukarı yönlü hareket gücü
    /// - -DI: Aşağı yönlü hareket gücü
    /// - ADX filtresi olmadan sadece DI kesişimleri kullanılır
    ///
    /// Trading Logic (choice=0):
    /// - AL: +DI, -DI'yı yukarı kesiyor
    /// - SAT: -DI, +DI'yı yukarı kesiyor
    ///
    /// Trading Logic (choice=1):
    /// - (İleride eklenecek alternatif sinyal mantığı)
    ///
    /// Parametreler:
    /// - period: DI periyodu (varsayılan 14)
    /// - choice: Sinyal mantığı seçimi (varsayılan 0)
    /// </summary>
    public class SimpleDIStrategy : BaseStrategy
    {
        public override string Name => "Simple DI Strategy";

        private readonly int _period;
        private readonly int _choice;
        private ADXResult? _adxResult;

        public SimpleDIStrategy(int period = 14, int choice = 0)
        {
            _period = period;
            _choice = choice;

            Parameters["Period"] = period;
            Parameters["Choice"] = choice;
        }

        public SimpleDIStrategy(List<StockData> data, IndicatorManager indicators, int period = 14, int choice = 0)
        {
            _period = period;
            _choice = choice;

            Parameters["Period"] = period;
            Parameters["Choice"] = choice;

            // Initialize base strategy
            // Initialize(data, indicators);
        }

        public override void OnInit()
        {
            if (!IsInitialized)
                return;

            _adxResult = Indicators.Trend.ADXWithDI(_period);

            Log($"SimpleDIStrategy initialized: Period={_period}");
        }

        public override TradeSignals OnStep(int currentIndex)
        {
            bool buy = false;
            bool sell = false;
            bool takeProfit = false;
            bool stopLoss = false;
            bool flat = false;
            bool skip = false;

            if (currentIndex < _period + 1)
                return TradeSignals.None;

            if (_adxResult == null || _adxResult.PlusDI.Length == 0)
                return TradeSignals.None;

            double currentPlusDI = _adxResult.PlusDI[currentIndex];
            double prevPlusDI = _adxResult.PlusDI[currentIndex - 1];
            double currentMinusDI = _adxResult.MinusDI[currentIndex];
            double prevMinusDI = _adxResult.MinusDI[currentIndex - 1];

            if (double.IsNaN(currentPlusDI) || double.IsNaN(currentMinusDI) ||
                double.IsNaN(prevPlusDI) || double.IsNaN(prevMinusDI))
                return TradeSignals.None;

            // ************************************************************************************************************************
            // choice: 0 = DI crossover, 1 = (İleride eklenecek)
            if (_choice == 0)
            {
                // AL: +DI, -DI'yı yukarı kesiyor
                if (prevPlusDI <= prevMinusDI && currentPlusDI > currentMinusDI)
                {
                    buy = true;
                }

                // SAT: -DI, +DI'yı yukarı kesiyor
                if (prevMinusDI <= prevPlusDI && currentMinusDI > currentPlusDI)
                {
                    sell = true;
                }
            }
            else
            {
                // İleride eklenecek alternatif sinyal mantığı
            }
            // ************************************************************************************************************************

            if (Trader != null)
            {
                takeProfit = Trader.karAlZararKes.SonFiyataGoreKarAlSeviyeHesaplaSeviyeli(currentIndex, 5, 50, 1000) != 0;
            }

            if (Trader != null)
            {
                stopLoss = Trader.karAlZararKes.SonFiyataGoreZararKesSeviyeHesaplaSeviyeli(currentIndex, -1, -10, 1000) != 0;
            }

            if (skip) return TradeSignals.Skip;
            else if (flat) return TradeSignals.Flat;
            else if (takeProfit) return TradeSignals.TakeProfit;
            else if (stopLoss) return TradeSignals.StopLoss;
            else if (buy) return TradeSignals.Buy;
            else if (sell) return TradeSignals.Sell;

            return TradeSignals.None;
        }

        public double[]? GetPlusDI() => _adxResult?.PlusDI;
        public double[]? GetMinusDI() => _adxResult?.MinusDI;

        public override Dictionary<string, double[]>? GetPlotIndicators()
        {
            var indicators = new Dictionary<string, double[]>();

            if (_adxResult?.PlusDI != null && _adxResult.PlusDI.Length > 0)
                indicators["+DI"] = _adxResult.PlusDI;

            if (_adxResult?.MinusDI != null && _adxResult.MinusDI.Length > 0)
                indicators["-DI"] = _adxResult.MinusDI;

            return indicators.Count > 0 ? indicators : null;
        }
    }
}
