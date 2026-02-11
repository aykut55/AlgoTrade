using System;
using AlgoTrade.Core.Trading.Indicators.Base;

namespace AlgoTrade.Core.Trading.Indicators.Volume
{
    /// <summary>
    /// Volume Indicators - OBV, VWAP, MFI, CMF, A/D, etc.
    /// TODO: Implement remaining indicators
    /// </summary>
    public class VolumeIndicators
    {
        private readonly IndicatorManager _manager;
        private readonly IndicatorConfig _config;

        public VolumeIndicators(IndicatorManager manager, IndicatorConfig config)
        {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <summary>
        /// On Balance Volume
        /// Cumulative volume indicator that adds volume on up days and subtracts on down days
        /// OBV[i] = OBV[i-1] + (Close[i] > Close[i-1] ? Volume[i] : -Volume[i])
        /// Used to confirm price trends
        /// </summary>
        public double[] OBV()
        {
            if (!_manager.IsInitialized)
                throw new InvalidOperationException("Manager not initialized with data");

            var closes = _manager.GetClosePrices();
            var volumes = _manager.GetVolume();

            var obv = new double[closes.Length];

            if (closes.Length == 0)
                return obv;

            // First OBV value is just the first volume
            obv[0] = volumes[0];

            // Calculate cumulative OBV
            for (int i = 1; i < closes.Length; i++)
            {
                if (closes[i] > closes[i - 1])
                {
                    // Price up: add volume
                    obv[i] = obv[i - 1] + volumes[i];
                }
                else if (closes[i] < closes[i - 1])
                {
                    // Price down: subtract volume
                    obv[i] = obv[i - 1] - volumes[i];
                }
                else
                {
                    // Price unchanged: no change
                    obv[i] = obv[i - 1];
                }
            }

            return obv;
        }

        /// <summary>
        /// Volume Weighted Average Price
        /// Cumulative volume-weighted average of typical price
        /// VWAP = Sum(Typical Price * Volume) / Sum(Volume)
        /// Typical Price = (High + Low + Close) / 3
        /// Popular intraday indicator showing average price weighted by volume
        /// </summary>
        public double[] VWAP()
        {
            if (!_manager.IsInitialized)
                throw new InvalidOperationException("Manager not initialized with data");

            var highs = _manager.GetHighPrices();
            var lows = _manager.GetLowPrices();
            var closes = _manager.GetClosePrices();
            var volumes = _manager.GetVolume();

            var vwap = new double[closes.Length];

            if (closes.Length == 0)
                return vwap;

            double cumulativePriceVolume = 0;
            double cumulativeVolume = 0;

            for (int i = 0; i < closes.Length; i++)
            {
                // Typical Price = (H + L + C) / 3
                var typicalPrice = (highs[i] + lows[i] + closes[i]) / 3.0;

                cumulativePriceVolume += typicalPrice * volumes[i];
                cumulativeVolume += volumes[i];

                if (cumulativeVolume > 0)
                {
                    vwap[i] = cumulativePriceVolume / cumulativeVolume;
                }
                else
                {
                    vwap[i] = typicalPrice;
                }
            }

            return vwap;
        }

        /// <summary>
        /// Money Flow Index
        /// Volume-weighted RSI measuring buying/selling pressure
        /// MFI = 100 - (100 / (1 + Money Flow Ratio))
        /// Money Flow Ratio = Positive Money Flow / Negative Money Flow
        /// Money Flow = Typical Price * Volume
        /// Values 0-100: >80 overbought, <20 oversold
        /// </summary>
        public double[] MFI(int period = 14)
        {
            if (!_manager.IsInitialized)
                throw new InvalidOperationException("Manager not initialized with data");
            if (period <= 0)
                throw new ArgumentException("Period must be positive", nameof(period));

            var highs = _manager.GetHighPrices();
            var lows = _manager.GetLowPrices();
            var closes = _manager.GetClosePrices();
            var volumes = _manager.GetVolume();

            var mfi = new double[closes.Length];

            if (closes.Length < period + 1)
            {
                // Not enough data
                for (int i = 0; i < closes.Length; i++)
                    mfi[i] = double.NaN;
                return mfi;
            }

            // Calculate Typical Price and Money Flow
            var typicalPrice = new double[closes.Length];
            var moneyFlow = new double[closes.Length];

            for (int i = 0; i < closes.Length; i++)
            {
                typicalPrice[i] = (highs[i] + lows[i] + closes[i]) / 3.0;
                moneyFlow[i] = typicalPrice[i] * volumes[i];
            }

            // Initialize result with NaN
            for (int i = 0; i < period; i++)
            {
                mfi[i] = double.NaN;
            }

            // Calculate MFI
            for (int i = period; i < closes.Length; i++)
            {
                double positiveFlow = 0;
                double negativeFlow = 0;

                // Sum positive and negative money flows over period
                for (int j = 1; j <= period; j++)
                {
                    int idx = i - period + j;
                    if (typicalPrice[idx] > typicalPrice[idx - 1])
                    {
                        positiveFlow += moneyFlow[idx];
                    }
                    else if (typicalPrice[idx] < typicalPrice[idx - 1])
                    {
                        negativeFlow += moneyFlow[idx];
                    }
                    // If equal, neither positive nor negative
                }

                // Calculate MFI
                if (negativeFlow == 0)
                {
                    mfi[i] = 100.0;
                }
                else
                {
                    double moneyFlowRatio = positiveFlow / negativeFlow;
                    mfi[i] = 100.0 - (100.0 / (1.0 + moneyFlowRatio));
                }
            }

            return mfi;
        }

        /// <summary>
        /// Chaikin Money Flow
        /// Measures buying/selling pressure over a period
        /// CMF = Sum(Money Flow Volume) / Sum(Volume)
        /// Money Flow Multiplier = ((Close - Low) - (High - Close)) / (High - Low)
        /// Money Flow Volume = Money Flow Multiplier * Volume
        /// Values: +1 to -1 (positive = buying pressure, negative = selling pressure)
        /// </summary>
        public double[] CMF(int period = 20)
        {
            if (!_manager.IsInitialized)
                throw new InvalidOperationException("Manager not initialized with data");
            if (period <= 0)
                throw new ArgumentException("Period must be positive", nameof(period));

            var highs = _manager.GetHighPrices();
            var lows = _manager.GetLowPrices();
            var closes = _manager.GetClosePrices();
            var volumes = _manager.GetVolume();

            var cmf = new double[closes.Length];

            if (closes.Length < period)
            {
                // Not enough data
                for (int i = 0; i < closes.Length; i++)
                    cmf[i] = double.NaN;
                return cmf;
            }

            // Calculate Money Flow Volume for each bar
            var moneyFlowVolume = new double[closes.Length];

            for (int i = 0; i < closes.Length; i++)
            {
                var range = highs[i] - lows[i];

                if (range > 0)
                {
                    // Money Flow Multiplier = ((Close - Low) - (High - Close)) / (High - Low)
                    var moneyFlowMultiplier = ((closes[i] - lows[i]) - (highs[i] - closes[i])) / range;
                    moneyFlowVolume[i] = moneyFlowMultiplier * volumes[i];
                }
                else
                {
                    // No range, neutral
                    moneyFlowVolume[i] = 0;
                }
            }

            // Initialize result with NaN
            for (int i = 0; i < period - 1; i++)
            {
                cmf[i] = double.NaN;
            }

            // Calculate CMF
            for (int i = period - 1; i < closes.Length; i++)
            {
                double sumMoneyFlowVolume = 0;
                double sumVolume = 0;

                for (int j = 0; j < period; j++)
                {
                    sumMoneyFlowVolume += moneyFlowVolume[i - j];
                    sumVolume += volumes[i - j];
                }

                if (sumVolume > 0)
                {
                    cmf[i] = sumMoneyFlowVolume / sumVolume;
                }
                else
                {
                    cmf[i] = 0;
                }
            }

            return cmf;
        }
    }
}
