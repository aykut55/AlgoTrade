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
    /// ADX (Average Directional Index) Stratejisi
    ///
    /// ADX Mantığı:
    /// - ADX: Trend gücünü ölçer (yön değil)
    /// - +DI: Yukarı yönlü hareket
    /// - -DI: Aşağı yönlü hareket
    ///
    /// Trading Logic (choice=0):
    /// - AL: +DI, -DI'yı yukarı kesiyor VE ADX > 25
    /// - SAT: -DI, +DI'yı yukarı kesiyor VE ADX > 25
    ///
    /// Trading Logic (choice=1):
    /// - (İleride eklenecek alternatif sinyal mantığı)
    ///
    /// Parametreler:
    /// - period: ADX periyodu (varsayılan 14)
    /// - adxThreshold: Minimum ADX değeri (varsayılan 25)
    /// - choice: Sinyal mantığı seçimi (varsayılan 0)
    /// </summary>
    public class SimpleADXStrategy : BaseStrategy
    {
        public override string Name => "Simple ADX Strategy";

        private readonly int _period;
        private readonly double _adxThreshold;
        private readonly int _choice;
        private ADXResult? _adxResult;

        public SimpleADXStrategy(int period = 14, double adxThreshold = 25, int choice = 0)
        {
            _period = period;
            _adxThreshold = adxThreshold;
            _choice = choice;

            Parameters["Period"] = period;
            Parameters["ADXThreshold"] = adxThreshold;
            Parameters["Choice"] = choice;
        }

        public SimpleADXStrategy(List<StockData> data, IndicatorManager indicators, int period = 14, double adxThreshold = 25, int choice = 0)
        {
            _period = period;
            _adxThreshold = adxThreshold;
            _choice = choice;

            Parameters["Period"] = period;
            Parameters["ADXThreshold"] = adxThreshold;
            Parameters["Choice"] = choice;

            // Initialize base strategy
            Initialize(data, indicators);
        }

        public override void OnInit()
        {
            if (!IsInitialized)
                return;

            _adxResult = Indicators.Trend.ADXWithDI(_period);

            Log($"SimpleADXStrategy initialized: Period={_period}, ADXThreshold={_adxThreshold}");
        }

        public override TradeSignals OnStep(int currentIndex)
        {
            bool buy = false;
            bool sell = false;
            bool takeProfit = false;
            bool stopLoss = false;
            bool flat = false;
            bool skip = false;

            if (currentIndex < _period * 2 + 1)
                return TradeSignals.None;

            if (_adxResult == null || _adxResult.ADX.Length == 0)
                return TradeSignals.None;

            double currentADX = _adxResult.ADX[currentIndex];
            double currentPlusDI = _adxResult.PlusDI[currentIndex];
            double prevPlusDI = _adxResult.PlusDI[currentIndex - 1];
            double currentMinusDI = _adxResult.MinusDI[currentIndex];
            double prevMinusDI = _adxResult.MinusDI[currentIndex - 1];

            if (double.IsNaN(currentADX) || double.IsNaN(currentPlusDI) || double.IsNaN(currentMinusDI))
                return TradeSignals.None;

            // ************************************************************************************************************************
            // choice: 0 = DI crossover with ADX filter, 1 = (İleride eklenecek)
            if (_choice == 0)
            {
                // ADX trend gücü filtresi
                bool strongTrend = currentADX > _adxThreshold;

                // AL: +DI, -DI'yı yukarı kesiyor VE ADX > threshold
                if (prevPlusDI <= prevMinusDI && currentPlusDI > currentMinusDI && strongTrend)
                {
                    buy = true;
                }

                // SAT: -DI, +DI'yı yukarı kesiyor VE ADX > threshold
                if (prevMinusDI <= prevPlusDI && currentMinusDI > currentPlusDI && strongTrend)
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

        public double[]? GetADX() => _adxResult?.ADX;
        public double[]? GetPlusDI() => _adxResult?.PlusDI;
        public double[]? GetMinusDI() => _adxResult?.MinusDI;

        public override Dictionary<string, double[]>? GetPlotIndicators()
        {
            var indicators = new Dictionary<string, double[]>();

            if (_adxResult?.ADX != null && _adxResult.ADX.Length > 0)
                indicators["ADX"] = _adxResult.ADX;

            if (_adxResult?.PlusDI != null && _adxResult.PlusDI.Length > 0)
                indicators["+DI"] = _adxResult.PlusDI;

            if (_adxResult?.MinusDI != null && _adxResult.MinusDI.Length > 0)
                indicators["-DI"] = _adxResult.MinusDI;

            return indicators.Count > 0 ? indicators : null;
        }
    }
}
