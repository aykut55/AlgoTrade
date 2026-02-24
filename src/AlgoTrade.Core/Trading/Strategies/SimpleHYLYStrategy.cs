using AlgoTrade.Core;
using AlgoTrade.Core.Trading.Indicators;
using AlgoTrade.Core.Logging;
using AlgoTrade.Core.Trading.Core;
using AlgoTrade.Core.Trading.Strategy;
using System;
using System.Collections.Generic;

namespace AlgoTrade.Core.Trading.Strategies
{
    /// <summary>
    /// HY/LY (High-Yüksek / Low-Yüksek) Relatif Mesafe Stratejisi
    ///
    /// HY/LY Mantığı:
    /// - Fiyatın HHV ve LLV'ye göre göreceli mesafesini hesaplar
    /// - HY = (Close - LLV) / (HHV - LLV) * 100
    /// - LY = (HHV - Close) / (HHV - LLV) * 100
    ///
    /// Trading Logic (choice=0):
    /// - AL: HY belirli bir eşiği yukarı geçerse (örn: 80)
    /// - SAT: LY belirli bir eşiği yukarı geçerse (örn: 80)
    ///
    /// Trading Logic (choice=1):
    /// - (İleride eklenecek alternatif sinyal mantığı)
    ///
    /// Parametreler:
    /// - period: HHV/LLV lookback periyodu (varsayılan 20)
    /// - threshold: Sinyal eşiği (varsayılan 80)
    /// - choice: Sinyal mantığı seçimi (varsayılan 0)
    /// </summary>
    public class SimpleHYLYStrategy : BaseStrategy
    {
        public override string Name => "Simple HY/LY Strategy";

        private readonly int _period;
        private readonly double _threshold;
        private readonly int _choice;
        private double[]? _hy;
        private double[]? _ly;

        public SimpleHYLYStrategy(int period = 20, double threshold = 80, int choice = 0)
        {
            _period = period;
            _threshold = threshold;
            _choice = choice;

            Parameters["Period"] = period;
            Parameters["Threshold"] = threshold;
            Parameters["Choice"] = choice;
        }

        public SimpleHYLYStrategy(List<StockData> data, IndicatorManager indicators, int period = 20, double threshold = 80, int choice = 0)
        {
            _period = period;
            _threshold = threshold;
            _choice = choice;

            Parameters["Period"] = period;
            Parameters["Threshold"] = threshold;
            Parameters["Choice"] = choice;

            // Initialize base strategy
            Initialize(data, indicators);
        }

        public override void OnInit()
        {
            if (!IsInitialized)
                return;

            var highs = Indicators.GetHighPrices();
            var lows = Indicators.GetLowPrices();
            var closes = Indicators.GetClosePrices();

            var hhv = Indicators.Utils.HHV(highs, _period);
            var llv = Indicators.Utils.LLV(lows, _period);

            int length = closes.Length;
            _hy = new double[length];
            _ly = new double[length];

            for (int i = 0; i < length; i++)
            {
                double range = hhv[i] - llv[i];
                if (double.IsNaN(hhv[i]) || double.IsNaN(llv[i]) || range <= 0)
                {
                    _hy[i] = double.NaN;
                    _ly[i] = double.NaN;
                }
                else
                {
                    _hy[i] = ((closes[i] - llv[i]) / range) * 100;
                    _ly[i] = ((hhv[i] - closes[i]) / range) * 100;
                }
            }

            //Log($"SimpleHYLYStrategy initialized: Period={_period}, Threshold={_threshold}");
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

            if (_hy == null || _ly == null || _hy.Length == 0 || _ly.Length == 0)
                return TradeSignals.None;

            double currentHY = _hy[currentIndex];
            double prevHY = _hy[currentIndex - 1];
            double currentLY = _ly[currentIndex];
            double prevLY = _ly[currentIndex - 1];

            if (double.IsNaN(currentHY) || double.IsNaN(prevHY) || double.IsNaN(currentLY) || double.IsNaN(prevLY))
                return TradeSignals.None;

            // ************************************************************************************************************************
            // choice: 0 = HY/LY threshold crossover, 1 = (İleride eklenecek)
            if (_choice == 0)
            {
                // AL: HY threshold'u yukarı kesiyor (fiyat yükseğe yaklaşıyor)
                if (prevHY <= _threshold && currentHY > _threshold)
                {
                    buy = true;
                }

                // SAT: LY threshold'u yukarı kesiyor (fiyat düşüğe yaklaşıyor)
                if (prevLY <= _threshold && currentLY > _threshold)
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

        public double[]? GetHY() => _hy;
        public double[]? GetLY() => _ly;

        public override Dictionary<string, double[]>? GetPlotIndicators()
        {
            var indicators = new Dictionary<string, double[]>();

            if (_hy != null && _hy.Length > 0)
                indicators["HY"] = _hy;

            if (_ly != null && _ly.Length > 0)
                indicators["LY"] = _ly;

            return indicators.Count > 0 ? indicators : null;
        }
    }
}
