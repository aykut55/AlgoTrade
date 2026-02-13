using AlgoTrade.Core;
using AlgoTrade.Core.Trading.Indicators;
using AlgoTrade.Core.Trading.Indicators.Trend.Results;
using AlgoTrade.Core.Logging;
using AlgoTrade.Core.Trading.Core;
using AlgoTrade.Core.Trading.Strategy;
using System;
using System.Collections.Generic;
using Microsoft.FSharp.Core;

namespace AlgoTrade.Core.Trading.Strategies
{
    /// <summary>
    /// SuperTrend İndikatörü Stratejisi
    ///
    /// SuperTrend Mantığı:
    /// - ATR tabanlı trend-following indicator
    /// - Yükseliş trendinde: SuperTrend fiyatın altında destek görevi görür
    /// - Düşüş trendinde: SuperTrend fiyatın üstünde direnç görevi görür
    /// - Direction: 1 (bullish), -1 (bearish)
    ///
    /// Trading Logic:
    /// - AL: Direction -1'den 1'e değişirse (trend dönüşü: düşüşten yükselişe)
    /// - SAT: Direction 1'den -1'e değişirse (trend dönüşü: yükselişten düşüşe)
    ///
    /// Parametreler:
    /// - period: ATR periyodu (varsayılan 10)
    /// - multiplier: ATR çarpanı (varsayılan 3.0)
    /// </summary>
    public class SimpleSuperTrendStrategy : BaseStrategy
    {
        public override string Name => "Simple SuperTrend Strategy";

        private readonly int _period;
        private readonly double _multiplier;
        private readonly int _choice;
        private SuperTrendResult? _superTrendResult;

        // Parametresiz constructor (eski kullanımlar için)
        public SimpleSuperTrendStrategy(int period = 10, double multiplier = 3.0, int choice = 0)
        {
            _period = period;
            _multiplier = multiplier;
            _choice = choice;

            Parameters["Period"] = period;
            Parameters["Multiplier"] = multiplier;
            Parameters["Choice"] = choice;
        }

        // Parametreli constructor (yeni kullanım)
        public SimpleSuperTrendStrategy(List<StockData> data, IndicatorManager indicators, int period = 10, double multiplier = 3.0, int choice = 0)
        {
            _period = period;
            _multiplier = multiplier;
            _choice = choice;

            Parameters["Period"] = period;
            Parameters["Multiplier"] = multiplier;
            Parameters["Choice"] = choice;

            // Initialize base strategy
            // Initialize(data, indicators);
        }

        public override void OnInit()
        {
            if (!IsInitialized)
                return;

            // SuperTrend indicator'ı hesapla
            _superTrendResult = Indicators.Trend.SuperTrend(_period, _multiplier);

            LogManager.Log($"SimpleSuperTrendStrategy initialized: Period={_period}, Multiplier={_multiplier}, Choice={_choice}");
            LogManager.Log($"SuperTrend calculated for {_superTrendResult.Length} bars");
        }

        public override TradeSignals OnStep(int currentIndex)
        {
            bool buy = false;
            bool sell = false;
            bool takeProfit = false;
            bool stopLoss = false;
            bool flat = false;
            bool skip = false;

            // İlk barlarda yeterli veri yok
            if (currentIndex < _period)
                return TradeSignals.None;

            // SuperTrend hesaplanmamışsa sinyal üretme
            if (_superTrendResult == null || _superTrendResult.Length == 0)
                return TradeSignals.None;

            // Geçerli ve önceki direction değerleri
            int currentDirection = _superTrendResult.Direction[currentIndex];
            int prevDirection = _superTrendResult.Direction[currentIndex - 1];

            // AL Sinyali: Direction -1'den 1'e değişiyor (trend dönüşü: düşüşten yükselişe)
            // Fiyat SuperTrend'i yukarı kırıyor
            if (prevDirection == -1 && currentDirection == 1)
            {
                buy = true;
            }

            // SAT Sinyali: Direction 1'den -1'e değişiyor (trend dönüşü: yükselişten düşüşe)
            // Fiyat SuperTrend'i aşağı kırıyor
            if (prevDirection == 1 && currentDirection == -1)
            {
                sell = true;
            }

            // ÖRNEK: Trader referansını kullanarak kar al / zarar kes hesaplama
            // Trader property'si BaseStrategy.SetTrader() ile otomatik set edilir
            if (Trader != null)
            {
                takeProfit = Trader.karAlZararKes.SonFiyataGoreKarAlSeviyeHesaplaSeviyeli(currentIndex, 5, 50, 1000) != 0;
            }

            if (Trader != null)
            {
                stopLoss = Trader.karAlZararKes.SonFiyataGoreZararKesSeviyeHesaplaSeviyeli(currentIndex, -1, -10, 1000) != 0;
            }

            // ************************************************************************************************************************
            // ************************************************************************************************************************
            // ************************************************************************************************************************
            // Sinyal önceliklendirmesi
            // ************************************************************************************************************************
            // ************************************************************************************************************************
            // ************************************************************************************************************************
            if (skip)
            {
                return TradeSignals.Skip;
            }
            else if (flat)
            {
                return TradeSignals.Flat;
            }
            else if (takeProfit)
            {
                return TradeSignals.TakeProfit;
            }
            else if (stopLoss)
            {
                return TradeSignals.StopLoss;
            }
            else if (buy)
            {
                return TradeSignals.Buy;
            }
            else if (sell)
            {
                return TradeSignals.Sell;
            }
            // ************************************************************************************************************************
            // ************************************************************************************************************************
            // ************************************************************************************************************************

            return TradeSignals.None;
        }

        /// <summary>
        /// SuperTrend değerlerini al (plotting veya analiz için)
        /// </summary>
        public double[]? GetSuperTrend() => _superTrendResult?.SuperTrend;

        /// <summary>
        /// SuperTrend direction değerlerini al (plotting veya analiz için)
        /// </summary>
        public int[]? GetDirection() => _superTrendResult?.Direction;

        /// <summary>
        /// Period parametresini al
        /// </summary>
        public int Period => _period;

        /// <summary>
        /// Multiplier parametresini al
        /// </summary>
        public double Multiplier => _multiplier;

        /// <summary>
        /// Şu anki trend yönü (1 = bullish, -1 = bearish)
        /// </summary>
        public int CurrentDirection => _superTrendResult?.CurrentDirection ?? 0;

        /// <summary>
        /// Şu anki trend bullish mi?
        /// </summary>
        public bool IsBullish => _superTrendResult?.IsBullish ?? false;

        /// <summary>
        /// Şu anki trend bearish mi?
        /// </summary>
        public bool IsBearish => _superTrendResult?.IsBearish ?? false;

        /// <summary>
        /// Get indicators for plotting (IStrategy implementation)
        /// </summary>
        public override Dictionary<string, double[]>? GetPlotIndicators()
        {
            var indicators = new Dictionary<string, double[]>();

            // Close fiyatlarını ekle
            var closes = Indicators.GetClosePrices();
            if (closes != null && closes.Length > 0)
                indicators["Close"] = closes;

            if (_superTrendResult?.SuperTrend != null && _superTrendResult.SuperTrend.Length > 0)
                indicators["SuperTrend"] = _superTrendResult.SuperTrend;

            // Direction'ı double[] olarak dönüştür (int[] -> double[])
            if (_superTrendResult?.Direction != null && _superTrendResult.Direction.Length > 0)
            {
                var directionAsDouble = new double[_superTrendResult.Direction.Length];
                for (int i = 0; i < _superTrendResult.Direction.Length; i++)
                {
                    directionAsDouble[i] = _superTrendResult.Direction[i];
                }
                //indicators["Direction"] = directionAsDouble;
            }

            return indicators.Count > 0 ? indicators : null;
        }
    }
}
