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
    /// MavilimW İndikatörü Stratejisi
    ///
    /// MavilimW Mantığı:
    /// - Fibonacci tabanlı ağırlıklı hareketli ortalama kombinasyonları
    /// - Hem trend indikatörü hem destek/direnç görevi görür
    ///
    /// Trading Logic (choice=0):
    /// - AL: Fiyat MavilimW'yi yukarı kesiyor
    /// - SAT: Fiyat MavilimW'yi aşağı kesiyor
    ///
    /// Trading Logic (choice=1):
    /// - (İleride eklenecek alternatif sinyal mantığı)
    ///
    /// Parametreler:
    /// - param1: Birinci hassasiyet parametresi (varsayılan 3)
    /// - param2: İkinci hassasiyet parametresi (varsayılan 5)
    /// - choice: Sinyal mantığı seçimi (varsayılan 0)
    /// </summary>
    public class SimpleMavilimWStrategy : BaseStrategy
    {
        public override string Name => "Simple MavilimW Strategy";

        private readonly int _param1;
        private readonly int _param2;
        private readonly int _choice;
        private MavilimWResult? _mavilimWResult;

        public SimpleMavilimWStrategy(int param1 = 3, int param2 = 5, int choice = 0)
        {
            _param1 = param1;
            _param2 = param2;
            _choice = choice;

            Parameters["Param1"] = param1;
            Parameters["Param2"] = param2;
            Parameters["Choice"] = choice;
        }

        public SimpleMavilimWStrategy(List<StockData> data, IndicatorManager indicators, int param1 = 3, int param2 = 5, int choice = 0)
        {
            _param1 = param1;
            _param2 = param2;
            _choice = choice;

            Parameters["Param1"] = param1;
            Parameters["Param2"] = param2;
            Parameters["Choice"] = choice;

            // Initialize base strategy
            // Initialize(data, indicators);
        }

        public override void OnInit()
        {
            if (!IsInitialized)
                return;

            _mavilimWResult = Indicators.Trend.MavilimW(_param1, _param2);

            Log($"SimpleMavilimWStrategy initialized: Param1={_param1}, Param2={_param2}");
        }

        public override TradeSignals OnStep(int currentIndex)
        {
            bool buy = false;
            bool sell = false;
            bool takeProfit = false;
            bool stopLoss = false;
            bool flat = false;
            bool skip = false;

            int minPeriod = 100; // MavilimW uses Fibonacci periods up to 250
            if (currentIndex < minPeriod)
                return TradeSignals.None;

            if (_mavilimWResult == null || _mavilimWResult.MavilimW.Length == 0)
                return TradeSignals.None;

            var mavilimW = _mavilimWResult.MavilimW;
            double currentPrice = Data[currentIndex].Close;
            double prevPrice = Data[currentIndex - 1].Close;
            double currentMavilim = mavilimW[currentIndex];
            double prevMavilim = mavilimW[currentIndex - 1];

            if (double.IsNaN(currentMavilim) || double.IsNaN(prevMavilim))
                return TradeSignals.None;

            // ************************************************************************************************************************
            // choice: 0 = Price-MavilimW crossover, 1 = (İleride eklenecek)
            if (_choice == 0)
            {
                // AL: Fiyat MavilimW'yi yukarı kesiyor
                if (prevPrice <= prevMavilim && currentPrice > currentMavilim)
                {
                    buy = true;
                }

                // SAT: Fiyat MavilimW'yi aşağı kesiyor
                if (prevPrice >= prevMavilim && currentPrice < currentMavilim)
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

        public double[]? GetMavilimW() => _mavilimWResult?.MavilimW;
        public double[]? GetTrendline() => _mavilimWResult?.Trendline;

        public override Dictionary<string, double[]>? GetPlotIndicators()
        {
            var indicators = new Dictionary<string, double[]>();

            var closes = Indicators.GetClosePrices();
            if (closes != null && closes.Length > 0)
                indicators["Close"] = closes;

            if (_mavilimWResult?.MavilimW != null && _mavilimWResult.MavilimW.Length > 0)
                indicators["MavilimW"] = _mavilimWResult.MavilimW;

            return indicators.Count > 0 ? indicators : null;
        }
    }
}
