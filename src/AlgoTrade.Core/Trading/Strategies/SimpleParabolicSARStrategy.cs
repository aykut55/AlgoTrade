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
    /// Parabolic SAR (Stop and Reverse) Stratejisi
    ///
    /// Parabolic SAR Mantığı:
    /// - Trend takip eden trailing stop indikatörü
    /// - Yükseliş trendinde: SAR fiyatın altında
    /// - Düşüş trendinde: SAR fiyatın üstünde
    ///
    /// Trading Logic (choice=0):
    /// - AL: Trend false'dan true'ya değişirse (SAR fiyatın altına geçer)
    /// - SAT: Trend true'dan false'a değişirse (SAR fiyatın üstüne geçer)
    ///
    /// Trading Logic (choice=1):
    /// - (İleride eklenecek alternatif sinyal mantığı)
    ///
    /// Parametreler:
    /// - step: Hızlanma faktörü adımı (varsayılan 0.02)
    /// - max: Maksimum hızlanma faktörü (varsayılan 0.2)
    /// - choice: Sinyal mantığı seçimi (varsayılan 0)
    /// </summary>
    public class SimpleParabolicSARStrategy : BaseStrategy
    {
        public override string Name => "Simple Parabolic SAR Strategy";

        private readonly double _step;
        private readonly double _max;
        private readonly int _choice;
        private ParabolicSARResult? _sarResult;

        public SimpleParabolicSARStrategy(double step = 0.02, double max = 0.2, int choice = 0)
        {
            _step = step;
            _max = max;
            _choice = choice;

            Parameters["Step"] = step;
            Parameters["Max"] = max;
            Parameters["Choice"] = choice;
        }

        public SimpleParabolicSARStrategy(List<StockData> data, IndicatorManager indicators, double step = 0.02, double max = 0.2, int choice = 0)
        {
            _step = step;
            _max = max;
            _choice = choice;

            Parameters["Step"] = step;
            Parameters["Max"] = max;
            Parameters["Choice"] = choice;

            // Initialize base strategy
            Initialize(data, indicators);
        }

        public override void OnInit()
        {
            if (!IsInitialized)
                return;

            _sarResult = Indicators.Trend.ParabolicSAR(_step, _max);

            //Log($"SimpleParabolicSARStrategy initialized: Step={_step}, Max={_max}");
        }

        public override TradeSignals OnStep(int currentIndex)
        {
            bool buy = false;
            bool sell = false;
            bool takeProfit = false;
            bool stopLoss = false;
            bool flat = false;
            bool skip = false;

            if (currentIndex < 2)
                return TradeSignals.None;

            if (_sarResult == null || _sarResult.SAR.Length == 0)
                return TradeSignals.None;

            bool currentTrend = _sarResult.Trend[currentIndex];
            bool prevTrend = _sarResult.Trend[currentIndex - 1];

            // ************************************************************************************************************************
            // choice: 0 = SAR trend change, 1 = (İleride eklenecek)
            if (_choice == 0)
            {
                // AL: Trend false'dan true'ya değişiyor (düşüşten yükselişe)
                if (!prevTrend && currentTrend)
                {
                    buy = true;
                }

                // SAT: Trend true'dan false'a değişiyor (yükselişten düşüşe)
                if (prevTrend && !currentTrend)
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

        public double[]? GetSAR() => _sarResult?.SAR;
        public bool[]? GetTrend() => _sarResult?.Trend;

        public override Dictionary<string, double[]>? GetPlotIndicators()
        {
            var indicators = new Dictionary<string, double[]>();

            var closes = Indicators.GetClosePrices();
            if (closes != null && closes.Length > 0)
                indicators["Close"] = closes;

            if (_sarResult?.SAR != null && _sarResult.SAR.Length > 0)
                indicators["SAR"] = _sarResult.SAR;

            return indicators.Count > 0 ? indicators : null;
        }
    }
}
