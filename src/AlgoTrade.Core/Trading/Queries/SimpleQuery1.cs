using System.Collections.Generic;
using AlgoTrade.Core;
using AlgoTrade.Core.Trading.Query;
using AlgoTrade.Core.Trading.Indicators;

namespace AlgoTrade.Core.Trading.Queries
{
    /// <summary>
    /// Simple Moving Average Query
    /// Returns MA values, cross signals, and distance metrics for the last bar
    /// Example query results:
    /// [0] = Close
    /// [1] = MA8
    /// [2] = MA200
    /// [3] = CrossSignal (1 if MA8 > MA200, -1 otherwise)
    /// [4] = Distance% = (Close - MA200) / MA200 * 100
    /// </summary>
    public class SimpleQuery1 : BaseQuery
    {
        public override string Name => "Simple MA Query";

        private readonly int _ma8Period;
        private readonly int _ma200Period;
        private readonly int _choice;
        private double[]? _ma8;
        private double[]? _ma200;

        // Parametresiz constructor (eski kullanımlar için)
        public SimpleQuery1(int ma8Period = 8, int ma200Period = 200, int choice = 0)
        {
            _ma8Period = ma8Period;
            _ma200Period = ma200Period;
            _choice = choice;

            Parameters["MA8Period"] = ma8Period;
            Parameters["MA200Period"] = ma200Period;
            Parameters["choice"] = choice;
        }

        // Parametreli constructor (yeni kullanım)
        public SimpleQuery1(List<StockData> data, IndicatorManager indicators,
                            int ma8Period = 8, int ma200Period = 200, int choice = 0)
        {
            _ma8Period = ma8Period;
            _ma200Period = ma200Period;
            _choice = choice;

            Parameters["MA8Period"] = ma8Period;
            Parameters["MA200Period"] = ma200Period;
            Parameters["choice"] = choice;

            // Initialize base query
            Initialize(data, indicators);
        }

        public override void OnInit()
        {
            if (!IsInitialized)
                return;

            // Calculate moving averages
            var closes = Indicators.GetClosePrices();
            _ma8 = Indicators.MA.SMA(closes, _ma8Period);
            _ma200 = Indicators.MA.SMA(closes, _ma200Period);

            Log($"SimpleMAQuery initialized: MA8={_ma8Period}, MA200={_ma200Period}, Choice={_choice}");
        }

        public override List<object> OnExecute(int lastBarIndex)
        {
            var results = new List<object>();
            ColumnNames.Clear(); // Clear previous column names

            // Check if we have enough data
            int barCount = lastBarIndex + 1;
            int requiredBars = _ma200Period;
            if (barCount < requiredBars)
            {
                //LogWarning($"Not enough data for query. Need {requiredBars} bars, have {barCount}");
                return results;
            }

            if (_choice == 0)
            {
                // Column: Close
                double lastClose = Data[lastBarIndex].Close;
                results.Add(lastClose);
                ColumnNames.Add("Close");

                // Column: MA8
                double lastMA8 = _ma8![lastBarIndex];
                results.Add(lastMA8);
                ColumnNames.Add($"MA{_ma8Period}");

                // Column: MA200
                double lastMA200 = _ma200![lastBarIndex];
                results.Add(lastMA200);
                ColumnNames.Add($"MA{_ma200Period}");

                // Column: CrossSignal
                int crossSignal = lastMA8 > lastMA200 ? 1 : -1;
                results.Add(crossSignal);
                ColumnNames.Add("CrossSignal");

                // Column: Distance%
                double distancePercent = (lastClose - lastMA200) / lastMA200 * 100.0;
                results.Add(distancePercent);
                ColumnNames.Add("Distance%");

                // Optional: Add strategy signal if trader has a strategy
                if (Trader?.Strategy != null)
                {
                    // Column: StrategySignal
                    var signal = Trader.strategySignal;
                    results.Add(signal.ToString());
                    ColumnNames.Add("StrategySignal");
                }

                // Optional: Add position info if trader has open position
                if (Trader != null)
                {
                    // Column: LastSignal
                    results.Add(Trader.SonYon ?? "F");
                    ColumnNames.Add("LastSignal");

                    // Column: BarsSinceSignal
                    results.Add(Trader.SonSinyaldenBeriBarSayisi);
                    ColumnNames.Add("BarsSinceSignal");

                    // Column: P&L
                    results.Add(Trader.SonKarZararFiyat);
                    ColumnNames.Add("P&L");

                    // Column: P&L%
                    results.Add(Trader.SonKarZararYuzde);
                    ColumnNames.Add("P&L%");
                }
            }
            else
            {

            }

            return results;
        }
    }
}
