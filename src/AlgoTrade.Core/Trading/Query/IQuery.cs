using System.Collections.Generic;

namespace AlgoTrade.Core.Trading.Query;

/// <summary>
/// Query interface
/// All market queries must implement this interface
/// Similar to IStrategy but for querying market state instead of trading
/// </summary>
public interface IQuery : IDisposable
{
    /// <summary>
    /// Query name
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Query parameters
    /// </summary>
    Dictionary<string, object> Parameters { get; }

    /// <summary>
    /// Initialize query
    /// </summary>
    void OnInit();

    /// <summary>
    /// Column names from last OnExecute() call
    /// </summary>
    List<string> ColumnNames { get; }

    /// <summary>
    /// Execute query for the last bar
    /// Returns list of results (dynamic columns)
    /// Also populates ColumnNames property
    /// Example: [Close, MA8, MA200, CrossSignal, DistancePercent, ...]
    /// </summary>
    List<object> OnExecute(int lastBarIndex);

    /// <summary>
    /// Reset query to initial state
    /// </summary>
    void Reset();
}
