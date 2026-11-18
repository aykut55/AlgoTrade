using System;
using System.Collections.Generic;
using AlgoTrade.Core.Trading;
using AlgoTrade.Core.Trading.Core;

namespace AlgoTrade.Core.Scripting
{
    /// <summary>
    /// pythonnet koprusunden (AlgoTrade2 plotter) gelen AL/SAT sinyal dizisini
    /// GERCEK SingleTrader execution motoruna (ReplaySignalStrategy uzerinden)
    /// besleyen script'e globals saglar - AlgoTrade.ReplayRunner/Program.cs ile
    /// AYNI mantik, sadece subprocess+JSON degil in-process+bellek.
    /// </summary>
    public class ReplaySimulationGlobals
    {
        public List<StockData> Data { get; }

        /// <summary>TradeSignals raw degerleri (0=None, 1=Buy, 2=Sell, ...), Data.Count uzunlugunda.</summary>
        public int[] SignalCodes { get; }

        public string SymbolName { get; }
        public string SymbolPeriod { get; }
        public Func<SingleTrader, int, TradeSignals>? ExitRule { get; }

        public ReplaySimulationGlobals(List<StockData> data, int[] signalCodes, string symbolName, string symbolPeriod)
            : this(data, signalCodes, symbolName, symbolPeriod, null)
        {
        }

        public ReplaySimulationGlobals(List<StockData> data, int[] signalCodes, string symbolName, string symbolPeriod,
            Func<SingleTrader, int, TradeSignals>? exitRule)
        {
            Data = data;
            SignalCodes = signalCodes;
            SymbolName = symbolName;
            SymbolPeriod = symbolPeriod;
            ExitRule = exitRule;
        }
    }
}
