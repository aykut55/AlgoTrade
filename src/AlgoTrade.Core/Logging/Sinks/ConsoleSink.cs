using System;

namespace AlgoTrade.Core.Logging.Sinks
{
    /// <summary>
    /// Console sink - Console.WriteLine ile yazar, renkli çıktı destekli
    /// </summary>
    public class ConsoleSink : ILogSink
    {
        private readonly object _lock = new object();
        private bool _isDisposed;

        public string Name => "Console";
        public LogSinks SinkType => LogSinks.Console;
        public bool IsEnabled { get; set; } = true;

        public bool ShowTimestamp { get; set; } = true;
        public bool ShowLevel { get; set; } = true;
        public bool ShowSource { get; set; } = true;

        public void Write(LogEntry entry)
        {
            if (_isDisposed || !IsEnabled)
                return;

            lock (_lock)
            {
                try
                {
                    if (entry.IsRaw)
                    {
                        if (entry.Color.HasValue)
                        {
                            var orig = Console.ForegroundColor;
                            Console.ForegroundColor = entry.Color.Value;
                            Console.WriteLine(entry.Message);
                            Console.ForegroundColor = orig;
                        }
                        else
                        {
                            Console.WriteLine(entry.Message);
                        }
                        return;
                    }

                    var message = entry.ToString(ShowTimestamp, ShowLevel, ShowSource);
                    var originalColor = Console.ForegroundColor;
                    Console.ForegroundColor = GetColor(entry.Level);
                    Console.WriteLine(message);
                    Console.ForegroundColor = originalColor;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"ConsoleSink error: {ex.Message}");
                }
            }
        }

        private static ConsoleColor GetColor(LogLevel level)
        {
            return level switch
            {
                LogLevel.Trace => ConsoleColor.Gray,
                LogLevel.Debug => ConsoleColor.DarkGray,
                LogLevel.Info => ConsoleColor.White,
                LogLevel.Warning => ConsoleColor.Yellow,
                LogLevel.Error => ConsoleColor.Red,
                LogLevel.Fatal => ConsoleColor.DarkRed,
                _ => ConsoleColor.White
            };
        }

        public void Clear()
        {
            lock (_lock)
            {
                try
                {
                    Console.Clear();
                }
                catch { }
            }
        }

        public void Flush()
        {
            // Console otomatik flush eder
        }

        public void Dispose()
        {
            _isDisposed = true;
        }
    }
}
