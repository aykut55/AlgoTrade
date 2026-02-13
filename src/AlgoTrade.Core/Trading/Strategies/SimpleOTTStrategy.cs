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
    /// OTT (Optimized Trend Tracker) İndikatörü Stratejisi
    ///
    /// OTT Mantığı:
    /// - Hareketli ortalama tabanlı trend takipçisi
    /// - ATR optimizasyonu ile daha stabil sinyaller
    ///
    /// Trading Logic (choice=0):
    /// - AL: MA, OTT'yi yukarı kesiyor
    /// - SAT: MA, OTT'yi aşağı kesiyor
    ///
    /// Trading Logic (choice=1):
    /// - (İleride eklenecek alternatif sinyal mantığı)
    ///
    /// Parametreler:
    /// - period: MA periyodu (varsayılan 2)
    /// - percent: OTT yüzde sapması (varsayılan 1.4)
    /// - choice: Sinyal mantığı seçimi (varsayılan 0)
    /// </summary>
    public class SimpleOTTStrategy : BaseStrategy
    {
        public override string Name => "Simple OTT Strategy";

        private readonly int _period;
        private readonly double _percent;
        private readonly int _choice;
        private OTTResult? _ottResult;

        public SimpleOTTStrategy(int period = 2, double percent = 1.4, int choice = 0)
        {
            _period = period;
            _percent = percent;
            _choice = choice;

            Parameters["Period"] = period;
            Parameters["Percent"] = percent;
            Parameters["Choice"] = choice;
        }

        public SimpleOTTStrategy(List<StockData> data, IndicatorManager indicators, int period = 2, double percent = 1.4, int choice = 0)
        {
            _period = period;
            _percent = percent;
            _choice = choice;

            Parameters["Period"] = period;
            Parameters["Percent"] = percent;
            Parameters["Choice"] = choice;

            // Initialize base strategy
            // Initialize(data, indicators);
        }

        public override void OnInit()
        {
            if (!IsInitialized)
                return;

            _ottResult = Indicators.Trend.OTT(_period, _percent);

            LogManager.Log($"SimpleOTTStrategy initialized: Period={_period}, Percent={_percent}");
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

            if (_ottResult == null || _ottResult.OTT.Length == 0)
                return TradeSignals.None;

            var ott = _ottResult.OTT;
            var ma = _ottResult.MA;

            double currentOtt = ott[currentIndex];
            double prevOtt = ott[currentIndex - 1];
            double currentMa = ma[currentIndex];
            double prevMa = ma[currentIndex - 1];

            // ************************************************************************************************************************
            // choice: 0 = MA-OTT crossover, 1 = (İleride eklenecek)
            if (_choice == 0)
            {
                // AL: MA, OTT'yi yukarı kesiyor
                if (prevMa <= prevOtt && currentMa > currentOtt)
                {
                    buy = true;
                }

                // SAT: MA, OTT'yi aşağı kesiyor
                if (prevMa >= prevOtt && currentMa < currentOtt)
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

        public double[]? GetOTT() => _ottResult?.OTT;
        public double[]? GetMA() => _ottResult?.MA;

        public override Dictionary<string, double[]>? GetPlotIndicators()
        {
            var indicators = new Dictionary<string, double[]>();

            var closes = Indicators.GetClosePrices();
            if (closes != null && closes.Length > 0)
                indicators["Close"] = closes;

            if (_ottResult?.OTT != null && _ottResult.OTT.Length > 0)
                indicators["OTT"] = _ottResult.OTT;

            if (_ottResult?.MA != null && _ottResult.MA.Length > 0)
                indicators["MA"] = _ottResult.MA;

            return indicators.Count > 0 ? indicators : null;
        }
    }
}
