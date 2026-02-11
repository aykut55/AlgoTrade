# Technical Indicators Library - Architecture & Design

## 📋 Overview

Comprehensive technical indicator library for algorithmic trading system. Supports 70+ Moving Average types, momentum, trend, volatility, volume, and price action indicators.

**Based on Python implementation** (src/Indicators/IndicatorManager.py)

## 🏗️ Architecture

```
IndicatorManager (Top-level Manager)
├── MA (MovingAverageCalculator)         → 70+ MA types
├── Momentum (MomentumIndicators)        → RSI, MACD, Stochastic, CCI, Williams%R
├── Trend (TrendIndicators)              → SuperTrend, MOST, ADX, Parabolic SAR
├── Volatility (VolatilityIndicators)    → ATR, Bollinger Bands, Keltner Channel
├── VolumeInd (VolumeIndicators)         → OBV, VWAP, MFI, CMF
├── PriceAction (PriceActionIndicators)  → HH/LL, Swing Points, ZigZag
└── Utils (PriceUtils)                   → HHV, LLV, StdDev, Sum, TrueRange
```

## 📁 Folder Structure

```
src/Indicators/
├── Base/
│   ├── MAMethod.cs                   ✅ 70+ MA enum (Python match)
│   └── IndicatorConfig.cs            ✅ Configuration
├── MovingAverages/
│   ├── MovingAverageCalculator.cs    ✅ Basic MAs (SMA, EMA, WMA, Hull, DEMA, TEMA, VWMA, LSMA)
│   └── MovingAverageCalculator.Advanced.cs  ✅ Advanced (KAMA, VIDYA, ZLEMA, T3, ALMA, JMA)
├── Trend/
│   ├── TrendIndicators.cs            ✅ Implemented (SuperTrend, MOST, ADX, Parabolic SAR, Aroon, Vortex, Ichimoku)
│   └── Results/SuperTrendResult.cs   ✅
├── Momentum/
│   ├── MomentumIndicators.cs         ✅ Implemented (RSI, MACD, Stochastic, CCI, Williams%R, ROC)
│   └── Results/
│       ├── RSIResult.cs              ✅
│       └── MACDResult.cs             ✅
├── Volatility/
│   ├── VolatilityIndicators.cs       ✅ Implemented (ATR, Bollinger Bands, Keltner Channel, Donchian Channel)
│   └── Results/BollingerBandsResult.cs  ✅
├── Volume/
│   └── VolumeIndicators.cs           ✅ Implemented (OBV, VWAP, MFI, CMF)
├── PriceAction/
│   └── PriceActionIndicators.cs      ✅ Implemented (HH/LL, Swing Points, ZigZag, Fractals)
├── Utils/
│   └── PriceUtils.cs                 ✅ HHV, LLV, StdDev, Sum, Mean, Variance, TrueRange
├── IndicatorManager.cs               ✅ Main manager class
├── IndicatorTest.cs                  ✅ ALL USAGE EXAMPLES (DON'T DELETE!)
├── README.md                         ✅ This file
└── TODO.md                           ✅ Task list
```

## 🚀 Quick Start

### Basic Usage

```csharp
// 1. Create manager
var manager = new IndicatorManager();
manager.Initialize(stockDataList);

// 2. Extract prices
var closes = manager.GetClosePrices();
var highs = manager.GetHighPrices();
var lows = manager.GetLowPrices();

// 3. Calculate indicators
var sma20 = manager.MA.SMA(closes, 20);
var ema50 = manager.MA.EMA(closes, 50);
var hull200 = manager.MA.HullMA(closes, 200);

// 4. Advanced MAs
var kama = manager.MA.KAMA(closes, 14);
var t3 = manager.MA.T3(closes, 5, 0.7);
var alma = manager.MA.ALMA(closes, 9);

// 5. Utils
var hhv20 = manager.Utils.HHV(highs, 20);
var llv20 = manager.Utils.LLV(lows, 20);
var stdDev = manager.Utils.StdDev(closes, 20);
```

### Generic MA Calculation (Enum)

```csharp
var ema20 = manager.MA.Calculate(closes, MAMethod.EMA, 20);
var hull50 = manager.MA.Calculate(closes, MAMethod.HULL, 50);
var dema14 = manager.MA.Calculate(closes, MAMethod.DEMA, 14);
```

### Bulk Operations

```csharp
// Same method, multiple periods
var periods = new[] { 20, 50, 100, 200 };
var smaResults = manager.MA.CalculateBulk(closes, MAMethod.SIMPLE, periods);

// Multiple methods, same period
var methods = new[] { MAMethod.SIMPLE, MAMethod.EMA, MAMethod.WMA, MAMethod.HULL };
var ma20Results = manager.MA.CalculateBulk(closes, methods, 20);

// Fibonacci periods
var fibPeriods = manager.Config.FibonacciPeriods.ToArray();
var fiboEmas = manager.MA.CalculateBulk(closes, MAMethod.EMA, fibPeriods);
```

### Configuration

```csharp
var config = new IndicatorConfig
{
    EnableDebugLogging = true,          // LogManager integration
    EnablePerformanceTiming = true,     // TimeManager integration
    CacheSize = 128,                    // Cache optimization
    DefaultRSIPeriod = 14,
    DefaultMACDFastPeriod = 12,
    DefaultMACDSlowPeriod = 26,
    DefaultMACDSignalPeriod = 9
};

var manager = new IndicatorManager(config);
```

## 📊 Implemented Moving Averages (17 types)

### Basic MAs
- ✅ **SMA** - Simple Moving Average
- ✅ **EMA** - Exponential Moving Average
- ✅ **WMA** - Weighted Moving Average
- ✅ **HULL** - Hull Moving Average (reduced lag)
- ✅ **DEMA** - Double Exponential MA
- ✅ **TEMA** - Triple Exponential MA
- ✅ **VWMA** - Volume Weighted MA
- ✅ **LSMA** - Least Squares MA (Linear Regression)
- ✅ **Triangular** - SMA of SMA
- ✅ **Wilder** - Wilder's Smoothing (used in RSI/ATR)
- ✅ **SMMA** - Smoothed MA

### Advanced MAs
- ✅ **KAMA** - Kaufman's Adaptive MA (volatility adaptive)
- ✅ **VIDYA** - Variable Index Dynamic Average (CMO-based)
- ✅ **ZLEMA** - Zero-Lag EMA (compensates delay)
- ✅ **T3** - Tillson T3 (6x EMA smoothing)
- ✅ **ALMA** - Arnaud Legoux MA (Gaussian weighted)
- ✅ **JMA** - Jurik MA (advanced smoothing)

### 53+ More MAs (Enum defined, not yet implemented)
See `MAMethod.cs` for full list: FRAMA, MAMA, McGinley, VAMA, DHULL, THULL, GMA, MEDIAN, etc.

## 🧰 Utility Functions (PriceUtils)

- ✅ **HHV** - Highest High Value (last N periods)
- ✅ **LLV** - Lowest Low Value (last N periods)
- ✅ **Sum** - Sum of values over period
- ✅ **Mean** - Average over period
- ✅ **StdDev** - Standard Deviation
- ✅ **Variance** - Variance over period
- ✅ **TrueRange** - True Range (for ATR calculation)
- ✅ **Diff** - Price differences (current - previous)
- ✅ **PercentChange** - Percentage change

## 🎯 Design Principles

### 1. Category-Based Access
```csharp
manager.MA.*           // Moving Averages
manager.Momentum.*     // RSI, MACD, Stochastic
manager.Trend.*        // SuperTrend, MOST, ADX
manager.Volatility.*   // ATR, Bollinger Bands
manager.VolumeInd.*    // OBV, VWAP, MFI
manager.PriceAction.*  // HH/LL, Swing Points
manager.Utils.*        // Helper functions
```

### 2. Result Objects
```csharp
// Complex indicators return structured results
var rsi = manager.Momentum.RSI(closes, 14);
// rsi.Values, rsi.Overbought, rsi.Oversold, rsi.Current

var macd = manager.Momentum.MACD(closes);
// macd.MACD, macd.Signal, macd.Histogram, macd.IsBullish

var st = manager.Trend.SuperTrend(10, 3.0);
// st.SuperTrend, st.Direction, st.IsBullish, st.IsBearish
```

### 3. Cache Optimization
- Automatic caching for calculated indicators
- Configurable cache size (default: 128)
- Hash-based cache keys (method + period + data hash)
- Performance logging via TimeManager

### 4. Integration
- **LogManager** - Debug logging
- **TimeManager** - Performance measurement
- **StockData** - Market data structure

## 🔧 Testing

```csharp
var test = new IndicatorTest();

// Run all tests
test.RunAllTests(stockDataList);

// Real-world examples
test.ShowRealWorldExamples(stockDataList);
```

**IMPORTANT:** `IndicatorTest.cs` contains ALL usage examples. Don't delete!

## 📝 Next Steps (Priority Order)

See `TODO.md` for detailed task list.

### High Priority (Python already implemented)
1. **RSI** - Relative Strength Index (Python: line 1125)
2. **MACD** - Moving Average Convergence Divergence (Python: line 1195)
3. **SuperTrend** - Trend indicator (Python: line 1422)
4. **MOST** - Moving Stop Loss (Python: line 1360)
5. **ATR** - Average True Range

### Medium Priority
6. **Bollinger Bands** - Volatility bands
7. **Stochastic** - Momentum oscillator
8. **OBV** - On Balance Volume
9. **VWAP** - Volume Weighted Average Price
10. **ADX** - Average Directional Index

### Low Priority
11. **Parabolic SAR** - Stop and Reverse
12. **CCI** - Commodity Channel Index
13. **Williams %R** - Momentum indicator
14. **MFI** - Money Flow Index
15. **HH/LL** - Higher High / Lower Low patterns

## 🌟 Features

- ✅ 70+ Moving Average types (17 implemented, 53+ defined)
- ✅ Generic MA calculation via enum
- ✅ Bulk operations (multiple periods/methods)
- ✅ Cache system for performance
- ✅ LogManager integration
- ✅ TimeManager integration
- ✅ Comprehensive test suite
- ✅ Real-world usage examples
- ✅ Structured result objects
- ✅ Python implementation compatibility

## 📚 Resources

### Python Reference
- `src/Indicators/IndicatorManager.py` (lines 1-1487)
- 70+ MA types defined (MAMethod enum, line 23-93)
- RSI implementation (line 1125-1191)
- MACD implementation (line 1195-1241)
- SuperTrend implementation (line 1422-1486)
- MOST implementation (line 1360-1420)

### TradingView Popular Indicators
- RSI, MACD, Bollinger Bands
- EMA (20, 50, 200)
- VWAP, SuperTrend, Ichimoku Cloud
- Stochastic RSI, ATR
- Volume Profile, Pivot Points

### Platform References
- **MetaTrader (MT4/MT5)** - 30+ built-in indicators
- **TradingView** - Most popular TA platform
- **IdealData (Turkey)** - Professional desktop TA software
- **Matrix** - BIST + derivatives focus

## 🏆 Credits

- **Architecture Design:** Based on Python IndicatorManager
- **Implementation:** C# port with enhancements
- **Integration:** LogManager, TimeManager, StockData

---

**Last Updated:** 2025-01-18
**Version:** 1.0.0 (Initial Design)
**Status:** 17 MAs implemented, Others TODO
