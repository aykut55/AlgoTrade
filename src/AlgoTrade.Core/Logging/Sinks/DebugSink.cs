using System;

namespace AlgoTrade.Core.Logging.Sinks
{
    /// <summary>
    /// Debug sink - Visual Studio Output penceresine yazar (System.Diagnostics.Debug.WriteLine)
    /// </summary>
    public class DebugSink : ILogSink
    {
        private bool _isDisposed;

        public string Name => "Debug";
        public LogSinks SinkType => LogSinks.Debug;
        public bool IsEnabled { get; set; } = true;

        public bool ShowTimestamp { get; set; } = true;
        public bool ShowLevel { get; set; } = true;
        public bool ShowSource { get; set; } = true;

        public void Write(LogEntry entry)
        {
            if (_isDisposed || !IsEnabled)
                return;

            try
            {
                var message = entry.IsRaw ? entry.Message : entry.ToString(ShowTimestamp, ShowLevel, ShowSource);
                System.Diagnostics.Debug.WriteLine(message);
            }
            catch
            {
                // Debug yazma hatası - sessizce yut
            }
        }

        public void Clear()
        {
            // Debug output temizlenemez
        }

        public void Flush()
        {
            System.Diagnostics.Debug.Flush();
        }

        public void Dispose()
        {
            _isDisposed = true;
        }
    }
}
