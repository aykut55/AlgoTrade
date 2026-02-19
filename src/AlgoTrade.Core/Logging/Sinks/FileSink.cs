using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace AlgoTrade.Core.Logging.Sinks
{
    /// <summary>
    /// File sink - Dosyaya batch yazma ile log yazar
    /// </summary>
    public class FileSink : ILogSink
    {
        private readonly string _filePath;
        private readonly ConcurrentQueue<LogEntry> _writeQueue = new ConcurrentQueue<LogEntry>();
        private readonly System.Threading.Timer _flushTimer;
        private readonly object _fileLock = new object();
        private bool _isDisposed;

        public string Name => "File";
        public LogSinks SinkType => LogSinks.File;
        public bool IsEnabled { get; set; } = true;

        public bool ShowTimestamp { get; set; } = true;
        public bool ShowLevel { get; set; } = true;
        public bool ShowSource { get; set; } = true;

        /// <summary>
        /// Flush intervali (ms) - Default 1000ms
        /// </summary>
        public int FlushIntervalMs { get; set; } = 1000;

        public FileSink(string filePath, bool appendMode = false)
        {
            _filePath = filePath;

            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (!appendMode && File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            _flushTimer = new System.Threading.Timer(_ => Flush(), null, FlushIntervalMs, FlushIntervalMs);
        }

        public FileSink(string fileDirectory, string fileName, bool appendMode = false)
            : this(Path.Combine(fileDirectory, fileName), appendMode)
        {
        }

        public void Write(LogEntry entry)
        {
            if (_isDisposed || !IsEnabled)
                return;

            _writeQueue.Enqueue(entry);

            if (_writeQueue.Count > 100)
            {
                Flush();
            }
        }

        public void Flush()
        {
            if (_isDisposed || _writeQueue.IsEmpty)
                return;

            lock (_fileLock)
            {
                try
                {
                    var entriesToWrite = new List<LogEntry>();
                    while (_writeQueue.TryDequeue(out var entry))
                    {
                        entriesToWrite.Add(entry);
                    }

                    if (entriesToWrite.Count > 0)
                    {
                        var lines = entriesToWrite.Select(e => e.IsRaw ? e.Message : e.ToString(ShowTimestamp, ShowLevel, ShowSource));
                        using var fs = new FileStream(_filePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                        using var writer = new StreamWriter(fs, System.Text.Encoding.UTF8);
                        foreach (var line in lines)
                            writer.WriteLine(line);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"FileSink error: {ex.Message}");
                }
            }
        }

        public void Clear()
        {
            lock (_fileLock)
            {
                try
                {
                    _writeQueue.Clear();
                    if (File.Exists(_filePath))
                    {
                        File.Delete(_filePath);
                    }
                }
                catch { }
            }
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            _flushTimer?.Dispose();
            Flush();
        }
    }
}
