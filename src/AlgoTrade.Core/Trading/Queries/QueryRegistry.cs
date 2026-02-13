using System.Globalization;
using System.Reflection;
using System.Text.Json;
using AlgoTrade.Core.Logging;
using AlgoTrade.Core.Trading.Indicators;
using AlgoTrade.Core.Trading.Query;

namespace AlgoTrade.Core.Trading.Queries;

public sealed class QueryRegistry
{
    private readonly Dictionary<string, Type> _queryTypes = new(StringComparer.OrdinalIgnoreCase);

    public QueryRegistry()
    {
        AutoRegister();
    }

    public void AutoRegister(Assembly? assembly = null)
    {
        var targetAssembly = assembly ?? typeof(BaseQuery).Assembly;

        var queryTypes = targetAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(BaseQuery).IsAssignableFrom(t));

        foreach (var queryType in queryTypes)
        {
            _queryTypes[queryType.Name] = queryType;
        }
    }

    public IReadOnlyCollection<string> GetQueryNames()
    {
        return _queryTypes.Keys.OrderBy(name => name).ToList();
    }

    public IQuery CreateQuery(
        List<StockData> data,
        IndicatorManager indicators,
        LogManager? logger,
        string? queryName = null,
        Dictionary<string, object>? parameters = null)
    {
        var resolvedQueryName = queryName;
        if (string.IsNullOrWhiteSpace(resolvedQueryName))
        {
            resolvedQueryName = "SimpleMAQuery";
            Log(logger, "Query name is null/empty. Falling back to SimpleMAQuery.");
        }

        if (!_queryTypes.TryGetValue(resolvedQueryName, out var queryType))
        {
            throw new ArgumentException(
                $"Unknown query: {resolvedQueryName}. Available queries: {string.Join(", ", GetQueryNames())}",
                nameof(queryName));
        }

        var safeParameters = parameters ?? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        IQuery createdQuery;
        if (TryCreateViaStaticFactory(queryType, data, indicators, safeParameters, out var queryFromFactory))
        {
            createdQuery = queryFromFactory;
        }
        else
        {
            createdQuery = CreateFromBestMatchingConstructor(queryType, data, indicators, safeParameters);
        }

        if (createdQuery is BaseQuery baseQuery)
        {
            baseQuery.SetLogger(logger);
        }

        return createdQuery;
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
        Type queryType,
        List<StockData> data,
        IndicatorManager indicators,
        Dictionary<string, object> parameters,
        out IQuery query)
    {
        query = null!;

        var factory = queryType
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(m =>
            {
                if (!typeof(IQuery).IsAssignableFrom(m.ReturnType))
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
        if (created is not IQuery typed)
            throw new InvalidOperationException($"Static Create method for {queryType.Name} did not return IQuery.");

        query = typed;
        return true;
    }

    private static IQuery CreateFromBestMatchingConstructor(
        Type queryType,
        List<StockData> data,
        IndicatorManager indicators,
        Dictionary<string, object> parameters)
    {
        var parameterLookup = new Dictionary<string, object>(parameters, StringComparer.OrdinalIgnoreCase);
        var constructors = queryType.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
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
                        errors.Add($"{queryType.Name}.{p.Name}: {ex.Message}");
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
                $"No compatible constructor found for query {queryType.Name}. Expected constructor signature: (List<StockData> data, IndicatorManager indicators, ...params).{errorText}");
        }

        var instance = bestCtor.Invoke(bestArgs);
        if (instance is not IQuery query)
            throw new InvalidOperationException($"Created instance of {queryType.Name} does not implement IQuery.");

        return query;
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
            return Enum.ToObject(nonNullableTarget, value);
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
