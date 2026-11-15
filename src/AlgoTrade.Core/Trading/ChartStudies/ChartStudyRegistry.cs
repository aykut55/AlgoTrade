using System.Reflection;

namespace AlgoTrade.Core.Trading.ChartStudies;

public sealed class ChartStudyRegistry
{
    private readonly Dictionary<string, Type> _studyTypes = new(StringComparer.OrdinalIgnoreCase);

    public ChartStudyRegistry() => AutoRegister();

    public void AutoRegister(Assembly? assembly = null)
    {
        Assembly target = assembly ?? typeof(BaseChartStudy).Assembly;
        foreach (Type type in target.GetTypes().Where(type =>
                     type.IsClass && !type.IsAbstract && typeof(BaseChartStudy).IsAssignableFrom(type)))
        {
            _studyTypes[type.Name] = type;
        }
    }

    public IReadOnlyCollection<string> GetStudyNames() =>
        _studyTypes.Keys.OrderBy(name => name).ToArray();

    public IChartStudy Create(string? studyName = null)
    {
        string resolvedName = string.IsNullOrWhiteSpace(studyName)
            ? nameof(IndicatorDashboardChartStudy)
            : studyName;

        if (!_studyTypes.TryGetValue(resolvedName, out Type? type))
            throw new ArgumentException(
                $"Unknown chart study: {resolvedName}. Available: {string.Join(", ", GetStudyNames())}",
                nameof(studyName));

        return (IChartStudy)(Activator.CreateInstance(type)
            ?? throw new InvalidOperationException($"Chart study oluşturulamadı: {resolvedName}"));
    }
}
