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
    /// Kairi Relative Index Stratejisi
    ///
    /// Kairi Mantığı:
    /// - Fiyatın hareketli ortalamaya göre yüzdesel sapması
    /// - Kairi = ((Close - MA) / MA) * 100
    /// - Pozitif: Fiyat MA üzerinde, Negatif: Fiyat MA altında
    ///
    /// Trading Logic (choice=0):
    /// - AL: Kairi pozitif eşiği yukarı kesiyor
    /// - SAT: Kairi negatif eşiği aşağı kesiyor
    ///
    /// Trading Logic (choice=1):
    /// - (İleride eklenecek alternatif sinyal mantığı)
    ///
    /// Parametreler:
    /// - period: MA periyodu (varsayılan 20)
    /// - positiveThreshold: Pozitif sinyal eşiği (varsayılan 5)
    /// - negativeThreshold: Negatif sinyal eşiği (varsayılan -5)
    /// - choice: Sinyal mantığı seçimi (varsayılan 0)
    /// </summary>
    public class SimpleKairiStrategy : BaseStrategy
    {
        public override string Name => "Simple Kairi Strategy";

        private readonly int _period;
        private readonly double _positiveThreshold;
        private readonly double _negativeThreshold;
        private readonly int _choice;
        private double[]? _kairi;
        private double[]? _ma;

        public SimpleKairiStrategy(int period = 20, double positiveThreshold = 5, double negativeThreshold = -5, int choice = 0)
        {
            _period = period;
            _positiveThreshold = positiveThreshold;
            _negativeThreshold = negativeThreshold;
            _choice = choice;

            Parameters["Period"] = period;
            Parameters["PositiveThreshold"] = positiveThreshold;
            Parameters["NegativeThreshold"] = negativeThreshold;
            Parameters["Choice"] = choice;
        }

        public SimpleKairiStrategy(List<StockData> data, IndicatorManager indicators, int period = 20, double positiveThreshold = 5, double negativeThreshold = -5, int choice = 0)
        {
            _period = period;
            _positiveThreshold = positiveThreshold;
            _negativeThreshold = negativeThreshold;
            _choice = choice;

            Parameters["Period"] = period;
            Parameters["PositiveThreshold"] = positiveThreshold;
            Parameters["NegativeThreshold"] = negativeThreshold;
            Parameters["Choice"] = choice;

            // Initialize base strategy
            Initialize(data, indicators);
        }

        public override void OnInit()
        {
            if (!IsInitialized)
                return;

            var closes = Indicators.GetClosePrices();
            _ma = Indicators.MA.SMA(closes, _period);

            int length = closes.Length;
            _kairi = new double[length];

            for (int i = 0; i < length; i++)
            {
                if (double.IsNaN(_ma[i]) || _ma[i] == 0)
                {
                    _kairi[i] = double.NaN;
                }
                else
                {
                    _kairi[i] = ((closes[i] - _ma[i]) / _ma[i]) * 100;
                }
            }

            //Log($"SimpleKairiStrategy initialized: Period={_period}, PositiveThreshold={_positiveThreshold}, NegativeThreshold={_negativeThreshold}");
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

            if (_kairi == null || _kairi.Length == 0)
                return TradeSignals.None;

            double currentKairi = _kairi[currentIndex];
            double prevKairi = _kairi[currentIndex - 1];

            if (double.IsNaN(currentKairi) || double.IsNaN(prevKairi))
                return TradeSignals.None;

            // ************************************************************************************************************************
            // choice: 0 = Kairi threshold crossover, 1 = (İleride eklenecek)
            if (_choice == 0)
            {
                // AL: Kairi pozitif eşiği yukarı kesiyor
                if (prevKairi <= _positiveThreshold && currentKairi > _positiveThreshold)
                {
                    buy = true;
                }

                // SAT: Kairi negatif eşiği aşağı kesiyor
                if (prevKairi >= _negativeThreshold && currentKairi < _negativeThreshold)
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

        public double[]? GetKairi() => _kairi;
        public double[]? GetMA() => _ma;

        public override Dictionary<string, double[]>? GetPlotIndicators()
        {
            var indicators = new Dictionary<string, double[]>();

            if (_kairi != null && _kairi.Length > 0)
                indicators["Kairi"] = _kairi;

            return indicators.Count > 0 ? indicators : null;
        }
    }
}
