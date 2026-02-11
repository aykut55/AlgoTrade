using System;
using System.Collections.Generic;
using System.Linq;
using AlgoTrade.Core;
using AlgoTrade.Core.Trading.Indicators.Base;
using AlgoTrade.Core.Logging;

namespace AlgoTrade.Core.Trading.Indicators
{
    /// <summary>
    /// Indicator Test Suite - All usage examples
    ///
    /// IMPORTANT: Bu dosya tüm kullanım örneklerini içerir, KAYBOLMASIN!
    ///
    /// Usage:
    ///   var test = new IndicatorTest();
    ///   test.RunAllTests(stockDataList);
    /// </summary>
    public class IndicatorTest
    {
        private readonly LogManager _logManager;

        public IndicatorTest()
        {
            _logManager = LogManager.Instance;
        }

        /// <summary>
        /// Run all tests
        /// </summary>
        public void RunAllTests(List<StockData> data)
        {
            _logManager.WriteLog("========================================");
            _logManager.WriteLog("INDICATOR TEST SUITE - STARTING");
            _logManager.WriteLog("========================================");

            try
            {
                Test_BasicSetup(data);
                Test_MovingAverages_Basic(data);
                Test_MovingAverages_Advanced(data);
                Test_MovingAverages_Bulk(data);
                Test_PriceUtils(data);
                Test_CachePerformance(data);

                _logManager.WriteLog("\n========================================");
                _logManager.WriteLog("ALL TESTS COMPLETED SUCCESSFULLY!");
                _logManager.WriteLog("========================================");
            }
            catch (Exception ex)
            {
                _logManager.WriteError($"TEST FAILED: {ex.Message}", ex);
            }
        }

        #region Test 1: Basic Setup

        /// <summary>
        /// Test 1: Basic setup and initialization
        /// </summary>
        private void Test_BasicSetup(List<StockData> data)
        {
            _logManager.WriteLog("\n=== Test 1: Basic Setup ===");

            // Create manager with default config
            var manager = new IndicatorManager();
            _logManager.WriteLog($"✓ IndicatorManager created: {manager}");

            // Initialize with data
            manager.SetData(data);
            _logManager.WriteLog($"✓ Initialized with {manager.BarCount} bars");

            // Check initialization
            if (!manager.IsInitialized)
                throw new Exception("Manager not initialized!");
            _logManager.WriteLog("✓ Manager is initialized");

            // Get cache stats
            var stats = manager.GetCacheStats();
            _logManager.WriteLog($"✓ Cache stats: {stats["CacheSize"]}/{stats["MaxCacheSize"]}");

            // Extract prices
            var closes = manager.GetClosePrices();
            var opens = manager.GetOpenPrices();
            var highs = manager.GetHighPrices();
            var lows = manager.GetLowPrices();
            var volume = manager.GetVolume();
            _logManager.WriteLog($"✓ Extracted prices: {closes.Length} close, {opens.Length} open, {highs.Length} high, {lows.Length} low, {volume.Length} volume");
        }

        #endregion

        #region Test 2: Moving Averages - Basic

        /// <summary>
        /// Test 2: Basic Moving Averages (SMA, EMA, WMA, Hull, DEMA, TEMA)
        /// </summary>
        private void Test_MovingAverages_Basic(List<StockData> data)
        {
            _logManager.WriteLog("\n=== Test 2: Moving Averages - Basic ===");

            var manager = new IndicatorManager();
            manager.SetData(data);
            var closes = manager.GetClosePrices();

            // SMA
            var sma20 = manager.MA.SMA(closes, 20);
            _logManager.WriteLog($"✓ SMA(20): Last value = {sma20[^1]:F2}");

            var sma50 = manager.MA.SMA(closes, 50);
            _logManager.WriteLog($"✓ SMA(50): Last value = {sma50[^1]:F2}");

            var sma200 = manager.MA.SMA(closes, 200);
            _logManager.WriteLog($"✓ SMA(200): Last value = {sma200[^1]:F2}");

            // EMA
            var ema12 = manager.MA.EMA(closes, 12);
            _logManager.WriteLog($"✓ EMA(12): Last value = {ema12[^1]:F2}");

            var ema26 = manager.MA.EMA(closes, 26);
            _logManager.WriteLog($"✓ EMA(26): Last value = {ema26[^1]:F2}");

            // WMA
            var wma10 = manager.MA.WMA(closes, 10);
            _logManager.WriteLog($"✓ WMA(10): Last value = {wma10[^1]:F2}");

            // Hull MA
            var hull20 = manager.MA.HullMA(closes, 20);
            _logManager.WriteLog($"✓ Hull MA(20): Last value = {hull20[^1]:F2}");

            // DEMA
            var dema14 = manager.MA.DEMA(closes, 14);
            _logManager.WriteLog($"✓ DEMA(14): Last value = {dema14[^1]:F2}");

            // TEMA
            var tema14 = manager.MA.TEMA(closes, 14);
            _logManager.WriteLog($"✓ TEMA(14): Last value = {tema14[^1]:F2}");

            // VWMA (uses volume)
            var vwma20 = manager.MA.VWMA(closes, 20);
            _logManager.WriteLog($"✓ VWMA(20): Last value = {vwma20[^1]:F2}");

            // LSMA (Linear Regression)
            var lsma20 = manager.MA.LSMA(closes, 20);
            _logManager.WriteLog($"✓ LSMA(20): Last value = {lsma20[^1]:F2}");
        }

        #endregion

        #region Test 3: Moving Averages - Advanced

        /// <summary>
        /// Test 3: Advanced Moving Averages (KAMA, VIDYA, ZLEMA, T3, ALMA, JMA)
        /// </summary>
        private void Test_MovingAverages_Advanced(List<StockData> data)
        {
            _logManager.WriteLog("\n=== Test 3: Moving Averages - Advanced ===");

            var manager = new IndicatorManager();
            manager.SetData(data);
            var closes = manager.GetClosePrices();

            // KAMA (Kaufman's Adaptive MA)
            var kama14 = manager.MA.KAMA(closes, 14, 2, 30);
            _logManager.WriteLog($"✓ KAMA(14,2,30): Last value = {kama14[^1]:F2}");

            // VIDYA (Variable Index Dynamic Average)
            var vidya14 = manager.MA.VIDYA(closes, 14);
            _logManager.WriteLog($"✓ VIDYA(14): Last value = {vidya14[^1]:F2}");

            // ZLEMA (Zero-Lag EMA)
            var zlema20 = manager.MA.ZLEMA(closes, 20);
            _logManager.WriteLog($"✓ ZLEMA(20): Last value = {zlema20[^1]:F2}");

            // T3 (Tillson T3)
            var t3_5 = manager.MA.T3(closes, 5, 0.7);
            _logManager.WriteLog($"✓ T3(5, 0.7): Last value = {t3_5[^1]:F2}");

            // ALMA (Arnaud Legoux MA)
            var alma9 = manager.MA.ALMA(closes, 9, 6.0, 0.85);
            _logManager.WriteLog($"✓ ALMA(9, 6.0, 0.85): Last value = {alma9[^1]:F2}");

            // JMA (Jurik MA)
            var jma7 = manager.MA.JMA(closes, 7, 0, 2);
            _logManager.WriteLog($"✓ JMA(7, 0, 2): Last value = {jma7[^1]:F2}");
        }

        #endregion

        #region Test 4: Moving Averages - Bulk & Generic

        /// <summary>
        /// Test 4: Bulk operations and generic MA calculation
        /// </summary>
        private void Test_MovingAverages_Bulk(List<StockData> data)
        {
            _logManager.WriteLog("\n=== Test 4: Moving Averages - Bulk & Generic ===");

            var manager = new IndicatorManager();
            manager.SetData(data);
            var closes = manager.GetClosePrices();

            // Generic MA calculation (all MA types via enum)
            var ema20 = manager.MA.Calculate(closes, MAMethod.EMA, 20);
            _logManager.WriteLog($"✓ Generic MA - EMA(20): Last value = {ema20[^1]:F2}");

            var hull50 = manager.MA.Calculate(closes, MAMethod.HULL, 50);
            _logManager.WriteLog($"✓ Generic MA - Hull(50): Last value = {hull50[^1]:F2}");

            // Bulk calculation: Same method, different periods
            var periods = new[] { 20, 50, 100, 200 };
            var smaResults = manager.MA.CalculateBulk(closes, MAMethod.SIMPLE, periods);
            _logManager.WriteLog($"✓ Bulk SMA calculated for {periods.Length} periods:");
            for (int i = 0; i < periods.Length; i++)
            {
                _logManager.WriteLog($"  SMA({periods[i]}): {smaResults[i][^1]:F2}");
            }

            // Bulk calculation: Multiple methods, same period
            var methods = new[] { MAMethod.SIMPLE, MAMethod.EMA, MAMethod.WMA, MAMethod.HULL };
            var ma20Results = manager.MA.CalculateBulk(closes, methods, 20);
            _logManager.WriteLog($"✓ Bulk MA(20) calculated for {methods.Length} methods:");
            for (int i = 0; i < methods.Length; i++)
            {
                _logManager.WriteLog($"  {methods[i]}(20): {ma20Results[i][^1]:F2}");
            }

            // Fibonacci periods
            var fibPeriods = manager.Config.FibonacciPeriods.ToArray();
            var fiboResults = manager.MA.CalculateBulk(closes, MAMethod.EMA, fibPeriods);
            _logManager.WriteLog($"✓ Fibonacci EMAs calculated for {fibPeriods.Length} periods:");
            _logManager.WriteLog($"  Periods: {string.Join(", ", fibPeriods)}");
            _logManager.WriteLog($"  Last values: {string.Join(", ", fiboResults.Select(r => $"{r[^1]:F2}"))}");
        }

        #endregion

        #region Test 5: Price Utils

        /// <summary>
        /// Test 5: Utility functions (HHV, LLV, StdDev, Sum, etc.)
        /// </summary>
        private void Test_PriceUtils(List<StockData> data)
        {
            _logManager.WriteLog("\n=== Test 5: Price Utils ===");

            var manager = new IndicatorManager();
            manager.SetData(data);
            var closes = manager.GetClosePrices();
            var highs = manager.GetHighPrices();
            var lows = manager.GetLowPrices();

            // HHV (Highest High Value)
            var hhv20 = manager.Utils.HHV(highs, 20);
            _logManager.WriteLog($"✓ HHV(20): Last value = {hhv20[^1]:F2}");

            // LLV (Lowest Low Value)
            var llv20 = manager.Utils.LLV(lows, 20);
            _logManager.WriteLog($"✓ LLV(20): Last value = {llv20[^1]:F2}");

            // Standard Deviation
            var stdDev20 = manager.Utils.StdDev(closes, 20);
            _logManager.WriteLog($"✓ StdDev(20): Last value = {stdDev20[^1]:F4}");

            // Sum
            var sum20 = manager.Utils.Sum(closes, 20);
            _logManager.WriteLog($"✓ Sum(20): Last value = {sum20[^1]:F2}");

            // Mean
            var mean20 = manager.Utils.Mean(closes, 20);
            _logManager.WriteLog($"✓ Mean(20): Last value = {mean20[^1]:F2}");

            // Variance
            var variance20 = manager.Utils.Variance(closes, 20);
            _logManager.WriteLog($"✓ Variance(20): Last value = {variance20[^1]:F4}");

            // True Range
            var tr = manager.Utils.TrueRange(highs, lows, closes);
            _logManager.WriteLog($"✓ True Range: Last value = {tr[^1]:F2}");

            // Diff (price changes)
            var diff = manager.Utils.Diff(closes);
            _logManager.WriteLog($"✓ Diff: Last value = {diff[^1]:F2}");

            // Percent Change
            var pctChange = manager.Utils.PercentChange(closes);
            _logManager.WriteLog($"✓ Percent Change: Last value = {pctChange[^1]:F2}%");
        }

        #endregion

        #region Test 6: Cache Performance

        /// <summary>
        /// Test 6: Cache performance and statistics
        /// </summary>
        private void Test_CachePerformance(List<StockData> data)
        {
            _logManager.WriteLog("\n=== Test 6: Cache Performance ===");

            // Enable performance timing
            var config = new IndicatorConfig
            {
                EnablePerformanceTiming = true,
                EnableDebugLogging = true,
                CacheSize = 10  // Small cache to test overflow
            };

            var manager = new IndicatorManager(config);
            manager.SetData(data);
            var closes = manager.GetClosePrices();

            _logManager.WriteLog($"Initial cache stats: {string.Join(", ", manager.GetCacheStats().Select(kv => $"{kv.Key}={kv.Value}"))}");

            // Calculate multiple indicators (should fill cache)
            for (int period = 10; period <= 25; period++)
            {
                var sma = manager.MA.SMA(closes, period);
            }

            var stats1 = manager.GetCacheStats();
            _logManager.WriteLog($"✓ After 16 calculations: Cache={stats1["CacheSize"]}/{stats1["MaxCacheSize"]}");

            // Re-calculate same indicator (should hit cache)
            var sma20_1 = manager.MA.SMA(closes, 20);
            var sma20_2 = manager.MA.SMA(closes, 20);
            _logManager.WriteLog("✓ Re-calculated SMA(20) twice - should hit cache on 2nd call");

            // Clear cache
            manager.ClearCache();
            var stats2 = manager.GetCacheStats();
            _logManager.WriteLog($"✓ After ClearCache: Cache={stats2["CacheSize"]}/{stats2["MaxCacheSize"]}");

            // Re-calculate after clear (should miss cache)
            var sma20_3 = manager.MA.SMA(closes, 20);
            _logManager.WriteLog("✓ Calculated SMA(20) after clear - should miss cache");
        }

        #endregion

        #region Placeholder Tests (TODO)

        /// <summary>
        /// Test 7: Momentum Indicators (RSI, MACD, Stochastic)
        /// TODO: Implement when momentum indicators are ready
        /// </summary>
        private void Test_MomentumIndicators(List<StockData> data)
        {
            _logManager.WriteLog("\n=== Test 7: Momentum Indicators (TODO) ===");
            _logManager.WriteLog("TODO: Implement RSI, MACD, Stochastic tests");

            /*
            var manager = new IndicatorManager();
            manager.Initialize(data);
            var closes = manager.GetClosePrices();

            // RSI
            var rsi14 = manager.Momentum.RSI(closes, 14);
            _logManager.WriteLog($"✓ RSI(14): Last value = {rsi14.Current:F2}, Overbought={rsi14.IsOverbought}, Oversold={rsi14.IsOversold}");

            // MACD
            var macd = manager.Momentum.MACD(closes, 12, 26, 9);
            _logManager.WriteLog($"✓ MACD(12,26,9): MACD={macd.CurrentMACD:F2}, Signal={macd.CurrentSignal:F2}, Histogram={macd.CurrentHistogram:F2}");
            _logManager.WriteLog($"  Trend: {(macd.IsBullish ? "Bullish" : "Bearish")}");

            // Stochastic
            var stoch = manager.Momentum.Stochastic(14, 3);
            _logManager.WriteLog($"✓ Stochastic(14,3): K={stoch.k[^1]:F2}, D={stoch.d[^1]:F2}");
            */
        }

        /// <summary>
        /// Test 8: Trend Indicators (SuperTrend, MOST, ADX)
        /// TODO: Implement when trend indicators are ready
        /// </summary>
        private void Test_TrendIndicators(List<StockData> data)
        {
            _logManager.WriteLog("\n=== Test 8: Trend Indicators (TODO) ===");
            _logManager.WriteLog("TODO: Implement SuperTrend, MOST, ADX tests");

            /*
            var manager = new IndicatorManager();
            manager.Initialize(data);

            // SuperTrend
            var supertrend = manager.Trend.SuperTrend(10, 3.0);
            _logManager.WriteLog($"✓ SuperTrend(10,3.0): Value={supertrend.Current:F2}, Trend={(supertrend.IsBullish ? "Bullish" : "Bearish")}");

            // MOST
            var (most, exmov) = manager.Trend.MOST(21, 1.0);
            _logManager.WriteLog($"✓ MOST(21,1.0): MOST={most[^1]:F2}, EXMOV={exmov[^1]:F2}");

            // ADX
            var adx = manager.Trend.ADX(14);
            _logManager.WriteLog($"✓ ADX(14): Last value = {adx[^1]:F2}");
            */
        }

        #endregion

        #region Usage Examples

        /// <summary>
        /// BONUS: Real-world usage examples
        /// </summary>
        public void ShowRealWorldExamples(List<StockData> data)
        {
            _logManager.WriteLog("\n========================================");
            _logManager.WriteLog("REAL-WORLD USAGE EXAMPLES");
            _logManager.WriteLog("========================================");

            var manager = new IndicatorManager();
            manager.SetData(data);
            var closes = manager.GetClosePrices();

            // Example 1: Moving Average Crossover Strategy
            _logManager.WriteLog("\n--- Example 1: MA Crossover Strategy ---");
            var sma50 = manager.MA.SMA(closes, 50);
            var sma200 = manager.MA.SMA(closes, 200);
            var lastSma50 = sma50[^1];
            var lastSma200 = sma200[^1];
            var signal = lastSma50 > lastSma200 ? "BULLISH (Golden Cross)" : "BEARISH (Death Cross)";
            _logManager.WriteLog($"SMA(50) = {lastSma50:F2}");
            _logManager.WriteLog($"SMA(200) = {lastSma200:F2}");
            _logManager.WriteLog($"Signal: {signal}");

            // Example 2: Multi-timeframe EMA
            _logManager.WriteLog("\n--- Example 2: Multi-timeframe EMA ---");
            var ema20 = manager.MA.EMA(closes, 20);
            var ema50 = manager.MA.EMA(closes, 50);
            var ema100 = manager.MA.EMA(closes, 100);
            var ema200 = manager.MA.EMA(closes, 200);
            _logManager.WriteLog($"EMA(20) = {ema20[^1]:F2}");
            _logManager.WriteLog($"EMA(50) = {ema50[^1]:F2}");
            _logManager.WriteLog($"EMA(100) = {ema100[^1]:F2}");
            _logManager.WriteLog($"EMA(200) = {ema200[^1]:F2}");
            var trend = (ema20[^1] > ema50[^1] && ema50[^1] > ema100[^1] && ema100[^1] > ema200[^1])
                ? "STRONG UPTREND"
                : "NOT IN UPTREND";
            _logManager.WriteLog($"Trend Analysis: {trend}");

            // Example 3: Bollinger Band Width (volatility)
            _logManager.WriteLog("\n--- Example 3: Volatility Analysis ---");
            var sma20 = manager.MA.SMA(closes, 20);
            var stdDev20 = manager.Utils.StdDev(closes, 20);
            var bbUpper = sma20[^1] + 2 * stdDev20[^1];
            var bbLower = sma20[^1] - 2 * stdDev20[^1];
            var bbWidth = bbUpper - bbLower;
            var price = closes[^1];
            _logManager.WriteLog($"Price = {price:F2}");
            _logManager.WriteLog($"BB Upper = {bbUpper:F2}");
            _logManager.WriteLog($"BB Middle = {sma20[^1]:F2}");
            _logManager.WriteLog($"BB Lower = {bbLower:F2}");
            _logManager.WriteLog($"BB Width = {bbWidth:F2} (Volatility indicator)");

            var percentB = (price - bbLower) / (bbUpper - bbLower) * 100;
            _logManager.WriteLog($"%B = {percentB:F2}% (0%=lower band, 100%=upper band)");
        }

        #endregion
    }
}
