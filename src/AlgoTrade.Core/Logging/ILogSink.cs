using System;

namespace AlgoTrade.Core.Logging
{
    /// <summary>
    /// Log sink interface - Tüm sink'ler bunu implement eder
    /// </summary>
    public interface ILogSink : IDisposable
    {
        /// <summary>
        /// Sink'in adı (Console, File, Network, Debug, vb.)
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Sink'in flag değeri (LogSinks enum'dan)
        /// </summary>
        LogSinks SinkType { get; }

        /// <summary>
        /// Sink aktif mi?
        /// </summary>
        bool IsEnabled { get; set; }

        /// <summary>
        /// Log entry'yi işle ve hedefe yaz
        /// Thread-safe olmalı!
        /// </summary>
        void Write(LogEntry entry);

        /// <summary>
        /// Sink'i temizle (opsiyonel)
        /// </summary>
        void Clear();

        /// <summary>
        /// Flush - Bekleyen logları hemen yaz
        /// </summary>
        void Flush();
    }
}
