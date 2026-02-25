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
        /// Master switch - false yapılırsa tüm loglama durur
        /// </summary>
        public bool IsEnabled { get; set; } = true;

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
        // SHOW/HIDE - Timestamp, Level, Source
        // ====================================================================

        /// <summary>
        /// Timestamp gösterimini aç/kapat. sinkType belirtilmezse tüm sink'lerde uygulanır.
        /// </summary>
        public static void EnableTimestamp(LogSinks? sinkType = null) => Instance.SetSinkProperty(sinkType, (s, v) => s.ShowTimestamp = v, true);
        public static void DisableTimestamp(LogSinks? sinkType = null) => Instance.SetSinkProperty(sinkType, (s, v) => s.ShowTimestamp = v, false);

        /// <summary>
        /// Level gösterimini aç/kapat. sinkType belirtilmezse tüm sink'lerde uygulanır.
        /// </summary>
        public static void EnableLevel(LogSinks? sinkType = null) => Instance.SetSinkProperty(sinkType, (s, v) => s.ShowLevel = v, true);
        public static void DisableLevel(LogSinks? sinkType = null) => Instance.SetSinkProperty(sinkType, (s, v) => s.ShowLevel = v, false);

        /// <summary>
        /// Source (App name) gösterimini aç/kapat. sinkType belirtilmezse tüm sink'lerde uygulanır.
        /// </summary>
        public static void EnableSource(LogSinks? sinkType = null) => Instance.SetSinkProperty(sinkType, (s, v) => s.ShowSource = v, true);
        public static void DisableSource(LogSinks? sinkType = null) => Instance.SetSinkProperty(sinkType, (s, v) => s.ShowSource = v, false);

        private void SetSinkProperty(LogSinks? sinkType, Action<ILogSink, bool> setter, bool value)
        {
            lock (_sinksLock)
            {
                foreach (var sink in _sinks)
                {
                    if (sinkType == null || sink.SinkType == sinkType.Value)
                    {
                        setter(sink, value);
                    }
                }
            }
        }

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
            if (!Instance.IsEnabled || message == null)
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
        // LOG METODLARI - INSTANCE (Inject edilmiş logger üzerinden yazma)
        // ====================================================================

        public void LogRawInstance(string message, LogSinks sinks = LogSinks.All)
        {
            LogRawInstance(message, null, sinks);
        }

        public void LogRawInstance(string message, ConsoleColor? color, LogSinks sinks = LogSinks.All)
        {
            if (!IsEnabled || message == null)
                return;

            var entry = new LogEntry(
                level: LogLevel.Info,
                message: message,
                targetSinks: sinks,
                isRaw: true,
                color: color
            );

            AddToBuffer(entry);
            SendToSinks(entry);
        }

        // ====================================================================
        // LOG METODLARI - STATIC (Global logging to all sinks)
        // ====================================================================

        /// <summary>
        /// Genel log metodu (variadic) - STATIC
        /// </summary>
        public static void Log(params object[] args)
        {
            Instance.LogInternal(LogLevel.Info, null, LogSinks.All, null, args);
        }

        /// <summary>
        /// Genel log metodu - renkli - STATIC
        /// </summary>
        public static void Log(ConsoleColor color, params object[] args)
        {
            Instance.LogInternal(LogLevel.Info, null, LogSinks.All, color, args);
        }

        /// <summary>
        /// Log seviyesi ve sink seçimiyle - STATIC
        /// </summary>
        public static void Log(LogLevel level, LogSinks sinks, params object[] args)
        {
            Instance.LogInternal(level, null, sinks, null, args);
        }

        /// <summary>
        /// Log seviyesi, source ve sink seçimiyle - STATIC
        /// </summary>
        public static void Log(LogLevel level, string source, LogSinks sinks, params object[] args)
        {
            Instance.LogInternal(level, source, sinks, null, args);
        }

        public static void LogTrace(params object[] args) => Instance.LogInternal(LogLevel.Trace, null, LogSinks.All, null, args);
        public static void LogDebug(params object[] args) => Instance.LogInternal(LogLevel.Debug, null, LogSinks.All, null, args);
        public static void LogInfo(params object[] args) => Instance.LogInternal(LogLevel.Info, null, LogSinks.All, null, args);
        public static void LogWarning(params object[] args) => Instance.LogInternal(LogLevel.Warning, null, LogSinks.All, null, args);
        public static void LogError(params object[] args) => Instance.LogInternal(LogLevel.Error, null, LogSinks.All, null, args);
        public static void LogFatal(params object[] args) => Instance.LogInternal(LogLevel.Fatal, null, LogSinks.All, null, args);

        public static void Log(string message, ConsoleColor color) => Instance.LogInternal(LogLevel.Info, null, LogSinks.All, color, message);

        public static void LogTrace(ConsoleColor color, params object[] args) => Instance.LogInternal(LogLevel.Trace, null, LogSinks.All, color, args);
        public static void LogTrace(string message, ConsoleColor color) => Instance.LogInternal(LogLevel.Trace, null, LogSinks.All, color, message);

        public static void LogDebug(ConsoleColor color, params object[] args) => Instance.LogInternal(LogLevel.Debug, null, LogSinks.All, color, args);
        public static void LogDebug(string message, ConsoleColor color) => Instance.LogInternal(LogLevel.Debug, null, LogSinks.All, color, message);

        public static void LogInfo(ConsoleColor color, params object[] args) => Instance.LogInternal(LogLevel.Info, null, LogSinks.All, color, args);
        public static void LogInfo(string message, ConsoleColor color) => Instance.LogInternal(LogLevel.Info, null, LogSinks.All, color, message);

        public static void LogWarning(ConsoleColor color, params object[] args) => Instance.LogInternal(LogLevel.Warning, null, LogSinks.All, color, args);
        public static void LogWarning(string message, ConsoleColor color) => Instance.LogInternal(LogLevel.Warning, null, LogSinks.All, color, message);

        public static void LogError(ConsoleColor color, params object[] args) => Instance.LogInternal(LogLevel.Error, null, LogSinks.All, color, args);
        public static void LogError(string message, ConsoleColor color) => Instance.LogInternal(LogLevel.Error, null, LogSinks.All, color, message);

        public static void LogFatal(ConsoleColor color, params object[] args) => Instance.LogInternal(LogLevel.Fatal, null, LogSinks.All, color, args);
        public static void LogFatal(string message, ConsoleColor color) => Instance.LogInternal(LogLevel.Fatal, null, LogSinks.All, color, message);

        // ====================================================================
        // LOG METODLARI - INSTANCE
        // ====================================================================

        /// <summary>
        /// Genel log metodu (variadic) - INSTANCE
        /// </summary>
        public void WriteLog(params object[] args)
        {
            LogInternal(LogLevel.Info, null, LogSinks.All, null, args);
        }

        /// <summary>
        /// Log seviyesi ve sink seçimiyle - INSTANCE
        /// </summary>
        public void WriteLog(LogLevel level, LogSinks sinks, params object[] args)
        {
            LogInternal(level, null, sinks, null, args);
        }

        /// <summary>
        /// Log seviyesi, source ve sink seçimiyle - INSTANCE
        /// </summary>
        public void WriteLog(LogLevel level, string source, LogSinks sinks, params object[] args)
        {
            LogInternal(level, source, sinks, null, args);
        }

        public void WriteTrace(params object[] args) => LogInternal(LogLevel.Trace, null, LogSinks.All, null, args);
        public void WriteDebug(params object[] args) => LogInternal(LogLevel.Debug, null, LogSinks.All, null, args);
        public void WriteInfo(params object[] args) => LogInternal(LogLevel.Info, null, LogSinks.All, null, args);
        public void WriteWarning(params object[] args) => LogInternal(LogLevel.Warning, null, LogSinks.All, null, args);
        public void WriteError(params object[] args) => LogInternal(LogLevel.Error, null, LogSinks.All, null, args);
        public void WriteFatal(params object[] args) => LogInternal(LogLevel.Fatal, null, LogSinks.All, null, args);

        /// <summary>
        /// Formatsız log - timestamp/level prefix olmadan düz metin yazar.
        /// Tablo, banner, harici çıktı (Python stdout vb.) için kullanılır.
        /// </summary>
        public void WriteRaw(string message, LogSinks sinks = LogSinks.All)
        {
            if (!IsEnabled || message == null) return;
            var entry = new LogEntry(LogLevel.Info, message, targetSinks: sinks, isRaw: true);
            AddToBuffer(entry);
            SendToSinks(entry);
        }

        public void WriteRaw(string message, ConsoleColor? color, LogSinks sinks = LogSinks.All)
        {
            if (!IsEnabled || message == null) return;
            var entry = new LogEntry(LogLevel.Info, message, targetSinks: sinks, isRaw: true, color: color);
            AddToBuffer(entry);
            SendToSinks(entry);
        }

        // ====================================================================
        // İÇ METOD - LOG İŞLEME
        // ====================================================================

        private void LogInternal(LogLevel level, string? source, LogSinks targetSinks, ConsoleColor? color, params object[] args)
        {
            if (!IsEnabled || _isDisposed || args == null || args.Length == 0)
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
                    targetSinks: targetSinks,
                    color: color
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

// ============================================================================
// TODO: Static -> Instance method refactoring
// ============================================================================
//
// YAPILAN:
//   - LogRawInstance() eklendi (non-static versiyon).
//     SingleTrader gibi siniflar inject edilen _logger uzerinden
//     instance method ile log yazabiliyor.
//     Boylece ileride sinifa ozel logger verilebilir (farkli sink'ler vb.)
//
// YAPILACAK:
//   Asagidaki static method'larin da non-static versiyonlari eklenmeli:
//
//   Static method                  ->  Instance method (onerilen isim)
//   ─────────────────────────────────────────────────────────────────────
//   LogRaw(message, color, sinks)  ->  LogRawInstance(message, color, sinks)   [DONE]
//   Log(params object[])           ->  WriteLog(params object[])               [DONE - zaten var]
//   Log(ConsoleColor, params)      ->  WriteLog(ConsoleColor, params)          [TODO]
//   Log(message, ConsoleColor)     ->  WriteLog(message, ConsoleColor)         [TODO]
//   LogTrace(params)               ->  WriteTrace(params)                      [DONE - zaten var]
//   LogDebug(params)               ->  WriteDebug(params)                      [DONE - zaten var]
//   LogInfo(params)                ->  WriteInfo(params)                       [DONE - zaten var]
//   LogWarning(params)             ->  WriteWarning(params)                    [DONE - zaten var]
//   LogError(params)               ->  WriteError(params)                      [DONE - zaten var]
//   LogFatal(params)               ->  WriteFatal(params)                      [DONE - zaten var]
//   LogTrace(color, params)        ->  WriteTrace(color, params)               [TODO]
//   LogDebug(color, params)        ->  WriteDebug(color, params)               [TODO]
//   LogInfo(color, params)         ->  WriteInfo(color, params)                [TODO]
//   LogWarning(color, params)      ->  WriteWarning(color, params)             [TODO]
//   LogError(color, params)        ->  WriteError(color, params)               [TODO]
//   LogFatal(color, params)        ->  WriteFatal(color, params)               [TODO]
//   LogError(message, Exception)   ->  WriteError(message, Exception)          [TODO]
//   DisableConsoleSink()           ->  (instance versiyon)                     [TODO]
//   EnableConsoleSink()            ->  (instance versiyon)                     [TODO]
//
// HEDEF:
//   - Her sinif kendi logger instance'i uzerinden yazabilsin
//   - Sinifa ozel sink konfigurasyonu mumkun olsun
//     (ornegin SingleTrader sadece dosyaya yazsin, konsola yazmasin)
//   - Static method'lar geriye uyumluluk icin kalacak (global/singleton loglama)
//   - Yeni kodlarda instance method'lar tercih edilecek
// ============================================================================
