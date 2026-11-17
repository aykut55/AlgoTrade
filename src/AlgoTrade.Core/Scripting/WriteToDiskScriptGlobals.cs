using AlgoTrade.Core.Trading;

namespace AlgoTrade.Core.Scripting
{
    /// <summary>
    /// "Diske Yaz" asamasinin dosya-tabanli (scripts_bridge/diskeYaz.csx) script'ine
    /// globals saglar - "Hesapla" (ReplaySimulationGlobals) asamasindan donen
    /// AlgoTrader'i tasir, kullanici hangi dosyalarin/ne sekilde yazilacagini
    /// (WriteTraderDataToFilesAsync disinda ek bir export de) kendi yazabilir.
    /// </summary>
    public class WriteToDiskScriptGlobals
    {
        public AlgoTrader Trader { get; }

        public WriteToDiskScriptGlobals(AlgoTrader trader)
        {
            Trader = trader;
        }
    }
}
