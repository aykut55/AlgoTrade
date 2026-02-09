using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text;
namespace AlgoTrade.Core.StockDataReader
{
    public class StockDataReader : IDisposable
    {
        public enum FilterMode
        {
            All,
            LastN,
            FirstN,
            IndexRange,
            AfterDateTime,
            BeforeDateTime,
            DateTimeRange
        }

        private readonly ConcurrentDictionary<string, string> _metaData = new();
        private readonly List<string> _metaDataLines = new();
        private readonly Stopwatch _stopwatch = new();
        private List<StockData> _stockDataList = new();
        private bool _isDisposed;

        public int DataReadCount { get; private set; }
        public bool IsMetaDataRead { get; private set; }
        public bool IsDataRead { get; private set; }
        public List<string> StockMetaDataLines => _metaDataLines;
        public ConcurrentDictionary<string, string> StockMetaData => _metaData;
        public List<StockData> StockDataList => _stockDataList;

        public event Action<StockDataReader, ConcurrentDictionary<string, string>>? OnReadMetaData;
        public event Action<StockDataReader, List<StockData>, long>? OnReadData;
        public event Action<StockDataReader, int, bool>? OnProgress;

        public void StartTimer() => _stopwatch.Start();
        public void ReStartTimer() => _stopwatch.Restart();
        public void StopTimer() => _stopwatch.Stop();
        public long GetElapsedTimeMsec() => _stopwatch.ElapsedMilliseconds;
        public TimeSpan GetElapsedTime() => _stopwatch.Elapsed;

        public void Clear()
        {
            _stopwatch.Reset();
            DataReadCount = 0;
            IsMetaDataRead = false;
            IsDataRead = false;
            _metaData.Clear();
            _metaDataLines.Clear();
            _stockDataList.Clear();
        }

        public ConcurrentDictionary<string, string> ReadMetaData(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("The specified data file was not found.", filePath);

            _metaData.Clear();
            _metaDataLines.Clear();

            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan);
            using var reader = new StreamReader(fileStream);

            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (!line.TrimStart().StartsWith('#'))
                    break;

                _metaDataLines.Add(line);

                var content = line.TrimStart('#').Trim();
                var separatorIndex = content.IndexOf(':');
                if (separatorIndex > 0)
                {
                    var key = content[..separatorIndex].Trim();
                    var value = content[(separatorIndex + 1)..].Trim();
                    _metaData[key] = value;
                }
            }

            IsMetaDataRead = true;
            OnReadMetaData?.Invoke(this, _metaData);
            return _metaData;
        }

        public List<StockData> ReadDataFast(string filePath, FilterMode mode = FilterMode.All, int n1 = 0, int n2 = 0, DateTime? dt1 = null, DateTime? dt2 = null)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("The specified data file was not found.", filePath);

            var culture = new CultureInfo("tr-TR");
            var bag = new ConcurrentBag<StockData>();
            int processedCount = 0;

            File.ReadLines(filePath)
                .AsParallel()
                .Where(line => !string.IsNullOrWhiteSpace(line) && !line.TrimStart().StartsWith('#') && !line.TrimStart().StartsWith("Id"))
                .ForAll(line =>
                {
                    var parts = line.Split(';').Select(p => p.Trim()).Where(p => !string.IsNullOrEmpty(p)).ToArray();
                    if (parts.Length < 8)
                        return;

                    try
                    {
                        bag.Add(CreateStockData(parts, culture));
                        var count = Interlocked.Increment(ref processedCount);
                        if (count % 1000 == 0)
                            OnProgress?.Invoke(this, count, false);
                    }
                    catch (FormatException)
                    {
                        // Skip malformed lines
                    }
                });

            var sorted = bag.ToList();
            sorted.Sort((x, y) => x.Id.CompareTo(y.Id));

            var filtered = ApplyFilter(sorted, mode, n1, n2, dt1, dt2);
            _stockDataList = filtered;
            DataReadCount = filtered.Count;
            IsDataRead = true;
            OnProgress?.Invoke(this, DataReadCount, true);
            OnReadData?.Invoke(this, filtered, _stopwatch.ElapsedMilliseconds);
            return filtered;
        }

        private static List<StockData> ApplyFilter(List<StockData> data, FilterMode mode, int n1, int n2, DateTime? dt1, DateTime? dt2)
        {
            return mode switch
            {
                FilterMode.All => data,
                FilterMode.LastN => data.TakeLast(n1).ToList(),
                FilterMode.FirstN => data.Take(n1).ToList(),
                FilterMode.IndexRange => n1 >= 0 && n2 >= n1 && n1 < data.Count
                    ? data.Skip(n1).Take(Math.Min(n2, data.Count - 1) - n1 + 1).ToList()
                    : new List<StockData>(),
                FilterMode.AfterDateTime => dt1.HasValue ? data.Where(x => x.DateTime >= dt1.Value).ToList() : data,
                FilterMode.BeforeDateTime => dt1.HasValue ? data.Where(x => x.DateTime <= dt1.Value).ToList() : data,
                FilterMode.DateTimeRange => dt1.HasValue && dt2.HasValue
                    ? data.Where(x => x.DateTime >= dt1.Value && x.DateTime <= dt2.Value).ToList()
                    : data,
                _ => data
            };
        }

        public string Head(int n = 5)
        {
            return FormatTable(_stockDataList.Take(n).ToList());
        }

        public string Tail(int n = 5)
        {
            return FormatTable(_stockDataList.TakeLast(n).ToList());
        }

        public string ToTable()
        {
            return FormatTable(_stockDataList);
        }

        public string ToTable(int start, int end)
        {
            if (start < 0) start = 0;
            if (end >= _stockDataList.Count) end = _stockDataList.Count - 1;
            if (start > end) return "(invalid range)";
            return FormatTable(_stockDataList.Skip(start).Take(end - start + 1).ToList());
        }

        private static string FormatTable(List<StockData> data)
        {
            if (data.Count == 0)
                return "(empty)";

            var sb = new StringBuilder();
            var header = $"{"Id",-8}{"DateTime",-21}{"Date",-13}{"Time",-10}{"Open",12}{"High",12}{"Low",12}{"Close",12}      {"Volume",-14}{"Size",-10}{"Diff",10}{"Chg%",8}  {"EpochTime",-14}";
            sb.AppendLine(header);
            sb.AppendLine(new string('-', header.Length));

            foreach (var d in data)
            {
                sb.AppendLine(
                    $"{d.Id,-8}{d.DateTime:yyyy.MM.dd HH:mm:ss}  {d.Date:yyyy.MM.dd}   {d.Time:hh\\:mm\\:ss}  {d.Open,12:F2}{d.High,12:F2}{d.Low,12:F2}{d.Close,12:F2}      {d.Volume,-14}{d.Size,-10}{d.Diff,10:F2}{d.ChangePct,8:F2}  {d.EpochTime,-14}");
            }

            sb.AppendLine($"\n[{data.Count} rows]");
            return sb.ToString();
        }

        private static string FormatSigned(double value)
        {
            return value >= 0 ? $" {value:F2}" : $"{value:F2}";
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            _metaData.Clear();
            _metaDataLines.Clear();
            _stockDataList.Clear();
        }

        private static StockData CreateStockData(string[] parts, CultureInfo culture)
        {
            var dateTimePart = parts[1];

            if (parts.Length >= 9 && parts[1].Length == 10 && parts[2].Contains(':'))
            {
                dateTimePart = $"{parts[1]} {parts[2]}";
                var newParts = new string[parts.Length - 1];
                newParts[0] = parts[0];
                newParts[1] = dateTimePart;
                Array.Copy(parts, 3, newParts, 2, parts.Length - 3);
                parts = newParts;
            }
            else if (dateTimePart.Contains(';'))
            {
                dateTimePart = dateTimePart.Replace(";", " ");
            }

            var dateTime = DateTime.ParseExact(dateTimePart, "yyyy.MM.dd HH:mm:ss", CultureInfo.InvariantCulture);

            return new StockData
            {
                Id = int.Parse(parts[0]),
                DateTime = dateTime,
                Date = dateTime.Date,
                Time = dateTime.TimeOfDay,
                Open = double.Parse(parts[2], CultureInfo.InvariantCulture),
                High = double.Parse(parts[3], CultureInfo.InvariantCulture),
                Low = double.Parse(parts[4], CultureInfo.InvariantCulture),
                Close = double.Parse(parts[5], CultureInfo.InvariantCulture),
                Volume = long.Parse(parts[6], NumberStyles.Any, culture),
                Size = long.Parse(parts[7], NumberStyles.Any, culture)
            };
        }
    }
}
