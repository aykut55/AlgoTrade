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
    /// PMax (Profit Maximizer) İndikatörü Stratejisi
    ///
    /// PMax Mantığı:
    /// - MOST + SuperTrend hibrit yapısı
    /// - ATR tabanlı trailing stop ile hareketli ortalama kombinasyonu
    ///
    /// Trading Logic (choice=0):
    /// - AL: Direction -1'den 1'e değişirse (trend dönüşü)
    /// - SAT: Direction 1'den -1'e değişirse (trend dönüşü)
    ///
    /// Trading Logic (choice=1):
    /// - (İleride eklenecek alternatif sinyal mantığı)
    ///
    /// Parametreler:
    /// - atrPeriod: ATR periyodu (varsayılan 10)
    /// - multiplier: ATR çarpanı (varsayılan 3.0)
    /// - maPeriod: MA periyodu (varsayılan 10)
    /// - choice: Sinyal mantığı seçimi (varsayılan 0)
    /// </summary>
    public class SimplePMaxStrategy : BaseStrategy
    {
        public override string Name => "Simple PMax Strategy";

        private readonly int _atrPeriod;
        private readonly double _multiplier;
        private readonly int _maPeriod;
        private readonly int _choice;
        private PMaxResult? _pmaxResult;

        public SimplePMaxStrategy(int atrPeriod = 10, double multiplier = 3.0, int maPeriod = 10, int choice = 0)
        {
            _atrPeriod = atrPeriod;
            _multiplier = multiplier;
            _maPeriod = maPeriod;
            _choice = choice;

            Parameters["ATRPeriod"] = atrPeriod;
            Parameters["Multiplier"] = multiplier;
            Parameters["MAPeriod"] = maPeriod;
            Parameters["Choice"] = choice;
        }

        public SimplePMaxStrategy(List<StockData> data, IndicatorManager indicators, int atrPeriod = 10, double multiplier = 3.0, int maPeriod = 10, int choice = 0)
        {
            _atrPeriod = atrPeriod;
            _multiplier = multiplier;
            _maPeriod = maPeriod;
            _choice = choice;

            Parameters["ATRPeriod"] = atrPeriod;
            Parameters["Multiplier"] = multiplier;
            Parameters["MAPeriod"] = maPeriod;
            Parameters["Choice"] = choice;

            // Initialize base strategy
            Initialize(data, indicators);
        }

        public override void OnInit()
        {
            if (!IsInitialized)
                return;

            _pmaxResult = Indicators.Trend.PMax(_atrPeriod, _multiplier, _maPeriod);

            //Log($"SimplePMaxStrategy initialized: ATRPeriod={_atrPeriod}, Multiplier={_multiplier}, MAPeriod={_maPeriod}");
        }

        public override TradeSignals OnStep(int currentIndex)
        {
            bool buy = false;
            bool sell = false;
            bool takeProfit = false;
            bool stopLoss = false;
            bool flat = false;
            bool skip = false;

            if (currentIndex < Math.Max(_atrPeriod, _maPeriod) + 1)
                return TradeSignals.None;

            if (_pmaxResult == null || _pmaxResult.Direction.Length == 0)
                return TradeSignals.None;

            int currentDirection = _pmaxResult.Direction[currentIndex];
            int prevDirection = _pmaxResult.Direction[currentIndex - 1];

            // ************************************************************************************************************************
            // choice: 0 = Direction change, 1 = (İleride eklenecek)
            if (_choice == 0)
            {
                // AL: Direction -1'den 1'e değişiyor
                if (prevDirection == -1 && currentDirection == 1)
                {
                    buy = true;
                }

                // SAT: Direction 1'den -1'e değişiyor
                if (prevDirection == 1 && currentDirection == -1)
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
                // Trader.flags.KarAlSeviyeHesaplaEnabled kapaliysa metod iceride 0 doner (takeProfit hep false kalir)
                takeProfit = Trader.karAlZararKes.SonFiyataGoreKarAlSeviyeHesaplaSeviyeli(currentIndex, 5, 50, 1000) != 0;
            }

            if (Trader != null)
            {
                // Trader.flags.ZararKesSeviyeHesaplaEnabled kapaliysa metod iceride 0 doner (stopLoss hep false kalir)
                stopLoss = Trader.karAlZararKes.SonFiyataGoreZararKesSeviyeHesaplaSeviyeli(currentIndex, -1, -10, 1000) != 0;
            }

            // Flat olma durumu burada incelenir ve flat flag'i setlenir
            flat = false;

            // Skip olma durumu burada incelenir ve skip flag'i setlenir
            skip = false;

            if (skip) return TradeSignals.Skip;
            else if (flat) return TradeSignals.Flat;
            else if (takeProfit) return TradeSignals.TakeProfit;
            else if (stopLoss) return TradeSignals.StopLoss;
            else if (buy) return TradeSignals.Buy;
            else if (sell) return TradeSignals.Sell;

            return TradeSignals.None;
        }

        public double[]? GetPMax() => _pmaxResult?.PMax;
        public double[]? GetMA() => _pmaxResult?.PMaxMA;
        public int[]? GetDirection() => _pmaxResult?.Direction;

        public override Dictionary<string, double[]>? GetPlotIndicators()
        {
            var indicators = new Dictionary<string, double[]>();

            var closes = Indicators.GetClosePrices();
            if (closes != null && closes.Length > 0)
                indicators["Close"] = closes;

            if (_pmaxResult?.PMax != null && _pmaxResult.PMax.Length > 0)
                indicators["PMax"] = _pmaxResult.PMax;

            if (_pmaxResult?.PMaxMA != null && _pmaxResult.PMaxMA.Length > 0)
                indicators["MA"] = _pmaxResult.PMaxMA;

            return indicators.Count > 0 ? indicators : null;
        }
    }
}
