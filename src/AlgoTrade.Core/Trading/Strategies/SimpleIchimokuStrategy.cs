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
    /// Ichimoku Cloud Stratejisi (TK Cross)
    ///
    /// Ichimoku Mantığı:
    /// - Tenkan-sen (Conversion Line): Kısa vadeli trend
    /// - Kijun-sen (Base Line): Orta vadeli trend
    /// - TK Cross: Tenkan ve Kijun kesişimi
    ///
    /// Trading Logic (choice=0):
    /// - AL: Tenkan, Kijun'u yukarı kesiyor (bullish cross)
    /// - SAT: Tenkan, Kijun'u aşağı kesiyor (bearish cross)
    ///
    /// Trading Logic (choice=1):
    /// - (İleride eklenecek alternatif sinyal mantığı)
    ///
    /// Parametreler:
    /// - tenkanPeriod: Tenkan-sen periyodu (varsayılan 9)
    /// - kijunPeriod: Kijun-sen periyodu (varsayılan 26)
    /// - senkouPeriod: Senkou Span B periyodu (varsayılan 52)
    /// - choice: Sinyal mantığı seçimi (varsayılan 0)
    /// </summary>
    public class SimpleIchimokuStrategy : BaseStrategy
    {
        public override string Name => "Simple Ichimoku Strategy";

        private readonly int _tenkanPeriod;
        private readonly int _kijunPeriod;
        private readonly int _senkouPeriod;
        private readonly int _choice;
        private IchimokuResult? _ichimokuResult;

        public SimpleIchimokuStrategy(int tenkanPeriod = 9, int kijunPeriod = 26, int senkouPeriod = 52, int choice = 0)
        {
            _tenkanPeriod = tenkanPeriod;
            _kijunPeriod = kijunPeriod;
            _senkouPeriod = senkouPeriod;
            _choice = choice;

            Parameters["TenkanPeriod"] = tenkanPeriod;
            Parameters["KijunPeriod"] = kijunPeriod;
            Parameters["SenkouPeriod"] = senkouPeriod;
            Parameters["Choice"] = choice;
        }

        public SimpleIchimokuStrategy(List<StockData> data, IndicatorManager indicators, int tenkanPeriod = 9, int kijunPeriod = 26, int senkouPeriod = 52, int choice = 0)
        {
            _tenkanPeriod = tenkanPeriod;
            _kijunPeriod = kijunPeriod;
            _senkouPeriod = senkouPeriod;
            _choice = choice;

            Parameters["TenkanPeriod"] = tenkanPeriod;
            Parameters["KijunPeriod"] = kijunPeriod;
            Parameters["SenkouPeriod"] = senkouPeriod;
            Parameters["Choice"] = choice;

            // Initialize base strategy
            Initialize(data, indicators);
        }

        public override void OnInit()
        {
            if (!IsInitialized)
                return;

            _ichimokuResult = Indicators.Trend.Ichimoku(_tenkanPeriod, _kijunPeriod, _senkouPeriod);

            Log($"SimpleIchimokuStrategy initialized: Tenkan={_tenkanPeriod}, Kijun={_kijunPeriod}, Senkou={_senkouPeriod}");
        }

        public override TradeSignals OnStep(int currentIndex)
        {
            bool buy = false;
            bool sell = false;
            bool takeProfit = false;
            bool stopLoss = false;
            bool flat = false;
            bool skip = false;

            if (currentIndex < _kijunPeriod + 1)
                return TradeSignals.None;

            if (_ichimokuResult == null || _ichimokuResult.Tenkan.Length == 0)
                return TradeSignals.None;

            double currentTenkan = _ichimokuResult.Tenkan[currentIndex];
            double prevTenkan = _ichimokuResult.Tenkan[currentIndex - 1];
            double currentKijun = _ichimokuResult.Kijun[currentIndex];
            double prevKijun = _ichimokuResult.Kijun[currentIndex - 1];

            if (double.IsNaN(currentTenkan) || double.IsNaN(currentKijun) ||
                double.IsNaN(prevTenkan) || double.IsNaN(prevKijun))
                return TradeSignals.None;

            // ************************************************************************************************************************
            // choice: 0 = TK Cross, 1 = (İleride eklenecek)
            if (_choice == 0)
            {
                // AL: Tenkan, Kijun'u yukarı kesiyor (TK Cross Bullish)
                if (prevTenkan <= prevKijun && currentTenkan > currentKijun)
                {
                    buy = true;
                }

                // SAT: Tenkan, Kijun'u aşağı kesiyor (TK Cross Bearish)
                if (prevTenkan >= prevKijun && currentTenkan < currentKijun)
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

        public double[]? GetTenkan() => _ichimokuResult?.Tenkan;
        public double[]? GetKijun() => _ichimokuResult?.Kijun;
        public double[]? GetSenkouA() => _ichimokuResult?.SenkouA;
        public double[]? GetSenkouB() => _ichimokuResult?.SenkouB;

        public override Dictionary<string, double[]>? GetPlotIndicators()
        {
            var indicators = new Dictionary<string, double[]>();

            var closes = Indicators.GetClosePrices();
            if (closes != null && closes.Length > 0)
                indicators["Close"] = closes;

            if (_ichimokuResult?.Tenkan != null && _ichimokuResult.Tenkan.Length > 0)
                indicators["Tenkan"] = _ichimokuResult.Tenkan;

            if (_ichimokuResult?.Kijun != null && _ichimokuResult.Kijun.Length > 0)
                indicators["Kijun"] = _ichimokuResult.Kijun;

            if (_ichimokuResult?.SenkouA != null && _ichimokuResult.SenkouA.Length > 0)
                indicators["SenkouA"] = _ichimokuResult.SenkouA;

            if (_ichimokuResult?.SenkouB != null && _ichimokuResult.SenkouB.Length > 0)
                indicators["SenkouB"] = _ichimokuResult.SenkouB;

            return indicators.Count > 0 ? indicators : null;
        }
    }
}
