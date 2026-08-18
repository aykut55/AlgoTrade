using AlgoTrade.Core.Logging;
using AlgoTrade.Core.Trading.Indicators;
using AlgoTrade.Core.Trading.Query;
using AlgoTrade.Core.StockDataReader;

namespace AlgoTrade.Core.Trading;

/// <summary>
/// Aynı veri üzerinde birden fazla sorguyu (<see cref="IQuery"/>) BAĞIMSIZ olarak çalıştırır
/// (bkz. docs/tarama-motoru-plan.md — Sorgu Tarama Matrisi, Senaryo 3, "Yapı Taşı B"nin Sorgu
/// karşılığı). <see cref="MultipleTrader"/>'ın strateji-ekseni consensus'unun (Net/Majority/
/// All/Any) Sorgu karşılığı YOK — kullanıcı ile netleşen karar gereği (sorgu salt okunur bir
/// kontrol, pozisyon üretmiyor, birleştirilecek bir "karar" yok), N sorgu çalıştırılır, N sonuç
/// hiç birleştirilmeden ayrı ayrı raporlanır.
///
/// MultipleTrader'a bağlı/onun bir varyantı DEĞİL — MultipleTrader'ın consensus/liste-yazma
/// mantığı tamamen trade-odaklı (bkz. araştırma notu, MultipleTrader.cs hiç "Query" içermiyor),
/// bu yüzden sıfırdan, çok daha basit bir sınıf. Her "child" aslında tam bağımsız bir
/// <see cref="SingleTrader"/> — <see cref="TraderRunMode.QueryOnly"/> modunda, kendi
/// <see cref="IQuery"/>'si ile kurulu, aynı paylaşılan veri/indikatör setini kullanıyor.
/// QueryOnly modda SingleTrader.Run() sadece ExecuteQuery(i) çağırıyor (pozisyon/emir mantığına
/// hiç girmiyor), o yüzden N tane child'ı yan yana çalıştırmak side-effect-free ve güvenli.
/// </summary>
public class MultipleQuery : IDisposable
{
    private readonly List<StockData> _data;
    private readonly IndicatorManager _indicators;
    private readonly LogManager? _logger;

    public List<SingleTrader> Traders { get; } = new();

    public MultipleQuery(List<StockData> data, IndicatorManager indicators, LogManager? logger)
    {
        _data = data;
        _indicators = indicators;
        _logger = logger;
    }

    /// <summary>Bağımsız çalışacak bir sorguyu ekler — çağıran taraf IQuery'yi (QueryRegistry ile) kendi kurar.</summary>
    public SingleTrader AddChildQuery(int id, IQuery query)
    {
        var trader = new SingleTrader(id, $"query_{id}", _data, _indicators, _logger);
        trader.Reset();
        trader.RunMode = TraderRunMode.QueryOnly;
        trader.SetQuery(query);
        trader.Init();

        Traders.Add(trader);
        return trader;
    }

    /// <summary>Tüm child sorguları barIndex için çalıştırır (birleştirme yok, her biri kendi state'ini biriktirir).</summary>
    public void Run(int barIndex)
    {
        foreach (var trader in Traders)
            trader.Run(barIndex);
    }

    /// <summary>Her child'ın Finalize()'ını çağırır (SorguOzeti'nin son bar için doldurulması için).</summary>
    #pragma warning disable CS0465
    public void Finalize()
    {
        foreach (var trader in Traders)
            trader.Finalize();
    }
    #pragma warning restore CS0465

    public void Dispose()
    {
        foreach (var trader in Traders)
            trader.Dispose();
        Traders.Clear();
    }
}
