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
    /// ATR (Average True Range) Volatilite Kırılım Stratejisi
    ///
    /// ATR Mantığı:
    /// - Piyasa volatilitesini ölçer
    /// - Kırılım seviyeleri için bant oluşturur
    ///
    /// Trading Logic (choice=0):
    /// - AL: Fiyat üst bandı (MA + ATR*çarpan) yukarı kırıyor
    /// - SAT: Fiyat alt bandı (MA - ATR*çarpan) aşağı kırıyor
    ///
    /// Trading Logic (choice=1):
    /// - (İleride eklenecek alternatif sinyal mantığı)
    ///
    /// Parametreler:
    /// - atrPeriod: ATR periyodu (varsayılan 14)
    /// - maPeriod: MA periyodu (varsayılan 20)
    /// - multiplier: ATR çarpanı (varsayılan 2.0)
    /// - choice: Sinyal mantığı seçimi (varsayılan 0)
    /// </summary>
    public class SimpleATRStrategy : BaseStrategy
    {
        public override string Name => "Simple ATR Strategy";

        private readonly int _atrPeriod;
        private readonly int _maPeriod;
        private readonly double _multiplier;
        private readonly int _choice;
        private double[]? _atr;
        private double[]? _ma;
        private double[]? _upperBand;
        private double[]? _lowerBand;

        public SimpleATRStrategy(int atrPeriod = 14, int maPeriod = 20, double multiplier = 2.0, int choice = 0)
        {
            _atrPeriod = atrPeriod;
            _maPeriod = maPeriod;
            _multiplier = multiplier;
            _choice = choice;

            Parameters["ATRPeriod"] = atrPeriod;
            Parameters["MAPeriod"] = maPeriod;
            Parameters["Multiplier"] = multiplier;
            Parameters["Choice"] = choice;
        }

        public SimpleATRStrategy(List<StockData> data, IndicatorManager indicators, int atrPeriod = 14, int maPeriod = 20, double multiplier = 2.0, int choice = 0)
        {
            _atrPeriod = atrPeriod;
            _maPeriod = maPeriod;
            _multiplier = multiplier;
            _choice = choice;

            Parameters["ATRPeriod"] = atrPeriod;
            Parameters["MAPeriod"] = maPeriod;
            Parameters["Multiplier"] = multiplier;
            Parameters["Choice"] = choice;

            // Initialize base strategy
            // Initialize(data, indicators);
        }

        public override void OnInit()
        {
            if (!IsInitialized)
                return;

            var closes = Indicators.GetClosePrices();
            _atr = Indicators.Volatility.ATR(_atrPeriod);
            _ma = Indicators.MA.SMA(closes, _maPeriod);

            int length = closes.Length;
            _upperBand = new double[length];
            _lowerBand = new double[length];

            for (int i = 0; i < length; i++)
            {
                if (double.IsNaN(_ma[i]) || double.IsNaN(_atr[i]))
                {
                    _upperBand[i] = double.NaN;
                    _lowerBand[i] = double.NaN;
                }
                else
                {
                    _upperBand[i] = _ma[i] + (_atr[i] * _multiplier);
                    _lowerBand[i] = _ma[i] - (_atr[i] * _multiplier);
                }
            }

            Log($"SimpleATRStrategy initialized: ATRPeriod={_atrPeriod}, MAPeriod={_maPeriod}, Multiplier={_multiplier}");
        }

        public override TradeSignals OnStep(int currentIndex)
        {
            bool buy = false;
            bool sell = false;
            bool takeProfit = false;
            bool stopLoss = false;
            bool flat = false;
            bool skip = false;

            int minPeriod = Math.Max(_atrPeriod, _maPeriod) + 1;
            if (currentIndex < minPeriod)
                return TradeSignals.None;

            if (_upperBand == null || _lowerBand == null || _upperBand.Length == 0)
                return TradeSignals.None;

            double currentClose = Data[currentIndex].Close;
            double prevClose = Data[currentIndex - 1].Close;
            double currentUpper = _upperBand[currentIndex];
            double prevUpper = _upperBand[currentIndex - 1];
            double currentLower = _lowerBand[currentIndex];
            double prevLower = _lowerBand[currentIndex - 1];

            if (double.IsNaN(currentUpper) || double.IsNaN(currentLower))
                return TradeSignals.None;

            // ************************************************************************************************************************
            // choice: 0 = ATR band breakout, 1 = (İleride eklenecek)
            if (_choice == 0)
            {
                // AL: Fiyat üst bandı yukarı kırıyor
                if (prevClose <= prevUpper && currentClose > currentUpper)
                {
                    buy = true;
                }

                // SAT: Fiyat alt bandı aşağı kırıyor
                if (prevClose >= prevLower && currentClose < currentLower)
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

        public double[]? GetATR() => _atr;
        public double[]? GetUpperBand() => _upperBand;
        public double[]? GetLowerBand() => _lowerBand;

        public override Dictionary<string, double[]>? GetPlotIndicators()
        {
            var indicators = new Dictionary<string, double[]>();

            var closes = Indicators.GetClosePrices();
            if (closes != null && closes.Length > 0)
                indicators["Close"] = closes;

            if (_upperBand != null && _upperBand.Length > 0)
                indicators["ATR_Upper"] = _upperBand;

            if (_lowerBand != null && _lowerBand.Length > 0)
                indicators["ATR_Lower"] = _lowerBand;

            if (_ma != null && _ma.Length > 0)
                indicators["MA"] = _ma;

            return indicators.Count > 0 ? indicators : null;
        }
    }
}
