using AlgoTrade.Core.Trading.Indicators.Base;
using AlgoTrade.Core.Trading.Indicators.SupportResistance.Results;
using ScottPlot;
using System;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace AlgoTrade.Core.Trading.Indicators.SupportResistance
{
    /// <summary>
    /// Support/Resistance Indicators - Pivot Points, Fibonacci Retracement, etc.
    /// </summary>
    public class SupportResistanceIndicators
    {
        private readonly IndicatorManager _manager;
        private readonly IndicatorConfig _config;

        public SupportResistanceIndicators(IndicatorManager manager, IndicatorConfig config)
        {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <summary>
        /// Fibonacci Retracement Levels
        /// Calculates Fibonacci retracement levels between high and low
        /// Levels: 0%, 23.6%, 38.2%, 50%, 61.8%, 78.6%, 100%
        /// Used to identify potential reversal points during pullbacks
        /// </summary>
        /// <param name="high">High price for the move</param>
        /// <param name="low">Low price for the move</param>
        /// <param name="isUptrend">True if calculating for uptrend (retracement from top), false for downtrend</param>
        /// <returns>FibonacciRetracementResult containing all Fibonacci levels</returns>
        public FibonacciRetracementResult FibonacciRetracement(double high, double low, bool isUptrend = true)
        {
            if (high <= low)
                throw new ArgumentException("High must be greater than low");

            double range = high - low;

            if (isUptrend)
            {
                // Retracement from high (downward levels)
                return new FibonacciRetracementResult(
                    level_0: high,                          // 0% - High
                    level_236: high - (range * 0.236),      // 23.6%
                    level_382: high - (range * 0.382),      // 38.2%
                    level_50: high - (range * 0.50),        // 50%
                    level_618: high - (range * 0.618),      // 61.8%
                    level_786: high - (range * 0.786),      // 78.6%
                    level_100: low,                         // 100% - Low
                    high: high,
                    low: low,
                    isUptrend: isUptrend
                );
            }
            else
            {
                // Retracement from low (upward levels)
                return new FibonacciRetracementResult(
                    level_0: low,                           // 0% - Low
                    level_236: low + (range * 0.236),       // 23.6%
                    level_382: low + (range * 0.382),       // 38.2%
                    level_50: low + (range * 0.50),         // 50%
                    level_618: low + (range * 0.618),       // 61.8%
                    level_786: low + (range * 0.786),       // 78.6%
                    level_100: high,                        // 100% - High
                    high: high,
                    low: low,
                    isUptrend: isUptrend
                );
            }
        }

        /// <summary>
        /// Fibonacci Retracement Levels (Auto-detect from data)
        /// Automatically finds the high and low from a specified period and calculates Fibonacci levels
        /// </summary>
        /// <param name="period">Lookback period to find high/low (default: 100)</param>
        /// <returns>FibonacciRetracementAutoResult containing arrays of Fibonacci levels for each bar</returns>
        public FibonacciRetracementAutoResult FibonacciRetracementAuto(int period = 100)
        {
            if (!_manager.IsInitialized)
                throw new InvalidOperationException("Manager not initialized with data");
            if (period <= 0)
                throw new ArgumentException("Period must be positive", nameof(period));

            var highs = _manager.GetHighPrices();
            var lows = _manager.GetLowPrices();

            var length = highs.Length;
            var level_0 = new double[length];
            var level_236 = new double[length];
            var level_382 = new double[length];
            var level_50 = new double[length];
            var level_618 = new double[length];
            var level_786 = new double[length];
            var level_100 = new double[length];

            // Initialize with NaN
            for (int i = 0; i < Math.Min(period - 1, length); i++)
            {
                level_0[i] = double.NaN;
                level_236[i] = double.NaN;
                level_382[i] = double.NaN;
                level_50[i] = double.NaN;
                level_618[i] = double.NaN;
                level_786[i] = double.NaN;
                level_100[i] = double.NaN;
            }

            // Calculate Fibonacci levels for each bar
            for (int i = period - 1; i < length; i++)
            {
                // Find highest high and lowest low in the period
                double periodHigh = highs[i];
                double periodLow = lows[i];

                for (int j = 0; j < period; j++)
                {
                    int idx = i - j;
                    if (highs[idx] > periodHigh)
                        periodHigh = highs[idx];
                    if (lows[idx] < periodLow)
                        periodLow = lows[idx];
                }

                // Determine if uptrend or downtrend (compare first and last in period)
                bool isUptrend = highs[i] > highs[i - period + 1];

                // Calculate Fibonacci levels
                var levels = FibonacciRetracement(periodHigh, periodLow, isUptrend);

                level_0[i] = levels.Level_0;
                level_236[i] = levels.Level_236;
                level_382[i] = levels.Level_382;
                level_50[i] = levels.Level_50;
                level_618[i] = levels.Level_618;
                level_786[i] = levels.Level_786;
                level_100[i] = levels.Level_100;
            }

            return new FibonacciRetracementAutoResult(level_0, level_236, level_382, level_50, level_618, level_786, level_100, period);
        }

        // ==================== PIVOT POINTS - 12 CALCULATION METHODS ====================
        // 1. Classic/Standard(zaten var) - (H+L+C)/3
        // 2. Fibonacci - Fibonacci oranları ile
        // 3. Woodie - (H+L+2C)/4
        // 4. Camarilla - Yüksek hassasiyetli
        // 5. DeMark - Conditional formulas
        // 6. Floor Pivot - Floor trader pivots
        // 7. Classic Extended - R4, R5, S4, S5 ile
        // 8. Fibonacci Extension - 161.8%, 261.8%
        // 9. Pivot Point High/Low - Swing based
        // 10. Traditional Floor - Eski usul
        // 11. Alternative Classic - Alternatif formül
        // 12. CPR(Central Pivot Range) - TC, BC, Pivot
        // ==================== PIVOT POINTS - 12 CALCULATION METHODS ====================

        /// <summary>
        /// Classic/Standard Pivot Points (Method #1 of 12)
        /// Calculates support and resistance levels based on previous period's high, low, close
        /// Formula:
        /// - Pivot Point (PP) = (High + Low + Close) / 3
        /// - R1 = (2 * PP) - Low
        /// - R2 = PP + (High - Low)
        /// - R3 = High + 2 * (PP - Low)
        /// - S1 = (2 * PP) - High
        /// - S2 = PP - (High - Low)
        /// - S3 = Low - 2 * (High - PP)
        /// Most widely used pivot point calculation method
        /// Used for intraday trading to identify potential reversal points
        /// </summary>
        /// <param name="useDaily">If true, uses previous day's data for each calculation (default: true)</param>
        /// <returns>PivotPointsResult containing pivot and support/resistance levels</returns>
        public PivotPointsResult ClassicPivotPoints(bool useDaily = true)
        {
            if (!_manager.IsInitialized)
                throw new InvalidOperationException("Manager not initialized with data");

            var highs = _manager.GetHighPrices();
            var lows = _manager.GetLowPrices();
            var closes = _manager.GetClosePrices();

            var length = closes.Length;
            var pivot = new double[length];
            var r1 = new double[length];
            var r2 = new double[length];
            var r3 = new double[length];
            var s1 = new double[length];
            var s2 = new double[length];
            var s3 = new double[length];

            if (length < 2)
            {
                // Not enough data
                for (int i = 0; i < length; i++)
                {
                    pivot[i] = double.NaN;
                    r1[i] = double.NaN;
                    r2[i] = double.NaN;
                    r3[i] = double.NaN;
                    s1[i] = double.NaN;
                    s2[i] = double.NaN;
                    s3[i] = double.NaN;
                }
                return new PivotPointsResult(pivot, r1, r2, r3, s1, s2, s3);
            }

            // First bar uses its own values
            CalculatePivotLevels(highs[0], lows[0], closes[0],
                out pivot[0], out r1[0], out r2[0], out r3[0],
                out s1[0], out s2[0], out s3[0]);

            // Calculate pivot points for each bar using previous bar's data
            for (int i = 1; i < length; i++)
            {
                // Use previous bar's high, low, close
                double prevHigh = highs[i - 1];
                double prevLow = lows[i - 1];
                double prevClose = closes[i - 1];

                CalculatePivotLevels(prevHigh, prevLow, prevClose,
                    out pivot[i], out r1[i], out r2[i], out r3[i],
                    out s1[i], out s2[i], out s3[i]);
            }

            return new PivotPointsResult(pivot, r1, r2, r3, s1, s2, s3);
        }

        /// <summary>
        /// Helper method to calculate pivot levels from HLC values
        /// </summary>
        private void CalculatePivotLevels(double high, double low, double close,
            out double pivot, out double r1, out double r2, out double r3,
            out double s1, out double s2, out double s3)
        {
            // Pivot Point
            pivot = (high + low + close) / 3.0;

            // Resistance levels
            r1 = (2.0 * pivot) - low;
            r2 = pivot + (high - low);
            r3 = high + 2.0 * (pivot - low);

            // Support levels
            s1 = (2.0 * pivot) - high;
            s2 = pivot - (high - low);
            s3 = low - 2.0 * (high - pivot);
        }


        /// <summary>
        /// Fibonacci Pivot Points (Method #2 of 12)
        /// Uses Fibonacci ratios to calculate support and resistance levels
        /// Formula:
        /// - Pivot Point (PP) = (High + Low + Close) / 3
        /// - R1 = PP + 0.382 * (High - Low)
        /// - R2 = PP + 0.618 * (High - Low)
        /// - R3 = PP + 1.000 * (High - Low)
        /// - S1 = PP - 0.382 * (High - Low)
        /// - S2 = PP - 0.618 * (High - Low)
        /// - S3 = PP - 1.000 * (High - Low)
        /// Popular among Fibonacci traders
        /// </summary>
        public PivotPointsResult FibonacciPivotPoints(bool useDaily = true)
        {
            if (!_manager.IsInitialized)
                throw new InvalidOperationException("Manager not initialized with data");

            var highs = _manager.GetHighPrices();
            var lows = _manager.GetLowPrices();
            var closes = _manager.GetClosePrices();

            var length = closes.Length;
            var pivot = new double[length];
            var r1 = new double[length];
            var r2 = new double[length];
            var r3 = new double[length];
            var s1 = new double[length];
            var s2 = new double[length];
            var s3 = new double[length];

            if (length < 2)
            {
                for (int i = 0; i < length; i++)
                {
                    pivot[i] = r1[i] = r2[i] = r3[i] = s1[i] = s2[i] = s3[i] = double.NaN;
                }
                return new PivotPointsResult(pivot, r1, r2, r3, s1, s2, s3);
            }

            // Calculate for each bar
            for (int i = 0; i < length; i++)
            {
                int idx = (i == 0) ? 0 : i - 1;
                double h = highs[idx];
                double l = lows[idx];
                double c = closes[idx];

                pivot[i] = (h + l + c) / 3.0;
                double range = h - l;

                r1[i] = pivot[i] + 0.382 * range;
                r2[i] = pivot[i] + 0.618 * range;
                r3[i] = pivot[i] + 1.000 * range;

                s1[i] = pivot[i] - 0.382 * range;
                s2[i] = pivot[i] - 0.618 * range;
                s3[i] = pivot[i] - 1.000 * range;
            }

            return new PivotPointsResult(pivot, r1, r2, r3, s1, s2, s3);
        }

        /// <summary>
        /// Woodie Pivot Points (Method #3 of 12)
        /// Gives more weight to the closing price
        /// Formula:
        /// - Pivot Point (PP) = (High + Low + 2*Close) / 4
        /// - R1 = (2 * PP) - Low
        /// - R2 = PP + (High - Low)
        /// - R3 = High + 2 * (PP - Low)
        /// - S1 = (2 * PP) - High
        /// - S2 = PP - (High - Low)
        /// - S3 = Low - 2 * (High - PP)
        /// Popular among day traders, emphasizes close price
        /// </summary>
        public PivotPointsResult WoodiePivotPoints(bool useDaily = true)
        {
            if (!_manager.IsInitialized)
                throw new InvalidOperationException("Manager not initialized with data");

            var highs = _manager.GetHighPrices();
            var lows = _manager.GetLowPrices();
            var closes = _manager.GetClosePrices();

            var length = closes.Length;
            var pivot = new double[length];
            var r1 = new double[length];
            var r2 = new double[length];
            var r3 = new double[length];
            var s1 = new double[length];
            var s2 = new double[length];
            var s3 = new double[length];

            if (length < 2)
            {
                for (int i = 0; i < length; i++)
                {
                    pivot[i] = r1[i] = r2[i] = r3[i] = s1[i] = s2[i] = s3[i] = double.NaN;
                }
                return new PivotPointsResult(pivot, r1, r2, r3, s1, s2, s3);
            }

            for (int i = 0; i < length; i++)
            {
                int idx = (i == 0) ? 0 : i - 1;
                double h = highs[idx];
                double l = lows[idx];
                double c = closes[idx];

                // Woodie formula: more weight on close
                pivot[i] = (h + l + 2.0 * c) / 4.0;

                r1[i] = (2.0 * pivot[i]) - l;
                r2[i] = pivot[i] + (h - l);
                r3[i] = h + 2.0 * (pivot[i] - l);

                s1[i] = (2.0 * pivot[i]) - h;
                s2[i] = pivot[i] - (h - l);
                s3[i] = l - 2.0 * (h - pivot[i]);
            }

            return new PivotPointsResult(pivot, r1, r2, r3, s1, s2, s3);
        }

        /// <summary>
        /// DeMark Pivot Points (Method #4 of 12)
        /// Tom DeMark's conditional pivot calculation
        /// Formula depends on Close vs Open relationship:
        /// - If Close < Open: X = High + 2*Low + Close
        /// - If Close > Open: X = 2*High + Low + Close
        /// - If Close = Open: X = High + Low + 2*Close
        /// - PP = X / 4
        /// - R1 = X / 2 - Low
        /// - S1 = X / 2 - High
        /// Only calculates one resistance and one support level
        /// </summary>
        public PivotPointsResult DeMarkPivotPoints(bool useDaily = true)
        {
            if (!_manager.IsInitialized)
                throw new InvalidOperationException("Manager not initialized with data");

            var highs = _manager.GetHighPrices();
            var lows = _manager.GetLowPrices();
            var closes = _manager.GetClosePrices();
            var opens = _manager.GetOpenPrices();

            var length = closes.Length;
            var pivot = new double[length];
            var r1 = new double[length];
            var r2 = new double[length];
            var r3 = new double[length];
            var s1 = new double[length];
            var s2 = new double[length];
            var s3 = new double[length];

            if (length < 2)
            {
                for (int i = 0; i < length; i++)
                {
                    pivot[i] = r1[i] = r2[i] = r3[i] = s1[i] = s2[i] = s3[i] = double.NaN;
                }
                return new PivotPointsResult(pivot, r1, r2, r3, s1, s2, s3);
            }

            for (int i = 0; i < length; i++)
            {
                int idx = (i == 0) ? 0 : i - 1;
                double h = highs[idx];
                double l = lows[idx];
                double c = closes[idx];
                double o = opens[idx];

                double x;
                if (c < o)
                    x = h + 2.0 * l + c;
                else if (c > o)
                    x = 2.0 * h + l + c;
                else
                    x = h + l + 2.0 * c;

                pivot[i] = x / 4.0;
                r1[i] = (x / 2.0) - l;
                s1[i] = (x / 2.0) - h;

                // DeMark only defines R1 and S1, others set to NaN
                r2[i] = double.NaN;
                r3[i] = double.NaN;
                s2[i] = double.NaN;
                s3[i] = double.NaN;
            }

            return new PivotPointsResult(pivot, r1, r2, r3, s1, s2, s3);
        }

        /// <summary>
        /// Floor Pivot Points (Method #5 of 12)
        /// Traditional floor trader pivot points (same as Classic)
        /// This is an alias for ClassicPivotPoints for backward compatibility
        /// </summary>
        public PivotPointsResult FloorPivotPoints(bool useDaily = true)
        {
            return ClassicPivotPoints(useDaily);
        }

        /// <summary>
        /// Camarilla Pivot Points (Method #6 of 12)
        /// High-precision intraday pivot levels with tight R1-R4 and S1-S4
        /// Formula:
        /// - Pivot Point (PP) = (High + Low + Close) / 3
        /// - R4 = Close + ((High - Low) * 1.1 / 2)
        /// - R3 = Close + ((High - Low) * 1.1 / 4)
        /// - R2 = Close + ((High - Low) * 1.1 / 6)
        /// - R1 = Close + ((High - Low) * 1.1 / 12)
        /// - S1 = Close - ((High - Low) * 1.1 / 12)
        /// - S2 = Close - ((High - Low) * 1.1 / 6)
        /// - S3 = Close - ((High - Low) * 1.1 / 4)
        /// - S4 = Close - ((High - Low) * 1.1 / 2)
        /// Popular for scalping and day trading with tight stops
        /// </summary>
        public CamarillaPivotPointsResult CamarillaPivotPoints(bool useDaily = true)
        {
            if (!_manager.IsInitialized)
                throw new InvalidOperationException("Manager not initialized with data");

            var highs = _manager.GetHighPrices();
            var lows = _manager.GetLowPrices();
            var closes = _manager.GetClosePrices();

            var length = closes.Length;
            var pivot = new double[length];
            var r1 = new double[length];
            var r2 = new double[length];
            var r3 = new double[length];
            var r4 = new double[length];
            var s1 = new double[length];
            var s2 = new double[length];
            var s3 = new double[length];
            var s4 = new double[length];

            if (length < 2)
            {
                for (int i = 0; i < length; i++)
                {
                    pivot[i] = r1[i] = r2[i] = r3[i] = r4[i] = s1[i] = s2[i] = s3[i] = s4[i] = double.NaN;
                }
                return new CamarillaPivotPointsResult(pivot, r1, r2, r3, r4, s1, s2, s3, s4);
            }

            for (int i = 0; i < length; i++)
            {
                int idx = (i == 0) ? 0 : i - 1;
                double h = highs[idx];
                double l = lows[idx];
                double c = closes[idx];

                pivot[i] = (h + l + c) / 3.0;
                double range = (h - l) * 1.1;

                r4[i] = c + (range / 2.0);
                r3[i] = c + (range / 4.0);
                r2[i] = c + (range / 6.0);
                r1[i] = c + (range / 12.0);

                s1[i] = c - (range / 12.0);
                s2[i] = c - (range / 6.0);
                s3[i] = c - (range / 4.0);
                s4[i] = c - (range / 2.0);
            }

            return new CamarillaPivotPointsResult(pivot, r1, r2, r3, r4, s1, s2, s3, s4);
        }

        /// <summary>
        /// CPR (Central Pivot Range) Pivot Points (Method #7 of 12)
        /// Identifies price consolidation zones
        /// Formula:
        /// - Pivot Point (PP) = (High + Low + Close) / 3
        /// - TC (Top Central) = (Pivot - BC) + Pivot
        /// - BC (Bottom Central) = (High + Low) / 2
        /// - R1, R2, R3 and S1, S2, S3 calculated as in Classic method
        /// Narrow CPR indicates potential breakout, wide CPR indicates ranging market
        /// </summary>
        public CPRPivotPointsResult CPRPivotPoints(bool useDaily = true)
        {
            if (!_manager.IsInitialized)
                throw new InvalidOperationException("Manager not initialized with data");

            var highs = _manager.GetHighPrices();
            var lows = _manager.GetLowPrices();
            var closes = _manager.GetClosePrices();

            var length = closes.Length;
            var pivot = new double[length];
            var tc = new double[length];
            var bc = new double[length];
            var r1 = new double[length];
            var r2 = new double[length];
            var r3 = new double[length];
            var s1 = new double[length];
            var s2 = new double[length];
            var s3 = new double[length];
            var cprWidth = new double[length];

            if (length < 2)
            {
                for (int i = 0; i < length; i++)
                {
                    pivot[i] = tc[i] = bc[i] = r1[i] = r2[i] = r3[i] = s1[i] = s2[i] = s3[i] = cprWidth[i] = double.NaN;
                }
                return new CPRPivotPointsResult(pivot, tc, bc, r1, r2, r3, s1, s2, s3, cprWidth);
            }

            for (int i = 0; i < length; i++)
            {
                int idx = (i == 0) ? 0 : i - 1;
                double h = highs[idx];
                double l = lows[idx];
                double c = closes[idx];

                pivot[i] = (h + l + c) / 3.0;
                bc[i] = (h + l) / 2.0;
                tc[i] = (pivot[i] - bc[i]) + pivot[i];
                cprWidth[i] = tc[i] - bc[i];

                // Classic R/S levels
                r1[i] = (2.0 * pivot[i]) - l;
                r2[i] = pivot[i] + (h - l);
                r3[i] = h + 2.0 * (pivot[i] - l);

                s1[i] = (2.0 * pivot[i]) - h;
                s2[i] = pivot[i] - (h - l);
                s3[i] = l - 2.0 * (h - pivot[i]);
            }

            return new CPRPivotPointsResult(pivot, tc, bc, r1, r2, r3, s1, s2, s3, cprWidth);
        }

        /// <summary>
        /// Classic Extended Pivot Points (Method #8 of 12)
        /// Classic pivots with additional R4, R5 and S4, S5 levels
        /// Formula: Uses classic pivot formulas extended with:
        /// - R4 = R3 + (High - Low)
        /// - R5 = R4 + (High - Low)
        /// - S4 = S3 - (High - Low)
        /// - S5 = S4 - (High - Low)
        /// Useful for volatile markets with extended ranges
        /// </summary>
        public ExtendedPivotPointsResult ClassicExtendedPivotPoints(bool useDaily = true)
        {
            if (!_manager.IsInitialized)
                throw new InvalidOperationException("Manager not initialized with data");

            var highs = _manager.GetHighPrices();
            var lows = _manager.GetLowPrices();
            var closes = _manager.GetClosePrices();

            var length = closes.Length;
            var pivot = new double[length];
            var r1 = new double[length];
            var r2 = new double[length];
            var r3 = new double[length];
            var r4 = new double[length];
            var r5 = new double[length];
            var s1 = new double[length];
            var s2 = new double[length];
            var s3 = new double[length];
            var s4 = new double[length];
            var s5 = new double[length];

            if (length < 2)
            {
                for (int i = 0; i < length; i++)
                {
                    pivot[i] = r1[i] = r2[i] = r3[i] = r4[i] = r5[i] = s1[i] = s2[i] = s3[i] = s4[i] = s5[i] = double.NaN;
                }
                return new ExtendedPivotPointsResult(pivot, r1, r2, r3, r4, r5, s1, s2, s3, s4, s5);
            }

            for (int i = 0; i < length; i++)
            {
                int idx = (i == 0) ? 0 : i - 1;
                double h = highs[idx];
                double l = lows[idx];
                double c = closes[idx];
                double range = h - l;

                // Classic pivot and levels
                pivot[i] = (h + l + c) / 3.0;
                r1[i] = (2.0 * pivot[i]) - l;
                r2[i] = pivot[i] + range;
                r3[i] = h + 2.0 * (pivot[i] - l);

                s1[i] = (2.0 * pivot[i]) - h;
                s2[i] = pivot[i] - range;
                s3[i] = l - 2.0 * (h - pivot[i]);

                // Extended levels
                r4[i] = r3[i] + range;
                r5[i] = r4[i] + range;
                s4[i] = s3[i] - range;
                s5[i] = s4[i] - range;
            }

            return new ExtendedPivotPointsResult(pivot, r1, r2, r3, r4, r5, s1, s2, s3, s4, s5);
        }

        /// <summary>
        /// Fibonacci Extension Pivot Points (Method #9 of 12)
        /// Fibonacci pivots with extension levels (161.8%, 261.8%)
        /// Formula:
        /// - Pivot Point (PP) = (High + Low + Close) / 3
        /// - R1 = PP + 0.618 * (High - Low)
        /// - R2 = PP + 1.000 * (High - Low)
        /// - R3 = PP + 1.618 * (High - Low) [Extension]
        /// - S1 = PP - 0.618 * (High - Low)
        /// - S2 = PP - 1.000 * (High - Low)
        /// - S3 = PP - 1.618 * (High - Low) [Extension]
        /// Uses Fibonacci extension for extreme price targets
        /// </summary>
        public PivotPointsResult FibonacciExtensionPivotPoints(bool useDaily = true)
        {
            if (!_manager.IsInitialized)
                throw new InvalidOperationException("Manager not initialized with data");

            var highs = _manager.GetHighPrices();
            var lows = _manager.GetLowPrices();
            var closes = _manager.GetClosePrices();

            var length = closes.Length;
            var pivot = new double[length];
            var r1 = new double[length];
            var r2 = new double[length];
            var r3 = new double[length];
            var s1 = new double[length];
            var s2 = new double[length];
            var s3 = new double[length];

            if (length < 2)
            {
                for (int i = 0; i < length; i++)
                {
                    pivot[i] = r1[i] = r2[i] = r3[i] = s1[i] = s2[i] = s3[i] = double.NaN;
                }
                return new PivotPointsResult(pivot, r1, r2, r3, s1, s2, s3);
            }

            for (int i = 0; i < length; i++)
            {
                int idx = (i == 0) ? 0 : i - 1;
                double h = highs[idx];
                double l = lows[idx];
                double c = closes[idx];

                pivot[i] = (h + l + c) / 3.0;
                double range = h - l;

                r1[i] = pivot[i] + 0.618 * range;
                r2[i] = pivot[i] + 1.000 * range;
                r3[i] = pivot[i] + 1.618 * range; // Extension

                s1[i] = pivot[i] - 0.618 * range;
                s2[i] = pivot[i] - 1.000 * range;
                s3[i] = pivot[i] - 1.618 * range; // Extension
            }

            return new PivotPointsResult(pivot, r1, r2, r3, s1, s2, s3);
        }

        /// <summary>
        /// Traditional Floor Pivot Points (Method #10 of 12)
        /// Alias for Classic Pivot Points (same calculation)
        /// Named for historical use by floor traders
        /// </summary>
        public PivotPointsResult TraditionalFloorPivotPoints(bool useDaily = true)
        {
            return ClassicPivotPoints(useDaily);
        }

        /// <summary>
        /// Alternative Classic Pivot Points (Method #11 of 12)
        /// Uses average of Open and Close instead of just Close
        /// Formula:
        /// - Pivot Point (PP) = (High + Low + ((Open + Close) / 2)) / 3
        /// - R1, R2, R3 and S1, S2, S3 calculated as in Classic method
        /// Provides smoother pivot levels accounting for opening price
        /// </summary>
        public PivotPointsResult AlternativeClassicPivotPoints(bool useDaily = true)
        {
            if (!_manager.IsInitialized)
                throw new InvalidOperationException("Manager not initialized with data");

            var highs = _manager.GetHighPrices();
            var lows = _manager.GetLowPrices();
            var closes = _manager.GetClosePrices();
            var opens = _manager.GetOpenPrices();

            var length = closes.Length;
            var pivot = new double[length];
            var r1 = new double[length];
            var r2 = new double[length];
            var r3 = new double[length];
            var s1 = new double[length];
            var s2 = new double[length];
            var s3 = new double[length];

            if (length < 2)
            {
                for (int i = 0; i < length; i++)
                {
                    pivot[i] = r1[i] = r2[i] = r3[i] = s1[i] = s2[i] = s3[i] = double.NaN;
                }
                return new PivotPointsResult(pivot, r1, r2, r3, s1, s2, s3);
            }

            for (int i = 0; i < length; i++)
            {
                int idx = (i == 0) ? 0 : i - 1;
                double h = highs[idx];
                double l = lows[idx];
                double c = closes[idx];
                double o = opens[idx];

                // Alternative formula: uses avg of open and close
                pivot[i] = (h + l + ((o + c) / 2.0)) / 3.0;

                r1[i] = (2.0 * pivot[i]) - l;
                r2[i] = pivot[i] + (h - l);
                r3[i] = h + 2.0 * (pivot[i] - l);

                s1[i] = (2.0 * pivot[i]) - h;
                s2[i] = pivot[i] - (h - l);
                s3[i] = l - 2.0 * (h - pivot[i]);
            }

            return new PivotPointsResult(pivot, r1, r2, r3, s1, s2, s3);
        }

        /// <summary>
        /// Mid Pivot Points (Method #12 of 12)
        /// Includes mid-levels between major pivot points
        /// Calculated as simple average between adjacent levels:
        /// - M0 = (Pivot + S1) / 2
        /// - M1 = (S1 + S2) / 2
        /// - M2 = (S2 + S3) / 2
        /// - M3 = (Pivot + R1) / 2
        /// - M4 = (R1 + R2) / 2
        /// - M5 = (R2 + R3) / 2
        /// Provides additional granular support/resistance levels
        /// Note: Uses Classic pivot calculation as base, returns extended result for mid-points
        /// </summary>
        public ExtendedPivotPointsResult MidPivotPoints(bool useDaily = true)
        {
            if (!_manager.IsInitialized)
                throw new InvalidOperationException("Manager not initialized with data");

            // First get classic pivots
            var classic = ClassicPivotPoints(useDaily);

            var length = classic.Length;
            var pivot = classic.Pivot;
            var r1 = classic.R1;
            var r2 = classic.R2;
            var r3 = classic.R3;
            var s1 = classic.S1;
            var s2 = classic.S2;
            var s3 = classic.S3;

            // Calculate mid-levels (using extended result to store them)
            var m3 = new double[length]; // Mid between Pivot and R1
            var m4 = new double[length]; // Mid between R1 and R2
            var m5 = new double[length]; // Mid between R2 and R3
            var m0 = new double[length]; // Mid between Pivot and S1
            var m1 = new double[length]; // Mid between S1 and S2
            var m2 = new double[length]; // Mid between S2 and S3

            for (int i = 0; i < length; i++)
            {
                m3[i] = (pivot[i] + r1[i]) / 2.0;
                m4[i] = (r1[i] + r2[i]) / 2.0;
                m5[i] = (r2[i] + r3[i]) / 2.0;

                m0[i] = (pivot[i] + s1[i]) / 2.0;
                m1[i] = (s1[i] + s2[i]) / 2.0;
                m2[i] = (s2[i] + s3[i]) / 2.0;
            }

            // Return as extended result: R4=M4, R5=M5, S4=M1, S5=M2
            // Note: This is a creative mapping to fit mid-levels into extended structure
            return new ExtendedPivotPointsResult(pivot, r1, m3, r2, m4, m5, s1, m0, s2, m1, m2);
        }
    }
}
