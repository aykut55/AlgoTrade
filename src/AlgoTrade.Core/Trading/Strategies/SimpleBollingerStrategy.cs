using AlgoTrade.Core;
using AlgoTrade.Core.Trading.Indicators;
using AlgoTrade.Core.Trading.Indicators.Volatility.Results;
using AlgoTrade.Core.Logging;
using AlgoTrade.Core.Trading.Core;
using AlgoTrade.Core.Trading.Strategy;
using System;
using System.Collections.Generic;

namespace AlgoTrade.Core.Trading.Strategies
{
    /// <summary>
    /// Bollinger Bands Stratejisi
    ///
    /// Bollinger Bands Mantığı:
    /// - Middle Band: SMA
    /// - Upper Band: Middle + (StdDev * multiplier)
    /// - Lower Band: Middle - (StdDev * multiplier)
    ///
    /// Trading Logic (choice=0):
    /// - AL: Fiyat alt bandı yukarı kesiyor (oversold'dan çıkış)
    /// - SAT: Fiyat üst bandı aşağı kesiyor (overbought'dan çıkış)
    ///
    /// Trading Logic (choice=1):
    /// - (İleride eklenecek alternatif sinyal mantığı)
    ///
    /// Parametreler:
    /// - period: BB periyodu (varsayılan 20)
    /// - multiplier: StdDev çarpanı (varsayılan 2.0)
    /// - choice: Sinyal mantığı seçimi (varsayılan 0)
    /// </summary>
    public class SimpleBollingerStrategy : BaseStrategy
    {
        public override string Name => "Simple Bollinger Strategy";

        private readonly int _period;
        private readonly double _multiplier;
        private readonly int _choice;
        private BollingerBandsResult? _bbResult;

        public SimpleBollingerStrategy(int period = 20, double multiplier = 2.0, int choice = 0)
        {
            _period = period;
            _multiplier = multiplier;
            _choice = choice;

            Parameters["Period"] = period;
            Parameters["Multiplier"] = multiplier;
            Parameters["Choice"] = choice;
        }

        public SimpleBollingerStrategy(List<StockData> data, IndicatorManager indicators, int period = 20, double multiplier = 2.0, int choice = 0)
        {
            _period = period;
            _multiplier = multiplier;
            _choice = choice;

            Parameters["Period"] = period;
            Parameters["Multiplier"] = multiplier;
            Parameters["Choice"] = choice;

            // Initialize base strategy
            Initialize(data, indicators);
        }

        public override void OnInit()
        {
            if (!IsInitialized)
                return;

            var closes = Indicators.GetClosePrices();
            _bbResult = Indicators.Volatility.BollingerBands(closes, _period, _multiplier);

            //Log($"SimpleBollingerStrategy initialized: Period={_period}, Multiplier={_multiplier}");
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

            if (_bbResult == null || _bbResult.Upper.Length == 0)
                return TradeSignals.None;

            double currentClose = Data[currentIndex].Close;
            double prevClose = Data[currentIndex - 1].Close;
            double currentUpper = _bbResult.Upper[currentIndex];
            double prevUpper = _bbResult.Upper[currentIndex - 1];
            double currentLower = _bbResult.Lower[currentIndex];
            double prevLower = _bbResult.Lower[currentIndex - 1];

            if (double.IsNaN(currentUpper) || double.IsNaN(currentLower))
                return TradeSignals.None;

            // ************************************************************************************************************************
            // choice: 0 = Bollinger band crossover, 1 = (İleride eklenecek)
            if (_choice == 0)
            {
                // AL: Fiyat alt bandı yukarı kesiyor
                if (prevClose <= prevLower && currentClose > currentLower)
                {
                    buy = true;
                }

                // SAT: Fiyat üst bandı aşağı kesiyor
                if (prevClose >= prevUpper && currentClose < currentUpper)
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

        public double[]? GetUpper() => _bbResult?.Upper;
        public double[]? GetMiddle() => _bbResult?.Middle;
        public double[]? GetLower() => _bbResult?.Lower;

        public override Dictionary<string, double[]>? GetPlotIndicators()
        {
            var indicators = new Dictionary<string, double[]>();

            var closes = Indicators.GetClosePrices();
            if (closes != null && closes.Length > 0)
                indicators["Close"] = closes;

            if (_bbResult?.Upper != null && _bbResult.Upper.Length > 0)
                indicators["BB_Upper"] = _bbResult.Upper;

            if (_bbResult?.Middle != null && _bbResult.Middle.Length > 0)
                indicators["BB_Middle"] = _bbResult.Middle;

            if (_bbResult?.Lower != null && _bbResult.Lower.Length > 0)
                indicators["BB_Lower"] = _bbResult.Lower;

            return indicators.Count > 0 ? indicators : null;
        }
    }
}
