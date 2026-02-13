using AlgoTrade.Core;
using AlgoTrade.Core.Trading.Indicators;
using AlgoTrade.Core.Trading.Indicators.Momentum.Results;
using AlgoTrade.Core.Logging;
using AlgoTrade.Core.Trading.Core;
using AlgoTrade.Core.Trading.Strategy;
using System;
using System.Collections.Generic;

namespace AlgoTrade.Core.Trading.Strategies
{
    /// <summary>
    /// RSI (Relative Strength Index) Stratejisi
    ///
    /// RSI Mantığı:
    /// - 0-100 arası momentum osilatörü
    /// - 70 üstü: Aşırı alım, 30 altı: Aşırı satım
    ///
    /// Trading Logic (choice=0):
    /// - AL: RSI 30 seviyesini yukarı kesiyor (aşırı satımdan çıkış)
    /// - SAT: RSI 70 seviyesini aşağı kesiyor (aşırı alımdan çıkış)
    ///
    /// Trading Logic (choice=1):
    /// - (İleride eklenecek alternatif sinyal mantığı)
    ///
    /// Parametreler:
    /// - period: RSI periyodu (varsayılan 14)
    /// - oversold: Aşırı satım seviyesi (varsayılan 30)
    /// - overbought: Aşırı alım seviyesi (varsayılan 70)
    /// - choice: Sinyal mantığı seçimi (varsayılan 0)
    /// </summary>
    public class SimpleRSIStrategy : BaseStrategy
    {
        public override string Name => "Simple RSI Strategy";

        private readonly int _period;
        private readonly double _oversold;
        private readonly double _overbought;
        private readonly int _choice;
        private RSIResult? _rsiResult;

        public SimpleRSIStrategy(int period = 14, double oversold = 30, double overbought = 70, int choice = 0)
        {
            _period = period;
            _oversold = oversold;
            _overbought = overbought;
            _choice = choice;

            Parameters["Period"] = period;
            Parameters["Oversold"] = oversold;
            Parameters["Overbought"] = overbought;
            Parameters["Choice"] = choice;
        }

        public SimpleRSIStrategy(List<StockData> data, IndicatorManager indicators, int period = 14, double oversold = 30, double overbought = 70, int choice = 0)
        {
            _period = period;
            _oversold = oversold;
            _overbought = overbought;
            _choice = choice;

            Parameters["Period"] = period;
            Parameters["Oversold"] = oversold;
            Parameters["Overbought"] = overbought;
            Parameters["Choice"] = choice;

            Initialize(data, indicators);
        }

        public override void OnInit()
        {
            if (!IsInitialized)
                return;

            var closes = Indicators.GetClosePrices();
            _rsiResult = Indicators.Momentum.RSI(closes, _period);

            LogManager.Log($"SimpleRSIStrategy initialized: Period={_period}, Oversold={_oversold}, Overbought={_overbought}");
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

            if (_rsiResult == null || _rsiResult.Values.Length == 0)
                return TradeSignals.None;

            double currentRSI = _rsiResult.Values[currentIndex];
            double prevRSI = _rsiResult.Values[currentIndex - 1];

            if (double.IsNaN(currentRSI) || double.IsNaN(prevRSI))
                return TradeSignals.None;

            // ************************************************************************************************************************
            // choice: 0 = RSI overbought/oversold crossover, 1 = (İleride eklenecek)
            if (_choice == 0)
            {
                // AL: RSI oversold seviyesini yukarı kesiyor
                if (prevRSI <= _oversold && currentRSI > _oversold)
                {
                    buy = true;
                }

                // SAT: RSI overbought seviyesini aşağı kesiyor
                if (prevRSI >= _overbought && currentRSI < _overbought)
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

        public double[]? GetRSI() => _rsiResult?.Values;

        public override Dictionary<string, double[]>? GetPlotIndicators()
        {
            var indicators = new Dictionary<string, double[]>();

            if (_rsiResult?.Values != null && _rsiResult.Values.Length > 0)
                indicators["RSI"] = _rsiResult.Values;

            return indicators.Count > 0 ? indicators : null;
        }
    }
}
