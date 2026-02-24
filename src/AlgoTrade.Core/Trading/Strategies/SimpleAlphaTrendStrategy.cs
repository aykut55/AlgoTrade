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
    /// AlphaTrend İndikatörü Stratejisi
    ///
    /// AlphaTrend Mantığı:
    /// - ATR tabanlı dinamik destek/direnç
    /// - MFI/RSI momentum ile filtreleme
    /// - 2-bar offset crossover sinyalleri
    ///
    /// Trading Logic (choice=0):
    /// - AL: AlphaTrend[2] crossover AlphaTrend[1] (yukarı kesim)
    /// - SAT: AlphaTrend[2] crossunder AlphaTrend[1] (aşağı kesim)
    ///
    /// Trading Logic (choice=1):
    /// - (İleride eklenecek alternatif sinyal mantığı)
    ///
    /// Parametreler:
    /// - atrPeriod: ATR periyodu (varsayılan 14)
    /// - coefficient: ATR çarpanı (varsayılan 1.0)
    /// - momentumPeriod: MFI/RSI periyodu (varsayılan 14)
    /// - choice: Sinyal mantığı seçimi (varsayılan 0)
    /// </summary>
    public class SimpleAlphaTrendStrategy : BaseStrategy
    {
        public override string Name => "Simple AlphaTrend Strategy";

        private readonly int _atrPeriod;
        private readonly double _coefficient;
        private readonly int _momentumPeriod;
        private readonly int _choice;
        private AlphaTrendResult? _alphaTrendResult;

        public SimpleAlphaTrendStrategy(int atrPeriod = 14, double coefficient = 1.0, int momentumPeriod = 14, int choice = 0)
        {
            _atrPeriod = atrPeriod;
            _coefficient = coefficient;
            _momentumPeriod = momentumPeriod;
            _choice = choice;

            Parameters["ATRPeriod"] = atrPeriod;
            Parameters["Coefficient"] = coefficient;
            Parameters["MomentumPeriod"] = momentumPeriod;
            Parameters["Choice"] = choice;
        }

        public SimpleAlphaTrendStrategy(List<StockData> data, IndicatorManager indicators, int atrPeriod = 14, double coefficient = 1.0, int momentumPeriod = 14, int choice = 0)
        {
            _atrPeriod = atrPeriod;
            _coefficient = coefficient;
            _momentumPeriod = momentumPeriod;
            _choice = choice;

            Parameters["ATRPeriod"] = atrPeriod;
            Parameters["Coefficient"] = coefficient;
            Parameters["MomentumPeriod"] = momentumPeriod;
            Parameters["Choice"] = choice;

            // Initialize base strategy
            Initialize(data, indicators);
        }

        public override void OnInit()
        {
            if (!IsInitialized)
                return;

            _alphaTrendResult = Indicators.Trend.AlphaTrend(_atrPeriod, _coefficient, _momentumPeriod);

            //Log($"SimpleAlphaTrendStrategy initialized: ATRPeriod={_atrPeriod}, Coefficient={_coefficient}, MomentumPeriod={_momentumPeriod}");
        }

        public override TradeSignals OnStep(int currentIndex)
        {
            bool buy = false;
            bool sell = false;
            bool takeProfit = false;
            bool stopLoss = false;
            bool flat = false;
            bool skip = false;

            if (currentIndex < _atrPeriod + 2)
                return TradeSignals.None;

            if (_alphaTrendResult == null || _alphaTrendResult.AlphaTrend.Length == 0)
                return TradeSignals.None;

            var alphaTrend = _alphaTrendResult.AlphaTrend;

            // 2-bar offset crossover: AlphaTrend[current-2] crosses AlphaTrend[current-1]
            double at0 = alphaTrend[currentIndex];
            double at1 = alphaTrend[currentIndex - 1];
            double at2 = alphaTrend[currentIndex - 2];

            // ************************************************************************************************************************
            // choice: 0 = 2-bar offset crossover, 1 = (İleride eklenecek)
            if (_choice == 0)
            {
                // AL: AlphaTrend[2] yukarı kesim (önceki: at2 <= at1, şimdi: at0 > at1)
                if (at2 <= at1 && at0 > at1)
                {
                    buy = true;
                }

                // SAT: AlphaTrend[2] aşağı kesim (önceki: at2 >= at1, şimdi: at0 < at1)
                if (at2 >= at1 && at0 < at1)
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

        public double[]? GetAlphaTrend() => _alphaTrendResult?.AlphaTrend;

        public override Dictionary<string, double[]>? GetPlotIndicators()
        {
            var indicators = new Dictionary<string, double[]>();

            var closes = Indicators.GetClosePrices();
            if (closes != null && closes.Length > 0)
                indicators["Close"] = closes;

            if (_alphaTrendResult?.AlphaTrend != null && _alphaTrendResult.AlphaTrend.Length > 0)
                indicators["AlphaTrend"] = _alphaTrendResult.AlphaTrend;

            return indicators.Count > 0 ? indicators : null;
        }
    }
}
