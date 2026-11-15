namespace AlgoTrade.Core.Trading.ChartStudies;

public abstract class BaseChartStudy : IChartStudy
{
    public abstract string Name { get; }
    public virtual string DisplayName => Name;
    public abstract string ScriptFileName { get; }
    public abstract string BuildScript();
}
