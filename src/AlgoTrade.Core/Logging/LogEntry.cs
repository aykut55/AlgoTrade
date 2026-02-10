using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace AlgoTrade.Core.Logging
{
    /// <summary>
    /// Log entry - Tek bir log kaydı
    /// Thread-safe, immutable after creation
    /// </summary>
    public class LogEntry
    {
        /// <summary>
        /// Log zamanı (local time)
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// Log seviyesi
        /// </summary>
        public LogLevel Level { get; }

        /// <summary>
        /// Thread ID
        /// </summary>
        public int ThreadId { get; }

        /// <summary>
        /// Thread adı (varsa)
        /// </summary>
        public string? ThreadName { get; }

        /// <summary>
        /// Kaynak (Logger adı, sınıf adı, vb.)
        /// </summary>
        public string Source { get; }

        /// <summary>
        /// Log mesajı
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Exception (varsa)
        /// </summary>
        public Exception? Exception { get; }

        /// <summary>
        /// Ek veriler (key-value pairs)
        /// </summary>
        public IReadOnlyDictionary<string, object>? Properties { get; }

        /// <summary>
        /// Hangi sink'lere gönderilecek
        /// </summary>
        public LogSinks TargetSinks { get; }

        /// <summary>
        /// Raw modda mı? (true ise formatsız, düz metin olarak yazılır)
        /// </summary>
        public bool IsRaw { get; }

        /// <summary>
        /// Console renk bilgisi (null ise varsayılan renk kullanılır)
        /// </summary>
        public ConsoleColor? Color { get; }

        public LogEntry(
            LogLevel level,
            string message,
            string source = "",
            Exception? exception = null,
            Dictionary<string, object>? properties = null,
            LogSinks targetSinks = LogSinks.All,
            bool isRaw = false,
            ConsoleColor? color = null)
        {
            Timestamp = DateTime.Now;
            Level = level;
            ThreadId = Thread.CurrentThread.ManagedThreadId;
            ThreadName = Thread.CurrentThread.Name;
            Source = source;
            Message = message ?? string.Empty;
            Exception = exception;
            Properties = properties;
            TargetSinks = targetSinks;
            IsRaw = isRaw;
            Color = color;
        }

        /// <summary>
        /// Varsayılan string formatı
        /// </summary>
        public override string ToString()
        {
            return ToString("default");
        }

        /// <summary>
        /// Özelleştirilmiş format
        /// </summary>
        public string ToString(string format)
        {
            var threadInfo = string.IsNullOrEmpty(ThreadName)
                ? $"T{ThreadId}"
                : $"{ThreadName}({ThreadId})";

            var sourceText = string.IsNullOrEmpty(Source) ? "" : $"[{Source}] ";
            var exceptionText = Exception != null
                ? $"\n  Exception: {Exception.GetType().Name}: {Exception.Message}\n  StackTrace: {Exception.StackTrace}"
                : string.Empty;

            return format switch
            {
                "short" => $"[{Timestamp:HH:mm:ss}] [{Level}] {Message}",
                "medium" => $"[{Timestamp:HH:mm:ss.fff}] [{Level}] {sourceText}{Message}",
                "default" => $"[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{Level,-7}] [{threadInfo,-12}] {sourceText}{Message}{exceptionText}",
                "long" => $"[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{Level,-7}] [{threadInfo,-12}] {sourceText}{Message}{exceptionText}{GetPropertiesText()}",
                "json" => ToJson(),
                _ => ToString("default")
            };
        }

        /// <summary>
        /// Flag'lere göre özelleştirilmiş format
        /// </summary>
        public string ToString(bool showTimestamp, bool showLevel, bool showSource)
        {
            var parts = new List<string>();
            if (showTimestamp) parts.Add($"[{Timestamp:HH:mm:ss.fff}]");
            if (showLevel) parts.Add($"[{Level}]");
            if (showSource && !string.IsNullOrEmpty(Source)) parts.Add($"[{Source}]");
            parts.Add(Message);

            var result = string.Join(" ", parts);

            if (Exception != null)
                result += $"\n  Exception: {Exception.GetType().Name}: {Exception.Message}\n  StackTrace: {Exception.StackTrace}";

            return result;
        }

        private string GetPropertiesText()
        {
            if (Properties == null || Properties.Count == 0)
                return string.Empty;

            var props = string.Join(", ", Properties.Select(kvp => $"{kvp.Key}={kvp.Value}"));
            return $"\n  Properties: {props}";
        }

        private string ToJson()
        {
            var escapedMessage = Message.Replace("\"", "\\\"").Replace("\n", "\\n");
            var exceptionJson = Exception != null
                ? $",\"exception\":{{\"type\":\"{Exception.GetType().Name}\",\"message\":\"{Exception.Message.Replace("\"", "\\\"")}\"}}"
                : string.Empty;

            return $"{{\"timestamp\":\"{Timestamp:O}\",\"level\":\"{Level}\",\"thread\":{ThreadId},\"source\":\"{Source}\",\"message\":\"{escapedMessage}\"{exceptionJson}}}";
        }
    }
}
