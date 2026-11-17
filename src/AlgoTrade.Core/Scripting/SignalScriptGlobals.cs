using System;
using System.Collections.Generic;
using AlgoTrade.Core.Trading;
using AlgoTrade.Core.Trading.Indicators;

namespace AlgoTrade.Core.Scripting
{
    /// <summary>
    /// Minimal globals exposed to AL/SAT/FLAT sinyal scriptlerine (pythonnet
    /// koprusunden - AlgoTrade2 plotter) - ScriptGlobals'tan farkli olarak
    /// AlgoTrader/SingleTrader kurulumu GEREKTIRMEZ, sadece OHLC verisi + (lazy)
    /// projenin gercek indikator kutuphanesini (IndicatorManager) verir.
    /// Script "return" ile int[] (1=AL, -1=SAT, 0=FLAT; Data.Count uzunlugunda) doner.
    /// </summary>
    public class SignalScriptGlobals : IDisposable
    {
        public List<StockData> Data { get; }

        /// <summary>
        /// Projenin gercek indikator kutuphanesi (Indicators.MA.Calculate,
        /// Indicators.Trend.ResolvePriceSource, Indicators.PriceAction.ZigZag, ...
        /// - stratejilerde kullanilanin AYNISI). Ilk erisimde Data'dan kurulur.
        /// </summary>
        public IndicatorManager Indicators => _indicators ??= new IndicatorManager(Data);

        private IndicatorManager? _indicators;

        public SignalScriptGlobals(List<StockData> data)
        {
            Data = data;
        }

        public void Dispose()
        {
            _indicators?.Dispose();
            _indicators = null;
        }
    }
}
