using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace AlgoTrade.Core.Logging
{
    /// <summary>
    /// Ana Log Manager - Singleton, Thread-safe
    ///
    /// KULLANIM:
    ///
    /// // 1. Sink'leri register et
    /// LogManager.Instance.RegisterSink(new ConsoleSink());
    /// LogManager.Instance.RegisterSink(new FileSink("logs/app.log"));
    /// LogManager.Instance.RegisterSink(new DebugSink());
    ///
    /// // 2. Log yaz (variadic)
    /// LogManager.Log("Application started");
    /// LogManager.Log("User clicked button", "param1", 123, true);
    /// LogManager.LogInfo("Info message");
    /// LogManager.LogError("Error occurred", exception);
    ///
    /// // 3. Hedef seçerek log (LogSinks enum ile)
    /// LogManager.Log("Debug info", sinks: LogSinks.Console | LogSinks.Debug);
    /// LogManager.Log("Sensitive data", sinks: LogSinks.File);
    ///
    /// // 4. Buffer yönetimi
    /// var logs = LogManager.Instance.GetBufferedLogs();
    /// LogManager.Instance.ClearBuffer();
    ///
    /// // 5. Sink yönetimi
    /// LogManager.Instance.EnableSink(LogSinks.Network, false);
    /// LogManager.Instance.ClearAllSinks();
    /// </summary>
    public class LogManager : IDisposable
    {
        private static LogManager? _instance;
        private static readonly object _instanceLock = new object();

        private readonly ConcurrentQueue<LogEntry> _buffer = new ConcurrentQueue<LogEntry>();
        private readonly List<ILogSink> _sinks = new List<ILogSink>();
        private readonly object _sinksLock = new object();
        private bool _isDisposed;

        /// <summary>
        /// Buffer max boyutu
        /// </summary>
        public int MaxBufferSize { get; set; } = 10000;

        /// <summary>
        /// Default log source
        /// </summary>
        public string DefaultSource { get; set; } = "App";

        private LogManager() { }

        /// <summary>
        /// Singleton instance
        /// </summary>
        public static LogManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_instanceLock)
                    {
                        if (_instance == null)
                        {
                            _instance = new LogManager();
                        }
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// Gets the singleton instance of LogManager.
        /// </summary>
        public static LogManager GetInstance()
        {
            return Instance;
        }

        /// <summary>
        /// Creates a new instance of LogManager for local use.
        /// </summary>
        public static LogManager GetNewInstance()
        {
            return new LogManager();
        }

        // ====================================================================
        // CONSOLE LOGGER
        // ====================================================================

        private ConsoleLogger? _consoleLogger;

        /// <summary>
        /// ConsoleLogger instance döner (düz metin yazmak için, log formatı olmadan)
        /// </summary>
        public static ConsoleLogger GetConsoleLogger()
        {
            Instance._consoleLogger ??= new ConsoleLogger();
            return Instance._consoleLogger;
        }

        /// <summary>
        /// ConsoleSink'i geçici olarak devre dışı bırak.
        /// </summary>
        public static void DisableConsoleSink() => Instance.EnableSink(LogSinks.Console, false);

        /// <summary>
        /// ConsoleSink'i tekrar aktif et.
        /// </summary>
        public static void EnableConsoleSink() => Instance.EnableSink(LogSinks.Console, true);

        /// <summary>
        /// FileSink'i geçici olarak devre dışı bırak.
        /// </summary>
        public static void DisableFileSink() => Instance.EnableSink(LogSinks.File, false);

        /// <summary>
        /// FileSink'i tekrar aktif et.
        /// </summary>
        public static void EnableFileSink() => Instance.EnableSink(LogSinks.File, true);

        /// <summary>
        /// DebugSink'i geçici olarak devre dışı bırak.
        /// </summary>
        public static void DisableDebugSink() => Instance.EnableSink(LogSinks.Debug, false);

        /// <summary>
        /// DebugSink'i tekrar aktif et.
        /// </summary>
        public static void EnableDebugSink() => Instance.EnableSink(LogSinks.Debug, true);

        // ====================================================================
        // SINK YÖNETİMİ
        // ====================================================================

        /// <summary>
        /// Sink register et
        /// </summary>
        public void RegisterSink(ILogSink sink)
        {
            if (sink == null)
                throw new ArgumentNullException(nameof(sink));

            lock (_sinksLock)
            {
                var existingSink = _sinks.FirstOrDefault(s => s.SinkType == sink.SinkType);
                if (existingSink != null)
                {
                    _sinks.Remove(existingSink);
                    existingSink.Dispose();
                }

                _sinks.Add(sink);
            }
        }

        /// <summary>
        /// Sink'i kaldır
        /// </summary>
        public void UnregisterSink(LogSinks sinkType)
        {
            lock (_sinksLock)
            {
                var sink = _sinks.FirstOrDefault(s => s.SinkType == sinkType);
                if (sink != null)
                {
                    _sinks.Remove(sink);
                    sink.Dispose();
                }
            }
        }

        /// <summary>
        /// Sink'i aktif/pasif yap
        /// </summary>
        public void EnableSink(LogSinks sinkType, bool enabled)
        {
            lock (_sinksLock)
            {
                var sink = _sinks.FirstOrDefault(s => s.SinkType == sinkType);
                if (sink != null)
                {
                    sink.IsEnabled = enabled;
                }
            }
        }

        /// <summary>
        /// Sink var mı kontrol et
        /// </summary>
        public bool HasSink(LogSinks sinkType)
        {
            lock (_sinksLock)
            {
                return _sinks.Any(s => s.SinkType == sinkType);
            }
        }

        /// <summary>
        /// Tüm sink'leri temizle
        /// </summary>
        public void ClearAllSinks()
        {
            lock (_sinksLock)
            {
                foreach (var sink in _sinks)
                {
                    try
                    {
                        sink.Clear();
                    }
                    catch { }
                }
            }
        }

        /// <summary>
        /// Tüm sink'leri flush et
        /// </summary>
        public void FlushAllSinks()
        {
            lock (_sinksLock)
            {
                foreach (var sink in _sinks)
                {
                    try
                    {
                        sink.Flush();
                    }
                    catch { }
                }
            }
        }

        // ====================================================================
        // RAW LOG - Formatsız, düz metin (timestamp/level/source yok)
        // ====================================================================

        /// <summary>
        /// Formatsız log - düz metin olarak tüm sink'lere yazar
        /// Tablo, banner, separator gibi çıktılar için kullanılır.
        /// Aynı kuyruktan geçer, sıralama korunur.
        /// </summary>
        public static void LogRaw(string message, LogSinks sinks = LogSinks.All)
        {
            LogRaw(message, null, sinks);
        }

        public static void LogRaw(string message, ConsoleColor? color, LogSinks sinks = LogSinks.All)
        {
            if (message == null)
                return;

            var entry = new LogEntry(
                level: LogLevel.Info,
                message: message,
                targetSinks: sinks,
                isRaw: true,
                color: color
            );

            Instance.AddToBuffer(entry);
            Instance.SendToSinks(entry);
        }

        // ====================================================================
        // LOG METODLARI - STATIC (Global logging to all sinks)
        // ====================================================================

        /// <summary>
        /// Genel log metodu (variadic) - STATIC
        /// </summary>
        public static void Log(params object[] args)
        {
            Instance.LogInternal(LogLevel.Info, null, LogSinks.All, args);
        }

        /// <summary>
        /// Log seviyesi ve sink seçimiyle - STATIC
        /// </summary>
        public static void Log(LogLevel level, LogSinks sinks, params object[] args)
        {
            Instance.LogInternal(level, null, sinks, args);
        }

        /// <summary>
        /// Log seviyesi, source ve sink seçimiyle - STATIC
        /// </summary>
        public static void Log(LogLevel level, string source, LogSinks sinks, params object[] args)
        {
            Instance.LogInternal(level, source, sinks, args);
        }

        /// <summary>
        /// Trace level log - STATIC
        /// </summary>
        public static void LogTrace(params object[] args)
        {
            Instance.LogInternal(LogLevel.Trace, null, LogSinks.All, args);
        }

        /// <summary>
        /// Debug level log - STATIC
        /// </summary>
        public static void LogDebug(params object[] args)
        {
            Instance.LogInternal(LogLevel.Debug, null, LogSinks.All, args);
        }

        /// <summary>
        /// Info level log - STATIC
        /// </summary>
        public static void LogInfo(params object[] args)
        {
            Instance.LogInternal(LogLevel.Info, null, LogSinks.All, args);
        }

        /// <summary>
        /// Warning level log - STATIC
        /// </summary>
        public static void LogWarning(params object[] args)
        {
            Instance.LogInternal(LogLevel.Warning, null, LogSinks.All, args);
        }

        /// <summary>
        /// Error level log - STATIC
        /// </summary>
        public static void LogError(params object[] args)
        {
            Instance.LogInternal(LogLevel.Error, null, LogSinks.All, args);
        }

        /// <summary>
        /// Fatal level log - STATIC
        /// </summary>
        public static void LogFatal(params object[] args)
        {
            Instance.LogInternal(LogLevel.Fatal, null, LogSinks.All, args);
        }

        // ====================================================================
        // LOG METODLARI - INSTANCE
        // ====================================================================

        /// <summary>
        /// Genel log metodu (variadic) - INSTANCE
        /// </summary>
        public void WriteLog(params object[] args)
        {
            LogInternal(LogLevel.Info, null, LogSinks.All, args);
        }

        /// <summary>
        /// Log seviyesi ve sink seçimiyle - INSTANCE
        /// </summary>
        public void WriteLog(LogLevel level, LogSinks sinks, params object[] args)
        {
            LogInternal(level, null, sinks, args);
        }

        /// <summary>
        /// Log seviyesi, source ve sink seçimiyle - INSTANCE
        /// </summary>
        public void WriteLog(LogLevel level, string source, LogSinks sinks, params object[] args)
        {
            LogInternal(level, source, sinks, args);
        }

        public void WriteTrace(params object[] args) => LogInternal(LogLevel.Trace, null, LogSinks.All, args);
        public void WriteDebug(params object[] args) => LogInternal(LogLevel.Debug, null, LogSinks.All, args);
        public void WriteInfo(params object[] args) => LogInternal(LogLevel.Info, null, LogSinks.All, args);
        public void WriteWarning(params object[] args) => LogInternal(LogLevel.Warning, null, LogSinks.All, args);
        public void WriteError(params object[] args) => LogInternal(LogLevel.Error, null, LogSinks.All, args);
        public void WriteFatal(params object[] args) => LogInternal(LogLevel.Fatal, null, LogSinks.All, args);

        // ====================================================================
        // İÇ METOD - LOG İŞLEME
        // ====================================================================

        private void LogInternal(LogLevel level, string? source, LogSinks targetSinks, params object[] args)
        {
            if (_isDisposed || args == null || args.Length == 0)
                return;

            try
            {
                string message = string.Empty;
                Exception? exception = null;
                var properties = new Dictionary<string, object>();

                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i] == null)
                        continue;

                    if (args[i] is Exception ex)
                    {
                        exception = ex;
                    }
                    else if (i == 0)
                    {
                        message = args[i].ToString() ?? string.Empty;
                    }
                    else
                    {
                        properties[$"arg{i}"] = args[i];
                    }
                }

                var entry = new LogEntry(
                    level: level,
                    message: message,
                    source: source ?? DefaultSource,
                    exception: exception,
                    properties: properties.Count > 0 ? properties : null,
                    targetSinks: targetSinks
                );

                AddToBuffer(entry);
                SendToSinks(entry);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LogManager error: {ex.Message}");
            }
        }

        private void AddToBuffer(LogEntry entry)
        {
            _buffer.Enqueue(entry);

            while (_buffer.Count > MaxBufferSize)
            {
                _buffer.TryDequeue(out _);
            }
        }

        private void SendToSinks(LogEntry entry)
        {
            lock (_sinksLock)
            {
                foreach (var sink in _sinks)
                {
                    try
                    {
                        if (!sink.IsEnabled)
                            continue;

                        if ((entry.TargetSinks & sink.SinkType) == 0)
                            continue;

                        sink.Write(entry);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Sink {sink.Name} error: {ex.Message}");
                    }
                }
            }
        }

        // ====================================================================
        // BUFFER YÖNETİMİ
        // ====================================================================

        /// <summary>
        /// Buffer'daki tüm logları al (copy)
        /// </summary>
        public List<LogEntry> GetBufferedLogs()
        {
            return _buffer.ToList();
        }

        /// <summary>
        /// Buffer'daki logları al ve temizle
        /// </summary>
        public List<LogEntry> GetAndClearBuffer()
        {
            var logs = new List<LogEntry>();
            while (_buffer.TryDequeue(out var entry))
            {
                logs.Add(entry);
            }
            return logs;
        }

        /// <summary>
        /// Buffer'ı temizle
        /// </summary>
        public void ClearBuffer()
        {
            _buffer.Clear();
        }

        /// <summary>
        /// Buffer boyutu
        /// </summary>
        public int BufferCount => _buffer.Count;

        /// <summary>
        /// Buffer'da hiç log mesajı var mı
        /// </summary>
        public bool HasAnyLogMessage()
        {
            return !_buffer.IsEmpty;
        }

        /// <summary>
        /// Buffer'da belirli bir mesaj var mı (contains)
        /// </summary>
        public bool HasLogMessage(string message, bool ignoreCase = true)
        {
            if (string.IsNullOrEmpty(message))
                return false;

            var comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            return _buffer.Any(entry => entry.Message.Contains(message, comparison));
        }

        /// <summary>
        /// Buffer'da belirli bir log level var mı
        /// </summary>
        public bool HasLogLevel(LogLevel level)
        {
            return _buffer.Any(entry => entry.Level == level);
        }

        /// <summary>
        /// Buffer'da belirli bir source var mı
        /// </summary>
        public bool HasLogSource(string source, bool ignoreCase = true)
        {
            if (string.IsNullOrEmpty(source))
                return false;

            var comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            return _buffer.Any(entry => entry.Source.Equals(source, comparison));
        }

        /// <summary>
        /// Buffer'daki logları level'a göre filtrele
        /// </summary>
        public List<LogEntry> GetLogsByLevel(LogLevel level)
        {
            return _buffer.Where(entry => entry.Level == level).ToList();
        }

        /// <summary>
        /// Buffer'daki logları mesaja göre filtrele
        /// </summary>
        public List<LogEntry> GetLogsByMessage(string message, bool ignoreCase = true)
        {
            if (string.IsNullOrEmpty(message))
                return new List<LogEntry>();

            var comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            return _buffer.Where(entry => entry.Message.Contains(message, comparison)).ToList();
        }

        // ====================================================================
        // DISPOSE
        // ====================================================================

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;

            lock (_sinksLock)
            {
                foreach (var sink in _sinks)
                {
                    try
                    {
                        sink.Flush();
                        sink.Dispose();
                    }
                    catch { }
                }
                _sinks.Clear();
            }

            _buffer.Clear();
        }
    }
}
