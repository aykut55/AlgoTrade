using System;
using System.Collections.Generic;
using System.Linq;

namespace AlgoTrade.Core.Trading.Statistics
{
    /// <summary>
    /// Trade statistics and performance metrics
    /// </summary>
    public class TradeStatistics
    {
        #region Properties

        public int TotalTrades { get; set; }
        public int WinningTrades { get; set; }
        public int LosingTrades { get; set; }
        public double TotalProfit { get; set; }
        public double TotalLoss { get; set; }
        public double MaxDrawdown { get; set; }
        public double MaxDrawdownPct { get; set; }
        public List<double> Returns { get; set; }

        #endregion

        #region Constructor

        public TradeStatistics()
        {
            Returns = new List<double>();
            Reset();
        }

        #endregion

        #region Calculated Metrics

        /// <summary>
        /// Net profit (total profit - total loss)
        /// </summary>
        public double NetProfit => TotalProfit - TotalLoss;

        /// <summary>
        /// Win rate percentage
        /// </summary>
        public double WinRate => TotalTrades > 0 ? (double)WinningTrades / TotalTrades * 100.0 : 0;

        /// <summary>
        /// Average win
        /// </summary>
        public double AverageWin => WinningTrades > 0 ? TotalProfit / WinningTrades : 0;

        /// <summary>
        /// Average loss
        /// </summary>
        public double AverageLoss => LosingTrades > 0 ? TotalLoss / LosingTrades : 0;

        /// <summary>
        /// Profit factor
        /// </summary>
        public double ProfitFactor => TotalLoss > 0 ? TotalProfit / TotalLoss : 0;

        /// <summary>
        /// Sharpe ratio (simplified)
        /// </summary>
        public double SharpeRatio
        {
            get
            {
                if (Returns.Count == 0) return 0;
                var avgReturn = Returns.Average();
                var stdDev = CalculateStdDev(Returns);
                return stdDev > 0 ? avgReturn / stdDev : 0;
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Add a trade result
        /// </summary>
        public void AddTrade(double pnl)
        {
            TotalTrades++;
            Returns.Add(pnl);

            if (pnl > 0)
            {
                WinningTrades++;
                TotalProfit += pnl;
            }
            else if (pnl < 0)
            {
                LosingTrades++;
                TotalLoss += Math.Abs(pnl);
            }
        }

        /// <summary>
        /// Update drawdown
        /// </summary>
        public void UpdateDrawdown(double currentDrawdown, double currentDrawdownPct)
        {
            if (currentDrawdown > MaxDrawdown)
                MaxDrawdown = currentDrawdown;
            if (currentDrawdownPct > MaxDrawdownPct)
                MaxDrawdownPct = currentDrawdownPct;
        }

        /// <summary>
        /// Reset statistics
        /// </summary>
        public void Reset()
        {
            TotalTrades = 0;
            WinningTrades = 0;
            LosingTrades = 0;
            TotalProfit = 0;
            TotalLoss = 0;
            MaxDrawdown = 0;
            MaxDrawdownPct = 0;
            Returns.Clear();
        }

        /// <summary>
        /// Get summary report
        /// </summary>
        public string GetSummary()
        {
            return $@"
=== Trade Statistics ===
Total Trades:    {TotalTrades}
Winning Trades:  {WinningTrades}
Losing Trades:   {LosingTrades}
Win Rate:        {WinRate:F2}%
Net Profit:      {NetProfit:F2}
Total Profit:    {TotalProfit:F2}
Total Loss:      {TotalLoss:F2}
Average Win:     {AverageWin:F2}
Average Loss:    {AverageLoss:F2}
Profit Factor:   {ProfitFactor:F2}
Max Drawdown:    {MaxDrawdown:F2} ({MaxDrawdownPct:F2}%)
Sharpe Ratio:    {SharpeRatio:F2}
";
        }

        #endregion

        #region Helper Methods

        private double CalculateStdDev(List<double> values)
        {
            if (values.Count == 0) return 0;
            var avg = values.Average();
            var sumSquares = values.Sum(v => Math.Pow(v - avg, 2));
            return Math.Sqrt(sumSquares / values.Count);
        }

        #endregion
    }
}
