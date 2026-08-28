using System.Globalization;
using System.Reflection;
using System.Text.Json;
using AlgoTrade.Core;
using AlgoTrade.Core.Logging;
using AlgoTrade.Core.Trading.Indicators;
using AlgoTrade.Core.Trading.Strategy;

namespace AlgoTrade.Core.Trading.Strategies;

public sealed class StrategyRegistry
{
    private readonly Dictionary<string, Type> _strategyTypes = new(StringComparer.OrdinalIgnoreCase);

    public StrategyRegistry()
    {
        AutoRegister();
    }

    public void AutoRegister(Assembly? assembly = null)
    {
        var targetAssembly = assembly ?? typeof(BaseStrategy).Assembly;

        var strategyTypes = targetAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(BaseStrategy).IsAssignableFrom(t));

        foreach (var strategyType in strategyTypes)
        {
            _strategyTypes[strategyType.Name] = strategyType;
        }
    }

    public IReadOnlyCollection<string> GetStrategyNames()
    {
        return _strategyTypes.Keys.OrderBy(name => name).ToList();
    }

    public IStrategy CreateStrategy(
        List<StockData> data,
        IndicatorManager indicators,
        LogManager? logger,
        string? strategyName = null,
        Dictionary<string, object>? parameters = null)
    {
        var resolvedStrategyName = strategyName;
        if (string.IsNullOrWhiteSpace(resolvedStrategyName))
        {
            resolvedStrategyName = "SimpleMAStrategy";
            Log(logger, "Strategy name is null/empty. Falling back to SimpleMAStrategy.");
        }

        if (!_strategyTypes.TryGetValue(resolvedStrategyName, out var strategyType))
        {
            throw new ArgumentException(
                $"Unknown strategy: {resolvedStrategyName}. Available strategies: {string.Join(", ", GetStrategyNames())}",
                nameof(strategyName));
        }

        var safeParameters = parameters ?? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        IStrategy createdStrategy;
        if (TryCreateViaStaticFactory(strategyType, data, indicators, safeParameters, out var strategyFromFactory))
        {
            createdStrategy = strategyFromFactory;
        }
        else
        {
            createdStrategy = CreateFromBestMatchingConstructor(strategyType, data, indicators, safeParameters);
        }

        if (createdStrategy is BaseStrategy baseStrategy)
        {
            baseStrategy.SetLogger(logger);
        }

        return createdStrategy;
    }

    private static void Log(LogManager? logger, string message)
    {
        if (logger is not null)
        {
            logger.LogRawInstance(message);
            return;
        }

        LogManager.LogRaw(message);
    }

    private static bool TryCreateViaStaticFactory(
        Type strategyType,
        List<StockData> data,
        IndicatorManager indicators,
        Dictionary<string, object> parameters,
        out IStrategy strategy)
    {
        strategy = null!;

        var factory = strategyType
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(m =>
            {
                if (!typeof(IStrategy).IsAssignableFrom(m.ReturnType))
                    return false;

                if (!string.Equals(m.Name, "Create", StringComparison.Ordinal))
                    return false;

                var ps = m.GetParameters();
                return ps.Length == 3
                       && ps[0].ParameterType == typeof(List<StockData>)
                       && ps[1].ParameterType == typeof(IndicatorManager)
                       && typeof(IDictionary<string, object>).IsAssignableFrom(ps[2].ParameterType);
            });

        if (factory is null)
            return false;

        var created = factory.Invoke(null, new object[] { data, indicators, parameters });
        if (created is not IStrategy typed)
            throw new InvalidOperationException($"Static Create method for {strategyType.Name} did not return IStrategy.");

        strategy = typed;
        return true;
    }

    private static IStrategy CreateFromBestMatchingConstructor(
        Type strategyType,
        List<StockData> data,
        IndicatorManager indicators,
        Dictionary<string, object> parameters)
    {
        var parameterLookup = new Dictionary<string, object>(parameters, StringComparer.OrdinalIgnoreCase);
        var constructors = strategyType.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
        var errors = new List<string>();

        ConstructorInfo? bestCtor = null;
        object[]? bestArgs = null;
        int bestScore = -1;

        foreach (var ctor in constructors)
        {
            var ctorParams = ctor.GetParameters();
            if (ctorParams.Length < 2)
                continue;

            if (ctorParams[0].ParameterType != typeof(List<StockData>) || ctorParams[1].ParameterType != typeof(IndicatorManager))
                continue;

            var args = new object[ctorParams.Length];
            args[0] = data;
            args[1] = indicators;

            bool valid = true;
            int score = 0;

            for (int i = 2; i < ctorParams.Length; i++)
            {
                var p = ctorParams[i];

                if (parameterLookup.TryGetValue(p.Name ?? string.Empty, out var rawValue))
                {
                    try
                    {
                        args[i] = ConvertToTargetType(rawValue, p.ParameterType);
                        score++;
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"{strategyType.Name}.{p.Name}: {ex.Message}");
                        valid = false;
                        break;
                    }
                }
                else if (p.HasDefaultValue)
                {
                    args[i] = p.DefaultValue!;
                }
                else
                {
                    valid = false;
                    break;
                }
            }

            if (!valid)
                continue;

            if (score > bestScore)
            {
                bestScore = score;
                bestCtor = ctor;
                bestArgs = args;
            }
        }

        if (bestCtor is null || bestArgs is null)
        {
            var errorText = errors.Count > 0 ? $" Conversion errors: {string.Join(" | ", errors)}" : string.Empty;
            throw new InvalidOperationException(
                $"No compatible constructor found for strategy {strategyType.Name}. Expected constructor signature: (List<StockData> data, IndicatorManager indicators, ...params).{errorText}");
        }

        var instance = bestCtor.Invoke(bestArgs);
        if (instance is not IStrategy strategy)
            throw new InvalidOperationException($"Created instance of {strategyType.Name} does not implement IStrategy.");

        return strategy;
    }

    private static object ConvertToTargetType(object value, Type targetType)
    {
        if (value is null)
        {
            if (targetType.IsValueType && Nullable.GetUnderlyingType(targetType) is null)
                throw new InvalidCastException($"Cannot convert null to non-nullable {targetType.Name}.");
            return null!;
        }

        var nonNullableTarget = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (nonNullableTarget.IsInstanceOfType(value))
            return value;

        if (value is JsonElement jsonElement)
            return ConvertJsonElement(jsonElement, nonNullableTarget);

        if (nonNullableTarget.IsEnum)
        {
            if (value is string enumString)
                return Enum.Parse(nonNullableTarget, enumString, true);
            // Sayısal değer (örn. optimizer range'i double üretir: 2.0) -> Enum.ToObject double kabul
            // etmez, önce integral'e çevir. int/long zaten sorunsuz geçer.
            return Enum.ToObject(nonNullableTarget, Convert.ToInt64(value, CultureInfo.InvariantCulture));
        }

        if (nonNullableTarget == typeof(Guid))
        {
            if (value is Guid g)
                return g;
            return Guid.Parse(value.ToString()!);
        }

        if (value is string s)
        {
            if (nonNullableTarget == typeof(bool))
                return bool.Parse(s);
            if (nonNullableTarget == typeof(int))
                return int.Parse(s, CultureInfo.InvariantCulture);
            if (nonNullableTarget == typeof(long))
                return long.Parse(s, CultureInfo.InvariantCulture);
            if (nonNullableTarget == typeof(float))
                return float.Parse(s, CultureInfo.InvariantCulture);
            if (nonNullableTarget == typeof(double))
                return double.Parse(s, CultureInfo.InvariantCulture);
            if (nonNullableTarget == typeof(decimal))
                return decimal.Parse(s, CultureInfo.InvariantCulture);
            if (nonNullableTarget == typeof(DateTime))
                return DateTime.Parse(s, CultureInfo.InvariantCulture);
            if (nonNullableTarget == typeof(TimeSpan))
                return TimeSpan.Parse(s, CultureInfo.InvariantCulture);
        }

        return Convert.ChangeType(value, nonNullableTarget, CultureInfo.InvariantCulture);
    }

    private static object ConvertJsonElement(JsonElement element, Type targetType)
    {
        if (targetType == typeof(string))
            return element.GetString() ?? string.Empty;
        if (targetType == typeof(int))
            return element.GetInt32();
        if (targetType == typeof(long))
            return element.GetInt64();
        if (targetType == typeof(float))
            return element.GetSingle();
        if (targetType == typeof(double))
            return element.GetDouble();
        if (targetType == typeof(decimal))
            return element.GetDecimal();
        if (targetType == typeof(bool))
            return element.GetBoolean();
        if (targetType == typeof(DateTime))
            return element.GetDateTime();
        if (targetType == typeof(Guid))
            return element.GetGuid();

        throw new InvalidCastException($"Unsupported JsonElement conversion target: {targetType.Name}");
    }
}
