using System;
using AlgoTrade.Core.Trading.Indicators.Base;
using AlgoTrade.Core.Trading.Indicators.Trend.Results;

namespace AlgoTrade.Core.Trading.Indicators.Trend
{
    /// <summary>
    /// Trend Indicators - SuperTrend, MOST, ADX, Parabolic SAR, Ichimoku, etc.
    /// TODO: Implement remaining indicators
    /// </summary>
    public class TrendIndicators
    {
        private readonly IndicatorManager _manager;
        private readonly IndicatorConfig _config;

        public TrendIndicators(IndicatorManager manager, IndicatorConfig config)
        {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <summary>
        /// SuperTrend Indicator
        ///
        /// SuperTrend is an ATR-based trend-following indicator:
        /// - In uptrend: SuperTrend stays below price (support level)
        /// - In downtrend: SuperTrend stays above price (resistance level)
        ///
        /// Trading Logic:
        /// - Buy when direction changes from -1 to 1 (trend reversal to uptrend)
        /// - Sell when direction changes from 1 to -1 (trend reversal to downtrend)
        /// </summary>
        /// <param name="period">ATR period (default: 10)</param>
        /// <param name="multiplier">ATR multiplier for bands (default: 3.0)</param>
        /// <returns>SuperTrendResult containing supertrend values and direction</returns>
        public SuperTrendResult SuperTrend(int period = 10, double multiplier = 3.0)
        {
            int barCount = _manager.BarCount;
            if (barCount == 0)
                return new SuperTrendResult
                {
                    SuperTrend = new double[0],
                    Direction = new int[0]
                };

            // Get OHLC data
            double[] highs = _manager.GetHighPrices();
            double[] lows = _manager.GetLowPrices();
            double[] closes = _manager.GetClosePrices();

            // Calculate True Range
            double[] tr = new double[barCount];
            tr[0] = highs[0] - lows[0];

            for (int i = 1; i < barCount; i++)
            {
                double tr1 = highs[i] - lows[i];
                double tr2 = Math.Abs(highs[i] - closes[i - 1]);
                double tr3 = Math.Abs(lows[i] - closes[i - 1]);
                tr[i] = Math.Max(tr1, Math.Max(tr2, tr3));
            }

            // Calculate ATR (EMA of True Range)
            double[] atr = CalculateEMA(tr, period);

            // Calculate basic upper and lower bands
            double[] upperBand = new double[barCount];
            double[] lowerBand = new double[barCount];

            for (int i = 0; i < barCount; i++)
            {
                double hl2 = (highs[i] + lows[i]) / 2.0;
                upperBand[i] = hl2 + (multiplier * atr[i]);
                lowerBand[i] = hl2 - (multiplier * atr[i]);
            }

            // Calculate final upper and lower bands
            double[] finalUpperBand = new double[barCount];
            double[] finalLowerBand = new double[barCount];

            finalUpperBand[0] = upperBand[0];
            finalLowerBand[0] = lowerBand[0];

            for (int i = 1; i < barCount; i++)
            {
                // Final Upper Band
                if (closes[i - 1] <= finalUpperBand[i - 1])
                    finalUpperBand[i] = Math.Min(upperBand[i], finalUpperBand[i - 1]);
                else
                    finalUpperBand[i] = upperBand[i];

                // Final Lower Band
                if (closes[i - 1] >= finalLowerBand[i - 1])
                    finalLowerBand[i] = Math.Max(lowerBand[i], finalLowerBand[i - 1]);
                else
                    finalLowerBand[i] = lowerBand[i];
            }

            // Calculate SuperTrend and Direction
            double[] supertrend = new double[barCount];
            int[] direction = new int[barCount];

            supertrend[0] = finalLowerBand[0];
            direction[0] = 1;

            for (int i = 1; i < barCount; i++)
            {
                // Determine direction
                if (closes[i] > finalUpperBand[i - 1])
                    direction[i] = 1;
                else if (closes[i] < finalLowerBand[i - 1])
                    direction[i] = -1;
                else
                {
                    direction[i] = direction[i - 1];

                    // Adjust bands if direction continues
                    if (direction[i] == 1 && finalLowerBand[i] > finalLowerBand[i - 1])
                        finalLowerBand[i] = finalLowerBand[i - 1];

                    if (direction[i] == -1 && finalUpperBand[i] < finalUpperBand[i - 1])
                        finalUpperBand[i] = finalUpperBand[i - 1];
                }

                // Set SuperTrend value based on direction
                supertrend[i] = direction[i] == 1 ? finalLowerBand[i] : finalUpperBand[i];
            }

            return new SuperTrendResult
            {
                SuperTrend = supertrend,
                Direction = direction
            };
        }

        /// <summary>
        /// Helper method to calculate EMA (internal use)
        /// </summary>
        private double[] CalculateEMA(double[] source, int period)
        {
            if (source.Length == 0)
                return new double[0];

            double alpha = 2.0 / (period + 1);
            double[] ema = new double[source.Length];

            // First value is the source value itself
            ema[0] = source[0];

            // Calculate EMA
            for (int i = 1; i < source.Length; i++)
            {
                ema[i] = alpha * source[i] + (1 - alpha) * ema[i - 1];
            }

            return ema;
        }

        /// <summary>
        /// MOSTLegacy - MOST'un eski/sabit sürümü. Kaynak HEP close, MA HEP EMA.
        /// Parametrik kaynak/MA tipi isteyen yeni <see cref="MOST"/> metodunu kullan;
        /// bu metot geriye dönük uyumluluk için bırakıldı.
        ///
        /// MOST bir trailing stop loss trend indikatörüdür:
        /// - Yükselişte: MOST fiyatın altında (destek)
        /// - Düşüşte: MOST fiyatın üstünde (direnç)
        /// - Fiyat MOST'u yukarı kırınca AL, aşağı kırınca SAT
        /// </summary>
        /// <param name="period">EMA periyodu (varsayılan: 21)</param>
        /// <param name="percent">Bant yüzdesi (varsayılan: 1.0)</param>
        /// <returns>most ve exmov dizilerini içeren MOSTResult</returns>
        public MOSTResult MOSTLegacy(int period = 21, double percent = 1.0)
        {
            int barCount = _manager.BarCount;
            if (barCount == 0)
                return new MOSTResult(new double[0], new double[0], period, percent);

            // Get close prices
            double[] closes = _manager.GetClosePrices();

            // Calculate EMA (exmov)
            double[] exmov = _manager.MA.EMA(closes, period);

            // Calculate bands
            double[] fark = new double[barCount];
            double[] up = new double[barCount];
            double[] down = new double[barCount];

            for (int i = 0; i < barCount; i++)
            {
                fark[i] = exmov[i] * (percent / 100.0);
                up[i] = exmov[i] - fark[i];
                down[i] = exmov[i] + fark[i];
            }

            // Initialize arrays
            double[] trendUp = new double[barCount];
            double[] trendDown = new double[barCount];
            int[] trend = new int[barCount];
            double[] most = new double[barCount];

            // Initialize first values
            trendUp[0] = up[0];
            trendDown[0] = down[0];
            trend[0] = 1;
            most[0] = trendUp[0];

            // Calculate MOST for each bar
            for (int i = 1; i < barCount; i++)
            {
                // Pine Script: TrendUp = prev(ExMov,1)>prev(TrendUp,1) ? max(Up,prev(TrendUp,1)) : Up
                if (exmov[i - 1] > trendUp[i - 1])
                    trendUp[i] = Math.Max(up[i], trendUp[i - 1]);
                else
                    trendUp[i] = up[i];

                // Pine Script: TrendDown = prev(ExMov,1)<prev(TrendDown,1) ? min(Down,prev(TrendDown,1)) : Down
                if (exmov[i - 1] < trendDown[i - 1])
                    trendDown[i] = Math.Min(down[i], trendDown[i - 1]);
                else
                    trendDown[i] = down[i];

                // Pine Script: Trend = ExMov>prev(TrendDown,1) ? 1 : ExMov<prev(TrendUp,1) ? -1 : prev(Trend,1)
                if (exmov[i] > trendDown[i - 1])
                    trend[i] = 1;
                else if (exmov[i] < trendUp[i - 1])
                    trend[i] = -1;
                else
                    trend[i] = trend[i - 1];

                // Pine Script: MOST = Trend==1 ? TrendUp : TrendDown
                most[i] = trend[i] == 1 ? trendUp[i] : trendDown[i];
            }

            return new MOSTResult(most, exmov, period, percent);
        }

        /// <summary>
        /// MOST (Moving Stop Loss) - trailing stop loss trend indikatörü.
        /// - Yükselişte MOST fiyatın altında (destek), düşüşte üstünde (direnç)
        /// - Fiyat MOST'u yukarı kırınca AL, aşağı kırınca SAT
        ///
        /// EXMOV hesabı iki noktada parametrik (varsayılanlarla çıktı <see cref="MOSTLegacy"/> ile birebir aynı):
        /// - <paramref name="source"/>: EXMOV'un besleneceği fiyat serisi (close/open/hl2/hlc3/ohlc4...) - varsayılan Close
        /// - <paramref name="maMethod"/>: EXMOV'un MA tipi (EMA/SMA/WMA/HULL/DEMA/TEMA/VIDYA/ZLEMA...) - varsayılan EMA
        ///   (Anıl Özekşi'nin orijinal MOST'undaki MAvType seçeneği).
        ///
        /// Bantlar ve trend/trailing-stop mantığı klasik MOST ile birebir aynı.
        /// </summary>
        /// <param name="period">EXMOV periyodu (varsayılan: 21)</param>
        /// <param name="percent">Bant yüzdesi (varsayılan: 1.0)</param>
        /// <param name="maMethod">EXMOV hareketli ortalama tipi (varsayılan: EMA)</param>
        /// <param name="source">Fiyat kaynağı (varsayılan: Close)</param>
        /// <returns>most ve exmov dizilerini içeren MOSTResult</returns>
        public MOSTResult MOST(int period = 21, double percent = 1.0, MAMethod maMethod = MAMethod.EMA, PriceSource source = PriceSource.Close)
        {
            int barCount = _manager.BarCount;
            if (barCount == 0)
                return new MOSTResult(new double[0], new double[0], period, percent);

            // Kaynak seri + seçilen MA tipiyle EXMOV
            double[] src = ResolvePriceSource(source);
            double[] exmov = _manager.MA.Calculate(src, maMethod, period);

            // Bantlar
            double[] fark = new double[barCount];
            double[] up = new double[barCount];
            double[] down = new double[barCount];

            for (int i = 0; i < barCount; i++)
            {
                fark[i] = exmov[i] * (percent / 100.0);
                up[i] = exmov[i] - fark[i];
                down[i] = exmov[i] + fark[i];
            }

            double[] trendUp = new double[barCount];
            double[] trendDown = new double[barCount];
            int[] trend = new int[barCount];
            double[] most = new double[barCount];

            trendUp[0] = up[0];
            trendDown[0] = down[0];
            trend[0] = 1;
            most[0] = trendUp[0];

            for (int i = 1; i < barCount; i++)
            {
                if (exmov[i - 1] > trendUp[i - 1])
                    trendUp[i] = Math.Max(up[i], trendUp[i - 1]);
                else
                    trendUp[i] = up[i];

                if (exmov[i - 1] < trendDown[i - 1])
                    trendDown[i] = Math.Min(down[i], trendDown[i - 1]);
                else
                    trendDown[i] = down[i];

                if (exmov[i] > trendDown[i - 1])
                    trend[i] = 1;
                else if (exmov[i] < trendUp[i - 1])
                    trend[i] = -1;
                else
                    trend[i] = trend[i - 1];

                most[i] = trend[i] == 1 ? trendUp[i] : trendDown[i];
            }

            return new MOSTResult(most, exmov, period, percent);
        }

        /// <summary>
        /// <see cref="PriceSource"/> enum'unu bar-bar fiyat dizisine çevirir.
        /// </summary>
        public double[] ResolvePriceSource(PriceSource source)
        {
            switch (source)
            {
                case PriceSource.Close: return _manager.GetClosePrices();
                case PriceSource.Open:  return _manager.GetOpenPrices();
                case PriceSource.High:  return _manager.GetHighPrices();
                case PriceSource.Low:   return _manager.GetLowPrices();
            }

            double[] o = _manager.GetOpenPrices();
            double[] h = _manager.GetHighPrices();
            double[] l = _manager.GetLowPrices();
            double[] c = _manager.GetClosePrices();
            int n = c.Length;
            double[] result = new double[n];

            for (int i = 0; i < n; i++)
            {
                result[i] = source switch
                {
                    PriceSource.Median   => (h[i] + l[i]) / 2.0,                // HL2
                    PriceSource.Typical  => (h[i] + l[i] + c[i]) / 3.0,         // HLC3
                    PriceSource.Weighted => (h[i] + l[i] + c[i] + c[i]) / 4.0,  // HLCC4
                    PriceSource.Average  => (o[i] + h[i] + l[i] + c[i]) / 4.0,  // OHLC4
                    _ => c[i]
                };
            }

            return result;
        }

        /// <summary>
        /// Average Directional Index
        /// Measures trend strength (not direction)
        /// ADX > 25 = strong trend, ADX < 20 = weak trend/ranging
        /// Returns only ADX values
        /// </summary>
        public double[] ADX(int period = 14)
        {
            var result = ADXWithDI(period);
            return result.ADX;
        }

        /// <summary>
        /// Average Directional Index with Directional Indicators
        /// Returns ADX along with +DI and -DI for complete directional analysis
        /// +DI > -DI = uptrend, -DI > +DI = downtrend
        /// </summary>
        public ADXResult ADXWithDI(int period = 14)
        {
            if (!_manager.IsInitialized)
                throw new InvalidOperationException("Manager not initialized with data");
            if (period <= 0)
                throw new ArgumentException("Period must be positive", nameof(period));

            var highs = _manager.GetHighPrices();
            var lows = _manager.GetLowPrices();
            var closes = _manager.GetClosePrices();

            var length = closes.Length;
            var adx = new double[length];
            var plusDI = new double[length];
            var minusDI = new double[length];

            if (length < period + 1)
            {
                for (int i = 0; i < length; i++)
                {
                    adx[i] = double.NaN;
                    plusDI[i] = double.NaN;
                    minusDI[i] = double.NaN;
                }
                return new ADXResult(adx, plusDI, minusDI, period);
            }

            // Calculate True Range and Directional Movements
            var tr = new double[length];
            var plusDM = new double[length];
            var minusDM = new double[length];

            tr[0] = highs[0] - lows[0];
            plusDM[0] = 0;
            minusDM[0] = 0;

            for (int i = 1; i < length; i++)
            {
                // True Range
                var tr1 = highs[i] - lows[i];
                var tr2 = Math.Abs(highs[i] - closes[i - 1]);
                var tr3 = Math.Abs(lows[i] - closes[i - 1]);
                tr[i] = Math.Max(tr1, Math.Max(tr2, tr3));

                // Directional Movements
                var highDiff = highs[i] - highs[i - 1];
                var lowDiff = lows[i - 1] - lows[i];

                if (highDiff > lowDiff && highDiff > 0)
                    plusDM[i] = highDiff;
                else
                    plusDM[i] = 0;

                if (lowDiff > highDiff && lowDiff > 0)
                    minusDM[i] = lowDiff;
                else
                    minusDM[i] = 0;
            }

            // Apply Wilder's smoothing
            var smoothedTR = _manager.MA.Wilder(tr, period);
            var smoothedPlusDM = _manager.MA.Wilder(plusDM, period);
            var smoothedMinusDM = _manager.MA.Wilder(minusDM, period);

            // Calculate +DI and -DI
            var dx = new double[length];

            for (int i = 0; i < length; i++)
            {
                if (i < period - 1)
                {
                    plusDI[i] = double.NaN;
                    minusDI[i] = double.NaN;
                    dx[i] = 0;
                    adx[i] = double.NaN;
                }
                else
                {
                    if (smoothedTR[i] > 0)
                    {
                        plusDI[i] = 100.0 * smoothedPlusDM[i] / smoothedTR[i];
                        minusDI[i] = 100.0 * smoothedMinusDM[i] / smoothedTR[i];
                    }
                    else
                    {
                        plusDI[i] = 0;
                        minusDI[i] = 0;
                    }

                    // Calculate DX
                    var diSum = plusDI[i] + minusDI[i];
                    if (diSum > 0)
                    {
                        dx[i] = 100.0 * Math.Abs(plusDI[i] - minusDI[i]) / diSum;
                    }
                    else
                    {
                        dx[i] = 0;
                    }
                }
            }

            // Calculate ADX (Wilder's smoothing of DX)
            var smoothedDX = _manager.MA.Wilder(dx, period);

            for (int i = 0; i < length; i++)
            {
                if (i < (period * 2) - 2)
                {
                    adx[i] = double.NaN;
                }
                else
                {
                    adx[i] = smoothedDX[i];
                }
            }

            return new ADXResult(adx, plusDI, minusDI, period);
        }

        /// <summary>
        /// Parabolic SAR (Stop and Reverse)
        /// Trailing stop indicator that follows price
        /// SAR below price = uptrend, SAR above price = downtrend
        /// Trend reverses when price crosses SAR
        /// </summary>
        /// <param name="step">Acceleration factor step (default: 0.02)</param>
        /// <param name="max">Maximum acceleration factor (default: 0.2)</param>
        /// <returns>ParabolicSARResult containing sar values and trend direction</returns>
        public ParabolicSARResult ParabolicSAR(double step = 0.02, double max = 0.2)
        {
            if (!_manager.IsInitialized)
                throw new InvalidOperationException("Manager not initialized with data");
            if (step <= 0 || max <= 0)
                throw new ArgumentException("Step and max must be positive");
            if (step > max)
                throw new ArgumentException("Step cannot be greater than max");

            var highs = _manager.GetHighPrices();
            var lows = _manager.GetLowPrices();
            var closes = _manager.GetClosePrices();

            var length = closes.Length;
            var sar = new double[length];
            var trend = new bool[length]; // true = uptrend, false = downtrend

            if (length < 2)
            {
                if (length == 1)
                {
                    sar[0] = closes[0];
                    trend[0] = true;
                }
                return new ParabolicSARResult(sar, trend, step, max);
            }

            // Initialize
            var af = step; // Acceleration Factor
            var ep = highs[0]; // Extreme Point
            trend[0] = true; // Start with uptrend
            sar[0] = lows[0];

            for (int i = 1; i < length; i++)
            {
                // Calculate SAR for current bar
                sar[i] = sar[i - 1] + af * (ep - sar[i - 1]);

                var isUptrend = trend[i - 1];

                if (isUptrend)
                {
                    // Uptrend: SAR should be below price
                    // SAR cannot be above prior two lows
                    if (i >= 1 && sar[i] > lows[i - 1])
                        sar[i] = lows[i - 1];
                    if (i >= 2 && sar[i] > lows[i - 2])
                        sar[i] = lows[i - 2];

                    // Check for trend reversal
                    if (lows[i] < sar[i])
                    {
                        // Trend reverses to downtrend
                        trend[i] = false;
                        sar[i] = ep; // SAR becomes previous EP
                        ep = lows[i]; // EP becomes current low
                        af = step; // Reset AF
                    }
                    else
                    {
                        // Continue uptrend
                        trend[i] = true;

                        // Update EP if new high
                        if (highs[i] > ep)
                        {
                            ep = highs[i];
                            af = Math.Min(af + step, max);
                        }
                    }
                }
                else
                {
                    // Downtrend: SAR should be above price
                    // SAR cannot be below prior two highs
                    if (i >= 1 && sar[i] < highs[i - 1])
                        sar[i] = highs[i - 1];
                    if (i >= 2 && sar[i] < highs[i - 2])
                        sar[i] = highs[i - 2];

                    // Check for trend reversal
                    if (highs[i] > sar[i])
                    {
                        // Trend reverses to uptrend
                        trend[i] = true;
                        sar[i] = ep; // SAR becomes previous EP
                        ep = highs[i]; // EP becomes current high
                        af = step; // Reset AF
                    }
                    else
                    {
                        // Continue downtrend
                        trend[i] = false;

                        // Update EP if new low
                        if (lows[i] < ep)
                        {
                            ep = lows[i];
                            af = Math.Min(af + step, max);
                        }
                    }
                }
            }

            return new ParabolicSARResult(sar, trend, step, max);
        }

        /// <summary>
        /// Aroon Indicator
        /// Identifies trend strength and potential reversals
        /// Aroon Up = ((period - periods since highest high) / period) * 100
        /// Aroon Down = ((period - periods since lowest low) / period) * 100
        /// Values range 0-100:
        /// - Aroon Up > 70 with Aroon Down < 30 = strong uptrend
        /// - Aroon Down > 70 with Aroon Up < 30 = strong downtrend
        /// - Both near 50 = consolidation/weak trend
        /// </summary>
        /// <param name="period">Lookback period (default: 25)</param>
        /// <returns>AroonResult containing aroon up and aroon down values</returns>
        public AroonResult Aroon(int period = 25)
        {
            if (!_manager.IsInitialized)
                throw new InvalidOperationException("Manager not initialized with data");
            if (period <= 0)
                throw new ArgumentException("Period must be positive", nameof(period));

            var highs = _manager.GetHighPrices();
            var lows = _manager.GetLowPrices();

            var length = highs.Length;
            var aroonUp = new double[length];
            var aroonDown = new double[length];

            if (length < period)
            {
                for (int i = 0; i < length; i++)
                {
                    aroonUp[i] = double.NaN;
                    aroonDown[i] = double.NaN;
                }
                return new AroonResult(aroonUp, aroonDown, period);
            }

            // Initialize result with NaN for warmup period
            for (int i = 0; i < period - 1; i++)
            {
                aroonUp[i] = double.NaN;
                aroonDown[i] = double.NaN;
            }

            // Calculate Aroon for each bar
            for (int i = period - 1; i < length; i++)
            {
                // Find periods since highest high and lowest low
                int periodsSinceHigh = 0;
                int periodsSinceLow = 0;
                double highestHigh = highs[i];
                double lowestLow = lows[i];

                // Look back over the period
                for (int j = 0; j < period; j++)
                {
                    int idx = i - j;

                    if (highs[idx] >= highestHigh)
                    {
                        highestHigh = highs[idx];
                        periodsSinceHigh = j;
                    }

                    if (lows[idx] <= lowestLow)
                    {
                        lowestLow = lows[idx];
                        periodsSinceLow = j;
                    }
                }

                // Calculate Aroon Up and Down
                aroonUp[i] = ((double)(period - periodsSinceHigh) / period) * 100.0;
                aroonDown[i] = ((double)(period - periodsSinceLow) / period) * 100.0;
            }

            return new AroonResult(aroonUp, aroonDown, period);
        }

        /// <summary>
        /// Vortex Indicator
        /// Identifies trend reversals and confirms trend direction
        /// VI+ = Sum(|High - Previous Low|) / Sum(True Range)
        /// VI- = Sum(|Low - Previous High|) / Sum(True Range)
        /// Trading signals:
        /// - VI+ crosses above VI- = bullish signal (buy)
        /// - VI- crosses above VI+ = bearish signal (sell)
        /// - VI+ > VI- = uptrend, VI- > VI+ = downtrend
        /// </summary>
        /// <param name="period">Lookback period (default: 14)</param>
        /// <returns>VortexResult containing VI+ and VI- values</returns>
        public VortexResult Vortex(int period = 14)
        {
            if (!_manager.IsInitialized)
                throw new InvalidOperationException("Manager not initialized with data");
            if (period <= 0)
                throw new ArgumentException("Period must be positive", nameof(period));

            var highs = _manager.GetHighPrices();
            var lows = _manager.GetLowPrices();
            var closes = _manager.GetClosePrices();

            var length = closes.Length;
            var viPlus = new double[length];
            var viMinus = new double[length];

            if (length < period + 1)
            {
                for (int i = 0; i < length; i++)
                {
                    viPlus[i] = double.NaN;
                    viMinus[i] = double.NaN;
                }
                return new VortexResult(viPlus, viMinus, period);
            }

            // Calculate Vortex Movements and True Range
            var vmPlus = new double[length];
            var vmMinus = new double[length];
            var tr = new double[length];

            // First bar has no vortex movement
            vmPlus[0] = 0;
            vmMinus[0] = 0;
            tr[0] = highs[0] - lows[0];

            for (int i = 1; i < length; i++)
            {
                // Vortex Movement+: |Current High - Previous Low|
                vmPlus[i] = Math.Abs(highs[i] - lows[i - 1]);

                // Vortex Movement-: |Current Low - Previous High|
                vmMinus[i] = Math.Abs(lows[i] - highs[i - 1]);

                // True Range
                var tr1 = highs[i] - lows[i];
                var tr2 = Math.Abs(highs[i] - closes[i - 1]);
                var tr3 = Math.Abs(lows[i] - closes[i - 1]);
                tr[i] = Math.Max(tr1, Math.Max(tr2, tr3));
            }

            // Initialize result with NaN for warmup period
            for (int i = 0; i < period; i++)
            {
                viPlus[i] = double.NaN;
                viMinus[i] = double.NaN;
            }

            // Calculate Vortex Indicator
            for (int i = period; i < length; i++)
            {
                double sumVMPlus = 0;
                double sumVMMinus = 0;
                double sumTR = 0;

                // Sum over the period
                for (int j = 0; j < period; j++)
                {
                    int idx = i - j;
                    sumVMPlus += vmPlus[idx];
                    sumVMMinus += vmMinus[idx];
                    sumTR += tr[idx];
                }

                // Calculate VI+ and VI-
                if (sumTR > 0)
                {
                    viPlus[i] = sumVMPlus / sumTR;
                    viMinus[i] = sumVMMinus / sumTR;
                }
                else
                {
                    viPlus[i] = 0;
                    viMinus[i] = 0;
                }
            }

            return new VortexResult(viPlus, viMinus, period);
        }

        /// <summary>
        /// Ichimoku Cloud (Ichimoku Kinko Hyo)
        /// Complete trend-following system with multiple components
        /// Components:
        /// - Tenkan-sen (Conversion Line): (9-high + 9-low) / 2
        /// - Kijun-sen (Base Line): (26-high + 26-low) / 2
        /// - Senkou Span A (Leading Span A): (Tenkan + Kijun) / 2, shifted 26 ahead
        /// - Senkou Span B (Leading Span B): (52-high + 52-low) / 2, shifted 26 ahead
        /// - Chikou Span (Lagging Span): Close, shifted 26 back
        /// Trading signals:
        /// - Price above cloud = uptrend, below cloud = downtrend
        /// - Tenkan crosses above Kijun = buy signal
        /// - Tenkan crosses below Kijun = sell signal
        /// - Cloud color (Senkou A vs B) indicates trend strength
        /// </summary>
        /// <param name="tenkanPeriod">Tenkan-sen period (default: 9)</param>
        /// <param name="kijunPeriod">Kijun-sen period (default: 26)</param>
        /// <param name="senkouPeriod">Senkou Span B period (default: 52)</param>
        /// <param name="displacement">Cloud displacement (default: 26)</param>
        /// <returns>IchimokuResult containing all components</returns>
        public IchimokuResult Ichimoku(int tenkanPeriod = 9, int kijunPeriod = 26, int senkouPeriod = 52, int displacement = 26)
        {
            if (!_manager.IsInitialized)
                throw new InvalidOperationException("Manager not initialized with data");
            if (tenkanPeriod <= 0 || kijunPeriod <= 0 || senkouPeriod <= 0 || displacement < 0)
                throw new ArgumentException("Periods must be positive and displacement non-negative");

            var highs = _manager.GetHighPrices();
            var lows = _manager.GetLowPrices();
            var closes = _manager.GetClosePrices();

            var length = closes.Length;
            var tenkan = new double[length];
            var kijun = new double[length];
            var senkouA = new double[length];
            var senkouB = new double[length];
            var chikou = new double[length];

            // Helper function to calculate midpoint of highest high and lowest low
            double CalculateMidpoint(int index, int period)
            {
                if (index < period - 1)
                    return double.NaN;

                double highest = highs[index];
                double lowest = lows[index];

                for (int j = 1; j < period; j++)
                {
                    int idx = index - j;
                    if (highs[idx] > highest)
                        highest = highs[idx];
                    if (lows[idx] < lowest)
                        lowest = lows[idx];
                }

                return (highest + lowest) / 2.0;
            }

            // Calculate Tenkan-sen (Conversion Line)
            for (int i = 0; i < length; i++)
            {
                tenkan[i] = CalculateMidpoint(i, tenkanPeriod);
            }

            // Calculate Kijun-sen (Base Line)
            for (int i = 0; i < length; i++)
            {
                kijun[i] = CalculateMidpoint(i, kijunPeriod);
            }

            // Calculate Senkou Span B (Leading Span B) - 52-period midpoint
            var senkouBBase = new double[length];
            for (int i = 0; i < length; i++)
            {
                senkouBBase[i] = CalculateMidpoint(i, senkouPeriod);
            }

            // Calculate Senkou Span A (Leading Span A) - (Tenkan + Kijun) / 2
            var senkouABase = new double[length];
            for (int i = 0; i < length; i++)
            {
                if (double.IsNaN(tenkan[i]) || double.IsNaN(kijun[i]))
                    senkouABase[i] = double.NaN;
                else
                    senkouABase[i] = (tenkan[i] + kijun[i]) / 2.0;
            }

            // Shift Senkou Spans forward by displacement periods
            for (int i = 0; i < length; i++)
            {
                senkouA[i] = double.NaN;
                senkouB[i] = double.NaN;
            }

            for (int i = 0; i < length - displacement; i++)
            {
                int targetIdx = i + displacement;
                if (targetIdx < length)
                {
                    senkouA[targetIdx] = senkouABase[i];
                    senkouB[targetIdx] = senkouBBase[i];
                }
            }

            // Calculate Chikou Span (Lagging Span) - Close shifted back
            for (int i = 0; i < length; i++)
            {
                if (i >= displacement)
                {
                    chikou[i - displacement] = closes[i];
                }
            }

            // Fill remaining chikou values with NaN
            for (int i = length - displacement; i < length; i++)
            {
                if (i >= 0)
                    chikou[i] = double.NaN;
            }

            return new IchimokuResult(tenkan, kijun, senkouA, senkouB, chikou,
                tenkanPeriod, kijunPeriod, senkouPeriod, displacement);
        }

        /// <summary>
        /// AlphaTrend Indicator
        /// Combines ATR for volatility, MFI/RSI for momentum
        /// Acts as dynamic support/resistance using trailing stops
        /// Formula: AlphaTrend = if(MOM>=50, max(L-ATR*coeff, prev), min(H+ATR*coeff, prev))
        /// Trading signals:
        /// - Buy when prices above AlphaTrend (green filling)
        /// - Sell when prices below AlphaTrend (red filling)
        /// </summary>
        /// <param name="atrPeriod">ATR period (default: 14)</param>
        /// <param name="coefficient">ATR multiplier (default: 1.0)</param>
        /// <param name="momentumPeriod">MFI/RSI period (default: 14)</param>
        /// <param name="useMFI">Use MFI instead of RSI (default: true, requires volume data)</param>
        /// <returns>AlphaTrendResult containing alphatrend values</returns>
        public AlphaTrendResult AlphaTrend(int atrPeriod = 14, double coefficient = 1.0, int momentumPeriod = 14, bool useMFI = true)
        {
            if (!_manager.IsInitialized)
                throw new InvalidOperationException("Manager not initialized with data");
            if (atrPeriod <= 0 || momentumPeriod <= 0)
                throw new ArgumentException("Periods must be positive");
            if (coefficient <= 0)
                throw new ArgumentException("Coefficient must be positive");

            var highs = _manager.GetHighPrices();
            var lows = _manager.GetLowPrices();
            var closes = _manager.GetClosePrices();
            var length = closes.Length;

            // Calculate ATR
            var atr = _manager.Volatility.ATR(atrPeriod);

            // Calculate momentum (MFI or RSI)
            double[] momentum;
            if (useMFI)
            {
                try
                {
                    momentum = _manager.VolumeInd.MFI(momentumPeriod);
                }
                catch
                {
                    // Fall back to RSI if volume data not available
                    momentum = _manager.Momentum.RSI(closes, momentumPeriod).Values;
                }
            }
            else
            {
                momentum = _manager.Momentum.RSI(closes, momentumPeriod).Values;
            }

            // Calculate upT and downT
            var upT = new double[length];
            var downT = new double[length];

            for (int i = 0; i < length; i++)
            {
                upT[i] = lows[i] - atr[i] * coefficient;
                downT[i] = highs[i] + atr[i] * coefficient;
            }

            // Calculate AlphaTrend
            var alphaTrend = new double[length];
            alphaTrend[0] = upT[0];

            for (int i = 1; i < length; i++)
            {
                if (double.IsNaN(momentum[i]))
                {
                    alphaTrend[i] = alphaTrend[i - 1];
                    continue;
                }

                if (momentum[i] >= 50)
                {
                    // Uptrend
                    if (upT[i] < alphaTrend[i - 1])
                        alphaTrend[i] = alphaTrend[i - 1];
                    else
                        alphaTrend[i] = upT[i];
                }
                else
                {
                    // Downtrend
                    if (downT[i] > alphaTrend[i - 1])
                        alphaTrend[i] = alphaTrend[i - 1];
                    else
                        alphaTrend[i] = downT[i];
                }
            }

            return new AlphaTrendResult(alphaTrend, atrPeriod, coefficient, momentumPeriod);
        }

        /// <summary>
        /// OTT (Optimized Trend Tracker)
        /// Moving average-based trend tracker with ATR optimization
        /// More stable than immediate price reactions
        /// Formula: OTT = MA * (1 ± percent/100)
        /// Trading signals:
        /// - Buy when price > OTT or Support crosses above OTT
        /// - Sell when price < OTT or Support crosses below OTT
        /// </summary>
        /// <param name="period">Moving average period (default: 2)</param>
        /// <param name="percent">OTT optimization percent (default: 1.4)</param>
        /// <param name="maMethod">Moving average method (default: VAR/VIDYA)</param>
        /// <param name="source">Price source MA'yı besleyen seri (default: Close - klasik OTT)</param>
        /// <returns>OTTResult containing ott, ma, and support values</returns>
        public OTTResult OTT(int period = 2, double percent = 1.4, Base.MAMethod maMethod = Base.MAMethod.VIDYA, PriceSource source = PriceSource.Close)
        {
            if (!_manager.IsInitialized)
                throw new InvalidOperationException("Manager not initialized with data");
            if (period <= 0)
                throw new ArgumentException("Period must be positive");
            if (percent < 0)
                throw new ArgumentException("Percent must be non-negative");

            var src = ResolvePriceSource(source);
            var length = src.Length;

            // Calculate moving average
            var ma = _manager.MA.Calculate(src, maMethod, period);

            // Calculate OTT bands
            var fark = new double[length];
            var longStop = new double[length];
            var shortStop = new double[length];

            for (int i = 0; i < length; i++)
            {
                fark[i] = ma[i] * (percent / 100.0);
                longStop[i] = ma[i] - fark[i];
                shortStop[i] = ma[i] + fark[i];
            }

            // Calculate OTT and Support
            var ott = new double[length];
            var support = new double[length];
            int[] direction = new int[length];

            longStop[0] = ma[0];
            shortStop[0] = ma[0];
            ott[0] = ma[0];
            support[0] = ma[0];
            direction[0] = 1;

            for (int i = 1; i < length; i++)
            {
                if (double.IsNaN(ma[i]))
                {
                    ott[i] = double.NaN;
                    support[i] = double.NaN;
                    continue;
                }

                // Update longStop: if MA > prevLongStop, take max, otherwise use current
                if (ma[i - 1] > longStop[i - 1])
                    longStop[i] = Math.Max(longStop[i], longStop[i - 1]);

                // Update shortStop: if MA < prevShortStop, take min, otherwise use current
                if (ma[i - 1] < shortStop[i - 1])
                    shortStop[i] = Math.Min(shortStop[i], shortStop[i - 1]);

                // Determine direction
                if (ma[i] > shortStop[i - 1])
                    direction[i] = 1;  // Long
                else if (ma[i] < longStop[i - 1])
                    direction[i] = -1; // Short
                else
                    direction[i] = direction[i - 1]; // Continue previous

                // Set OTT based on direction
                ott[i] = direction[i] == 1 ? longStop[i] : shortStop[i];
                support[i] = direction[i] == 1 ? longStop[i] : shortStop[i];
            }

            return new OTTResult(ott, ma, support, period, percent);
        }

        /// <summary>
        /// PTT (Progressive Trend Tracker)
        /// Combines Bollinger Bands with Highest/Lowest values
        /// Uses HH/LL instead of close prices for band calculation
        /// Trading signals:
        /// - Buy when price crosses above PTT Lower line
        /// - Sell when price crosses below PTT Upper line
        /// </summary>
        /// <param name="fasterPeriod">Faster period (default: 5)</param>
        /// <param name="period">Period for HH/LL (default: 5)</param>
        /// <param name="maPeriod">Bollinger Band MA period (default: 2)</param>
        /// <param name="slowerPeriod">Slower period for final values (default: 10)</param>
        /// <param name="stdDev">Standard deviation multiplier (default: 2.0)</param>
        /// <returns>PTTResult containing upper and lower bands</returns>
        public PTTResult PTT(int fasterPeriod = 5, int period = 5, int maPeriod = 2, int slowerPeriod = 10, double stdDev = 2.0)
        {
            if (!_manager.IsInitialized)
                throw new InvalidOperationException("Manager not initialized with data");
            if (fasterPeriod <= 0 || period <= 0 || maPeriod <= 0 || slowerPeriod <= 0)
                throw new ArgumentException("Periods must be positive");

            var highs = _manager.GetHighPrices();
            var lows = _manager.GetLowPrices();
            var length = highs.Length;

            // Calculate HHV and LLV
            var hhv = _manager.Utils.HHV(highs, fasterPeriod);
            var llv = _manager.Utils.LLV(lows, fasterPeriod);

            // Calculate HH period values (HHV of HHV)
            var hhvPeriod = _manager.Utils.HHV(hhv, period);
            var llvPeriod = _manager.Utils.LLV(llv, period);

            // Calculate moving averages for bands
            var maUpper = _manager.MA.SMA(hhvPeriod, maPeriod);
            var maLower = _manager.MA.SMA(llvPeriod, maPeriod);

            // Calculate standard deviations
            var stdUpper = _manager.Utils.StdDev(hhvPeriod, maPeriod);
            var stdLower = _manager.Utils.StdDev(llvPeriod, maPeriod);

            // Calculate Bollinger-style bands
            var upperBand = new double[length];
            var lowerBand = new double[length];

            for (int i = 0; i < length; i++)
            {
                upperBand[i] = maUpper[i] + stdDev * stdUpper[i];
                lowerBand[i] = maLower[i] - stdDev * stdLower[i];
            }

            // Apply slower period (LLV for upper, HHV for lower)
            var pttUpper = _manager.Utils.LLV(upperBand, slowerPeriod);
            var pttLower = _manager.Utils.HHV(lowerBand, slowerPeriod);

            return new PTTResult(pttUpper, pttLower, fasterPeriod, period, maPeriod, slowerPeriod);
        }

        /// <summary>
        /// HOTT/LOTT (High/Low Optimized Trend Tracker)
        /// Separate OTT calculations for High and Low prices
        /// Provides dual trend tracking channels
        /// Trading signals:
        /// - HOTT acts as resistance in downtrends
        /// - LOTT acts as support in uptrends
        /// - Price between HOTT and LOTT = consolidation
        /// </summary>
        /// <param name="period">Moving average period (default: 2)</param>
        /// <param name="percent">OTT optimization percent (default: 1.4)</param>
        /// <param name="maMethod">Moving average method (default: VAR/VIDYA)</param>
        /// <returns>HOTTLOTTResult containing hott and lott values</returns>
        public HOTTLOTTResult HOTTLOTT(int period = 2, double percent = 1.4, Base.MAMethod maMethod = Base.MAMethod.VIDYA)
        {
            if (!_manager.IsInitialized)
                throw new InvalidOperationException("Manager not initialized with data");
            if (period <= 0)
                throw new ArgumentException("Period must be positive");

            var highs = _manager.GetHighPrices();
            var lows = _manager.GetLowPrices();
            var length = highs.Length;

            // Calculate MAs for high and low
            var maHigh = _manager.MA.Calculate(highs, maMethod, period);
            var maLow = _manager.MA.Calculate(lows, maMethod, period);

            // Calculate HOTT (OTT on highs)
            var hottFark = new double[length];
            var hottLongStop = new double[length];
            var hottShortStop = new double[length];

            for (int i = 0; i < length; i++)
            {
                hottFark[i] = maHigh[i] * (percent / 100.0);
                hottLongStop[i] = maHigh[i] - hottFark[i];
                hottShortStop[i] = maHigh[i] + hottFark[i];
            }

            // Calculate LOTT (OTT on lows)
            var lottFark = new double[length];
            var lottLongStop = new double[length];
            var lottShortStop = new double[length];

            for (int i = 0; i < length; i++)
            {
                lottFark[i] = maLow[i] * (percent / 100.0);
                lottLongStop[i] = maLow[i] - lottFark[i];
                lottShortStop[i] = maLow[i] + lottFark[i];
            }

            // Calculate HOTT
            var hott = new double[length];
            int[] hottDir = new int[length];
            hott[0] = maHigh[0];
            hottDir[0] = 1;

            for (int i = 1; i < length; i++)
            {
                if (double.IsNaN(maHigh[i]))
                {
                    hott[i] = double.NaN;
                    continue;
                }

                if (maHigh[i - 1] > hottLongStop[i - 1])
                    hottLongStop[i] = Math.Max(hottLongStop[i], hottLongStop[i - 1]);

                if (maHigh[i - 1] < hottShortStop[i - 1])
                    hottShortStop[i] = Math.Min(hottShortStop[i], hottShortStop[i - 1]);

                if (maHigh[i] > hottShortStop[i - 1])
                    hottDir[i] = 1;
                else if (maHigh[i] < hottLongStop[i - 1])
                    hottDir[i] = -1;
                else
                    hottDir[i] = hottDir[i - 1];

                hott[i] = hottDir[i] == 1 ? hottLongStop[i] : hottShortStop[i];
            }

            // Calculate LOTT
            var lott = new double[length];
            int[] lottDir = new int[length];
            lott[0] = maLow[0];
            lottDir[0] = 1;

            for (int i = 1; i < length; i++)
            {
                if (double.IsNaN(maLow[i]))
                {
                    lott[i] = double.NaN;
                    continue;
                }

                if (maLow[i - 1] > lottLongStop[i - 1])
                    lottLongStop[i] = Math.Max(lottLongStop[i], lottLongStop[i - 1]);

                if (maLow[i - 1] < lottShortStop[i - 1])
                    lottShortStop[i] = Math.Min(lottShortStop[i], lottShortStop[i - 1]);

                if (maLow[i] > lottShortStop[i - 1])
                    lottDir[i] = 1;
                else if (maLow[i] < lottLongStop[i - 1])
                    lottDir[i] = -1;
                else
                    lottDir[i] = lottDir[i - 1];

                lott[i] = lottDir[i] == 1 ? lottLongStop[i] : lottShortStop[i];
            }

            return new HOTTLOTTResult(hott, lott, maHigh, maLow, period, percent);
        }

        /// <summary>
        /// PMax (Profit Maximizer)
        /// Hybrid of MOST + SuperTrend with better performance
        /// Combines ATR-based trailing stops with moving averages
        /// Trading signals:
        /// - Buy when direction changes to 1 (bullish)
        /// - Sell when direction changes to -1 (bearish)
        /// - Fewer false signals than SuperTrend/MOST
        /// </summary>
        /// <param name="atrPeriod">ATR period (default: 10)</param>
        /// <param name="multiplier">ATR multiplier (default: 3.0)</param>
        /// <param name="maPeriod">Moving average period (default: 10)</param>
        /// <param name="maMethod">Moving average method (default: EMA)</param>
        /// <returns>PMaxResult containing pmax values and direction</returns>
        public PMaxResult PMax(int atrPeriod = 10, double multiplier = 3.0, int maPeriod = 10, Base.MAMethod maMethod = Base.MAMethod.EMA)
        {
            if (!_manager.IsInitialized)
                throw new InvalidOperationException("Manager not initialized with data");
            if (atrPeriod <= 0 || maPeriod <= 0)
                throw new ArgumentException("Periods must be positive");
            if (multiplier <= 0)
                throw new ArgumentException("Multiplier must be positive");

            var closes = _manager.GetClosePrices();
            var length = closes.Length;

            // Calculate MA
            var ma = _manager.MA.Calculate(closes, maMethod, maPeriod);

            // Calculate ATR
            var atr = _manager.Volatility.ATR(atrPeriod);

            // Calculate basic bands
            var upperBand = new double[length];
            var lowerBand = new double[length];

            for (int i = 0; i < length; i++)
            {
                upperBand[i] = ma[i] + (multiplier * atr[i]);
                lowerBand[i] = ma[i] - (multiplier * atr[i]);
            }

            // Calculate final bands (MOST-style)
            var finalUpperBand = new double[length];
            var finalLowerBand = new double[length];

            finalUpperBand[0] = upperBand[0];
            finalLowerBand[0] = lowerBand[0];

            for (int i = 1; i < length; i++)
            {
                // Final Upper Band: if MA > prev finalUpper, take max, else use current
                if (ma[i - 1] > finalUpperBand[i - 1])
                    finalUpperBand[i] = Math.Max(upperBand[i], finalUpperBand[i - 1]);
                else
                    finalUpperBand[i] = upperBand[i];

                // Final Lower Band: if MA < prev finalLower, take min, else use current
                if (ma[i - 1] < finalLowerBand[i - 1])
                    finalLowerBand[i] = Math.Min(lowerBand[i], finalLowerBand[i - 1]);
                else
                    finalLowerBand[i] = lowerBand[i];
            }

            // Calculate PMax and direction
            var pmax = new double[length];
            var direction = new int[length];

            pmax[0] = finalLowerBand[0];
            direction[0] = 1;

            for (int i = 1; i < length; i++)
            {
                // Determine direction
                if (ma[i] > finalUpperBand[i - 1])
                    direction[i] = 1;  // Bullish
                else if (ma[i] < finalLowerBand[i - 1])
                    direction[i] = -1; // Bearish
                else
                    direction[i] = direction[i - 1]; // Continue

                // Set PMax based on direction
                pmax[i] = direction[i] == 1 ? finalLowerBand[i] : finalUpperBand[i];
            }

            return new PMaxResult(pmax, ma, direction, atrPeriod, multiplier, maPeriod);
        }

        /// <summary>
        /// MavilimW Indicator
        /// Fibonacci-based weighted moving average combinations
        /// Functions as both trend indicator and support/resistance levels
        /// Color-coding: Blue = uptrend, Red = downtrend
        /// Trading signals:
        /// - Short-term: Buy when blue, Sell when red
        /// - Long-term: Buy when price crosses above, Sell when crosses below
        /// </summary>
        /// <param name="param1">First sensitivity parameter (default: 3, must be <= param2)</param>
        /// <param name="param2">Second sensitivity parameter (default: 5)</param>
        /// <returns>MavilimWResult containing mavilimw and trendline values</returns>
        public MavilimWResult MavilimW(int param1 = 3, int param2 = 5)
        {
            if (!_manager.IsInitialized)
                throw new InvalidOperationException("Manager not initialized with data");
            if (param1 <= 0 || param2 <= 0)
                throw new ArgumentException("Parameters must be positive");
            if (param1 > param2)
                throw new ArgumentException("Param1 must be <= Param2");

            var closes = _manager.GetClosePrices();
            var length = closes.Length;

            // Fibonacci levels
            double[] fibLevels = { 3, 5, 8, 10, 12, 15, 30, 35, 40, 45, 50, 55, 60, 65, 70, 75, 80, 85, 90, 100,
                                   110, 120, 130, 140, 150, 160, 170, 180, 190, 200, 210, 220, 230, 240, 250 };

            // Calculate WMAs for all Fibonacci levels
            var wmas = new List<double[]>();
            foreach (var fib in fibLevels)
            {
                var period = (int)fib;
                var wma = _manager.MA.WMA(closes, period);
                wmas.Add(wma);
            }

            // Calculate MavilimW as average of all WMAs
            var mavilimW = new double[length];
            for (int i = 0; i < length; i++)
            {
                double sum = 0;
                int count = 0;
                foreach (var wma in wmas)
                {
                    if (!double.IsNaN(wma[i]))
                    {
                        sum += wma[i];
                        count++;
                    }
                }
                mavilimW[i] = count > 0 ? sum / count : double.NaN;
            }

            // Calculate trendline using FAMA (Following Adaptive MA)
            var trendline = _manager.MA.Calculate(mavilimW, Base.MAMethod.FAMA, param1 + param2);

            return new MavilimWResult(mavilimW, trendline, param1, param2);
        }
    }
}
