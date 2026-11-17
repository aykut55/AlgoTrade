using System.Collections.Generic;
using AlgoTrade.Core.Trading;

namespace AlgoTrade.Core.Scripting
{
    /// <summary>
    /// "Filter" asamasinin dosya-tabanli (inputs/scripts/csharpBridge/filter.csx)
    /// script'ine globals saglar - Signal asamasindan gelen HAM sinyal dizisini
    /// (RawSignals) alir, filtrelenmis halini int[] olarak dondurur. Sinyal
    /// uretiminden (signal.csx) KASITLI ayri tutulur - biri degismeden digeri
    /// degistirilebilsin diye.
    /// </summary>
    public class FilterScriptGlobals
    {
        public List<StockData> Data { get; }

        /// <summary>Signal asamasindan gelen ham sinyal dizisi (1=AL, -1=SAT, 0=yok).</summary>
        public int[] RawSignals { get; }

        public FilterScriptGlobals(List<StockData> data, int[] rawSignals)
        {
            Data = data;
            RawSignals = rawSignals;
        }
    }
}
