namespace AlgoTrade.Core.Trading;

public class AlgoTrader
{
    public string Name { get; }
    public bool IsRunning { get; private set; }

    public AlgoTrader(string name)
    {
        Name = name;
    }

    public void Start()
    {
        IsRunning = true;
        OnMessage($"AlgoTrader '{Name}' başlatıldı.");
    }

    public void Stop()
    {
        IsRunning = false;
        OnMessage($"AlgoTrader '{Name}' durduruldu.");
    }

    public event Action<string>? MessageReceived;

    private void OnMessage(string message)
    {
        MessageReceived?.Invoke(message);
    }
}
