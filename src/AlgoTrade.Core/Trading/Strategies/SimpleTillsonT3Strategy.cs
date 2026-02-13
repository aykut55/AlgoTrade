using AlgoTrade.Core;
using AlgoTrade.Core.Trading.Indicators;
using AlgoTrade.Core.Trading.Indicators.Base;
using AlgoTrade.Core.Logging;
using AlgoTrade.Core.Trading.Core;
using AlgoTrade.Core.Trading.Strategy;
using System;
using System.Collections.Generic;

namespace AlgoTrade.Core.Trading.Strategies
{
    /// <summary>
    /// Tillson T3 İndikatörü Stratejisi
    ///
    /// T3 Mantığı:
    /// - Triple exponential smoothing ile düşük gecikmeli MA
    /// - Tillson'un geliştirdiği özel ağırlıklandırma
    ///
    /// Trading Logic (choice=0):
    /// - AL: Fiyat T3'ü yukarı kesiyor
    /// - SAT: Fiyat T3'ü aşağı kesiyor
    ///
    /// Trading Logic (choice=1):
    /// - (İleride eklenecek alternatif sinyal mantığı)
    ///
    /// Parametreler:
    /// - period: T3 periyodu (varsayılan 5)
    /// - choice: Sinyal mantığı seçimi (varsayılan 0)
    /// </summary>
    public class SimpleTillsonT3Strategy : BaseStrategy
    {
        public override string Name => "Simple Tillson T3 Strategy";

        private readonly int _period;
        private readonly int _choice;
        private double[]? _t3;

        public SimpleTillsonT3Strategy(int period = 5, int choice = 0)
        {
            _period = period;
            _choice = choice;

            Parameters["Period"] = period;
            Parameters["Choice"] = choice;
        }

        public SimpleTillsonT3Strategy(List<StockData> data, IndicatorManager indicators, int period = 5, int choice = 0)
        {
            _period = period;
            _choice = choice;

            Parameters["Period"] = period;
            Parameters["Choice"] = choice;

            Initialize(data, indicators);
        }

        public override void OnInit()
        {
            if (!IsInitialized)
                return;

            var closes = Indicators.GetClosePrices();
            _t3 = Indicators.MA.T3(closes, _period);

            LogManager.Log($"SimpleTillsonT3Strategy initialized: Period={_period}");
        }

        public override TradeSignals OnStep(int currentIndex)
        {
            bool buy = false;
            bool sell = false;
            bool takeProfit = false;
            bool stopLoss = false;
            bool flat = false;
            bool skip = false;

            if (currentIndex < _period * 6 + 1) // T3 requires 6x EMA
                return TradeSignals.None;

            if (_t3 == null || _t3.Length == 0)
                return TradeSignals.None;

            double currentPrice = Data[currentIndex].Close;
            double prevPrice = Data[currentIndex - 1].Close;
            double currentT3 = _t3[currentIndex];
            double prevT3 = _t3[currentIndex - 1];

            if (double.IsNaN(currentT3) || double.IsNaN(prevT3))
                return TradeSignals.None;

            // ************************************************************************************************************************
            // choice: 0 = Price-T3 crossover, 1 = (İleride eklenecek)
            if (_choice == 0)
            {
                // AL: Fiyat T3'ü yukarı kesiyor
                if (prevPrice <= prevT3 && currentPrice > currentT3)
                {
                    buy = true;
                }

                // SAT: Fiyat T3'ü aşağı kesiyor
                if (prevPrice >= prevT3 && currentPrice < currentT3)
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

        public double[]? GetT3() => _t3;

        public override Dictionary<string, double[]>? GetPlotIndicators()
        {
            var indicators = new Dictionary<string, double[]>();

            var closes = Indicators.GetClosePrices();
            if (closes != null && closes.Length > 0)
                indicators["Close"] = closes;

            if (_t3 != null && _t3.Length > 0)
                indicators["T3"] = _t3;

            return indicators.Count > 0 ? indicators : null;
        }
    }
}
