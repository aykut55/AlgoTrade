using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace AlgoTrade.Core.Logging.Sinks
{
    /// <summary>
    /// Network sink - UDP ile log gönderir
    /// </summary>
    public class NetworkSink : ILogSink
    {
        private readonly UdpClient _udpClient;
        private readonly IPEndPoint _remoteEndPoint;
        private readonly ConcurrentQueue<LogEntry> _sendQueue = new ConcurrentQueue<LogEntry>();
        private bool _isDisposed;

        public string Name => "Network";
        public LogSinks SinkType => LogSinks.Network;
        public bool IsEnabled { get; set; } = true;

        public bool ShowTimestamp { get; set; } = true;
        public bool ShowLevel { get; set; } = true;
        public bool ShowSource { get; set; } = true;

        /// <summary>
        /// Log format (short, medium, default, long, json)
        /// </summary>
        public string LogFormat { get; set; } = "json";

        public NetworkSink(string remoteHost, int remotePort)
        {
            _udpClient = new UdpClient();
            _remoteEndPoint = new IPEndPoint(IPAddress.Parse(remoteHost), remotePort);
        }

        public void Write(LogEntry entry)
        {
            if (_isDisposed || !IsEnabled)
                return;

            _sendQueue.Enqueue(entry);
            Task.Run(() => SendPendingLogs());
        }

        private void SendPendingLogs()
        {
            while (_sendQueue.TryDequeue(out var entry))
            {
                try
                {
                    var logText = entry.ToString(LogFormat);
                    var bytes = Encoding.UTF8.GetBytes(logText);
                    _udpClient.Send(bytes, bytes.Length, _remoteEndPoint);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"NetworkSink error: {ex.Message}");
                }
            }
        }

        public void Flush()
        {
            SendPendingLogs();
        }

        public void Clear()
        {
            _sendQueue.Clear();
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            Flush();
            _udpClient?.Close();
            _udpClient?.Dispose();
        }
    }
}
