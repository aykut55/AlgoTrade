namespace AlgoTrade.Core.Logging
{
    /// <summary>
    /// Log seviyeleri - Düşükten yükseğe
    /// </summary>
    public enum LogLevel
    {
        /// <summary>
        /// En detaylı seviye - Trace bilgileri
        /// </summary>
        Trace = 0,

        /// <summary>
        /// Debug bilgileri - Development için
        /// </summary>
        Debug = 1,

        /// <summary>
        /// Genel bilgi mesajları
        /// </summary>
        Info = 2,

        /// <summary>
        /// Uyarı mesajları
        /// </summary>
        Warning = 3,

        /// <summary>
        /// Hata mesajları
        /// </summary>
        Error = 4,

        /// <summary>
        /// Kritik/Fatal hatalar
        /// </summary>
        Fatal = 5
    }
}
