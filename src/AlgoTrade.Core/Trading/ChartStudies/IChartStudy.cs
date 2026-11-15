namespace AlgoTrade.Core.Trading.ChartStudies;

/// <summary>
/// Strateji veya sorgu üretmeden, grafik üzerinde incelenecek panel ve
/// indikatörlerin canlı plotter scriptini tanımlar.
/// </summary>
public interface IChartStudy
{
    string Name { get; }
    string DisplayName { get; }
    string ScriptFileName { get; }
    string BuildScript();
}
