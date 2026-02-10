using System;
using System.Collections.Generic;

namespace AlgoTrade.Core.Logging
{
    /// <summary>
    /// Console'a düz metin yazmak için wrapper.
    /// LogManager'ın formatlı log sistemi yerine, doğrudan Console.Write/WriteLine kullanır.
    /// ShowTimestamp/ShowLevel/ShowSource ile opsiyonel prefix eklenebilir.
    /// İleride ConsoleManager yazıldığında, bu sınıf ConsoleManager üzerinden yazacak.
    /// </summary>
    public class ConsoleLogger
    {
        public bool ShowTimestamp { get; set; } = false;
        public bool ShowLevel { get; set; } = false;
        public bool ShowSource { get; set; } = false;
        public string Source { get; set; } = "App";

        public void Write(string message)
        {
            Console.Write(BuildMessage(message));
        }

        public void WriteLine(string message)
        {
            Console.WriteLine(BuildMessage(message));
        }

        public void WriteLine()
        {
            Console.WriteLine();
        }

        public void Write(string message, ConsoleColor color)
        {
            var original = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.Write(BuildMessage(message));
            Console.ForegroundColor = original;
        }

        public void WriteLine(string message, ConsoleColor color)
        {
            var original = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine(BuildMessage(message));
            Console.ForegroundColor = original;
        }

        public void Clear()
        {
            Console.Clear();
        }

        private string BuildMessage(string message)
        {
            if (!ShowTimestamp && !ShowLevel && !ShowSource)
                return message;

            var parts = new List<string>();
            if (ShowTimestamp) parts.Add($"[{DateTime.Now:HH:mm:ss.fff}]");
            if (ShowLevel) parts.Add("[Info]");
            if (ShowSource) parts.Add($"[{Source}]");
            parts.Add(message);
            return string.Join(" ", parts);
        }
    }
}
