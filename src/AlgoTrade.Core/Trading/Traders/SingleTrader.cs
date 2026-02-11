using AlgoTrade.Core;
using AlgoTrade.Core.DataProvider;
using AlgoTrade.Core.Trading.Indicators;
using AlgoTrade.Core.Logging;

namespace AlgoTrade.Core.Trading;

public class SingleTrader : MarketDataProvider, IDisposable
{
    public event Action<SingleTrader, int>? OnReset;
    public event Action<SingleTrader, int>? OnInit;
    public event Action<SingleTrader, int>? OnRun;
    public event Action<SingleTrader, int>? OnFinal;
    public event Action<SingleTrader, int>? OnBeforeOrder;
    public event Action<SingleTrader, string, int>? OnNotifySignal;
    public event Action<SingleTrader, int>? OnAfterOrder;
    public event Action<SingleTrader, int, int>? OnProgress;
    public event Action<SingleTrader>? OnApplyUserFlags;    

    public SingleTrader(int id, string name, List<StockData> data, IndicatorManager indicators, LogManager? logger = null)
    {
    }
    public SingleTrader SetCallbacks(
        Action<SingleTrader, int>? onReset = null,
        Action<SingleTrader, int>? onInit = null,
        Action<SingleTrader, int>? onRun = null,
        Action<SingleTrader, int>? onFinal = null,
        Action<SingleTrader, int>? onBeforeOrders = null,
        Action<SingleTrader, string, int>? onNotifySignal = null,
        Action<SingleTrader, int>? onAfterOrders = null,
        Action<SingleTrader, int, int>? onProgress = null,
        Action<SingleTrader>? onApplyUserFlags = null)
    {
        if (onReset != null) OnReset = onReset;
        if (onInit != null) OnInit = onInit;
        if (onRun != null) OnRun = onRun;
        if (onFinal != null) OnFinal = onFinal;
        if (onBeforeOrders != null) OnBeforeOrder = onBeforeOrders;
        if (onAfterOrders != null) OnAfterOrder = onAfterOrders;
        if (onNotifySignal != null) OnNotifySignal = onNotifySignal;
        if (onProgress != null) OnProgress = onProgress;
        if (onApplyUserFlags != null) OnApplyUserFlags = onApplyUserFlags;

        return this;
    }


    public void Reset()
    {
        OnReset?.Invoke(this, 0);
    }

    public void Init()
    {
        OnInit?.Invoke(this, 0);
    }

    public void Run(int barIndex)
    {
        OnBeforeOrder?.Invoke(this, barIndex);

        // TODO: Strategy evaluate, sinyal üret, emir uygula

        OnAfterOrder?.Invoke(this, barIndex);

        OnProgress?.Invoke(this, barIndex, GetDataCount());
    }

    public void Finalize(bool dispose)
    {
        OnFinal?.Invoke(this, dispose ? 1 : 0);
    }
    public void Dispose()
    {
    }
}
