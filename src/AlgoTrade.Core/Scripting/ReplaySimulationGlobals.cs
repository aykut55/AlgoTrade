using System.Collections.Generic;
using AlgoTrade.Core.Trading;

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

        public ReplaySimulationGlobals(List<StockData> data, int[] signalCodes, string symbolName, string symbolPeriod)
        {
            Data = data;
            SignalCodes = signalCodes;
            SymbolName = symbolName;
            SymbolPeriod = symbolPeriod;
        }
    }
}
