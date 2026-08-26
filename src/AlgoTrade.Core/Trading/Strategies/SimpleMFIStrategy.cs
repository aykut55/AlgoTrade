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
    /// MFI (Money Flow Index) Stratejisi
    ///
    /// MFI Mantığı:
    /// - Hacim ağırlıklı RSI benzeri indikatör
    /// - 0-100 arası, 80 üstü: Aşırı alım, 20 altı: Aşırı satım
    ///
    /// Trading Logic (choice=0):
    /// - AL: MFI 20 seviyesini yukarı kesiyor (aşırı satımdan çıkış)
    /// - SAT: MFI 80 seviyesini aşağı kesiyor (aşırı alımdan çıkış)
    ///
    /// Trading Logic (choice=1):
    /// - (İleride eklenecek alternatif sinyal mantığı)
    ///
    /// Parametreler:
    /// - period: MFI periyodu (varsayılan 14)
    /// - oversold: Aşırı satım seviyesi (varsayılan 20)
    /// - overbought: Aşırı alım seviyesi (varsayılan 80)
    /// - choice: Sinyal mantığı seçimi (varsayılan 0)
    /// </summary>
    public class SimpleMFIStrategy : BaseStrategy
    {
        public override string Name => "Simple MFI Strategy";

        private readonly int _period;
        private readonly double _oversold;
        private readonly double _overbought;
        private readonly int _choice;
        private double[]? _mfi;

        public SimpleMFIStrategy(int period = 14, double oversold = 20, double overbought = 80, int choice = 0)
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

        public SimpleMFIStrategy(List<StockData> data, IndicatorManager indicators, int period = 14, double oversold = 20, double overbought = 80, int choice = 0)
        {
            _period = period;
            _oversold = oversold;
            _overbought = overbought;
            _choice = choice;

            Parameters["Period"] = period;
            Parameters["Oversold"] = oversold;
            Parameters["Overbought"] = overbought;
            Parameters["Choice"] = choice;

            // Initialize base strategy
            Initialize(data, indicators);
        }

        public override void OnInit()
        {
            if (!IsInitialized)
                return;

            _mfi = Indicators.VolumeInd.MFI(_period);

            //Log($"SimpleMFIStrategy initialized: Period={_period}, Oversold={_oversold}, Overbought={_overbought}");
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

            if (_mfi == null || _mfi.Length == 0)
                return TradeSignals.None;

            double currentMFI = _mfi[currentIndex];
            double prevMFI = _mfi[currentIndex - 1];

            if (double.IsNaN(currentMFI) || double.IsNaN(prevMFI))
                return TradeSignals.None;

            // ************************************************************************************************************************
            // choice: 0 = MFI overbought/oversold crossover, 1 = (İleride eklenecek)
            if (_choice == 0)
            {
                // AL: MFI oversold seviyesini yukarı kesiyor
                if (prevMFI <= _oversold && currentMFI > _oversold)
                {
                    buy = true;
                }

                // SAT: MFI overbought seviyesini aşağı kesiyor
                if (prevMFI >= _overbought && currentMFI < _overbought)
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

        public double[]? GetMFI() => _mfi;

        public override Dictionary<string, double[]>? GetPlotIndicators()
        {
            var indicators = new Dictionary<string, double[]>();

            if (_mfi != null && _mfi.Length > 0)
                indicators["MFI"] = _mfi;

            return indicators.Count > 0 ? indicators : null;
        }
    }
}
