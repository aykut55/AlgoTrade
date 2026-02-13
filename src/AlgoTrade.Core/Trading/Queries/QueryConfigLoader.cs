using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AlgoTrade.Core.Trading.Queries;

/// <summary>
/// Represents a single parameter definition for a query
/// </summary>
public class QueryParameterInfo
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public object DefaultValue { get; set; } = null!;

    public QueryParameterInfo()
    {
    }

    public QueryParameterInfo(string name, string type, object defaultValue)
    {
        Name = name;
        Type = type;
        DefaultValue = defaultValue;
    }

    /// <summary>
    /// Parse parameter value from string based on type
    /// Allows flexible input: "10" or "10.0" can both be parsed as int or double
    /// </summary>
    public static object ParseValue(string type, string value)
    {
        return type.ToLower() switch
        {
            "int" => (int)double.Parse(value, System.Globalization.CultureInfo.InvariantCulture),
            "double" => double.Parse(value, System.Globalization.CultureInfo.InvariantCulture),
            "bool" => bool.Parse(value),
            "string" => value,
            _ => throw new ArgumentException($"Unsupported parameter type: {type}")
        };
    }
}

/// <summary>
/// Represents a complete query configuration with version and parameters
/// </summary>
public class QueryConfiguration
{
    public string QueryName { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public Dictionary<string, QueryParameterInfo> Parameters { get; set; } = new Dictionary<string, QueryParameterInfo>();

    /// <summary>
    /// Get parameters as a dictionary suitable for query factory
    /// Converts string values to appropriate types
    /// </summary>
    public Dictionary<string, object> GetParameterValues()
    {
        return Parameters.ToDictionary(
            kvp => kvp.Key,
            kvp =>
            {
                var value = kvp.Value.DefaultValue;
                if (value is string strValue)
                    return QueryParameterInfo.ParseValue(kvp.Value.Type, strValue);
                return value;
            });
    }

    public string GetParametersDisplayString()
    {
        var paramStrings = Parameters.Select(kvp =>
            $"{kvp.Value.Name}={kvp.Value.DefaultValue} ({kvp.Value.Type})");
        return string.Join(", ", paramStrings);
    }
}

/// <summary>
/// Loads and manages query configurations from QueryConfig.txt file
/// </summary>
public class QueryConfigLoader
{
    private readonly string _configFilePath;
    private readonly List<QueryConfiguration> _configurations = new List<QueryConfiguration>();

    public QueryConfigLoader(string configFilePath)
    {
        _configFilePath = configFilePath;
    }

    public void LoadFromFile()
    {
        _configurations.Clear();

        if (!File.Exists(_configFilePath))
            throw new FileNotFoundException($"Query configuration file not found: {_configFilePath}");

        var lines = File.ReadAllLines(_configFilePath);
        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmedLine) || trimmedLine.StartsWith("#"))
                continue;

            try
            {
                var config = ParseConfigurationLine(trimmedLine);
                _configurations.Add(config);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing line '{line}': {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Format: QueryName|Version|param1:type:value|param2:type:value|...
    /// </summary>
    private static QueryConfiguration ParseConfigurationLine(string line)
    {
        var parts = line.Split('|');
        if (parts.Length < 2)
            throw new FormatException($"Invalid configuration line format. Expected at least 2 parts (QueryName|Version), got {parts.Length}");

        var config = new QueryConfiguration
        {
            QueryName = parts[0].Trim(),
            Version = parts[1].Trim()
        };

        for (int i = 2; i < parts.Length; i++)
        {
            var paramParts = parts[i].Split(':');
            if (paramParts.Length != 3)
                throw new FormatException($"Invalid parameter format: {parts[i]}. Expected 'name:type:value'");

            var paramName = paramParts[0].Trim();
            var paramType = paramParts[1].Trim();
            var paramValueStr = paramParts[2].Trim();

            var paramValue = QueryParameterInfo.ParseValue(paramType, paramValueStr);
            config.Parameters[paramName] = new QueryParameterInfo(paramName, paramType, paramValue);
        }

        return config;
    }

    public List<QueryConfiguration> GetAllConfigurations()
    {
        return _configurations.ToList();
    }

    public List<string> GetUniqueQueryNames()
    {
        return _configurations
            .Select(c => c.QueryName)
            .Distinct()
            .OrderBy(name => name)
            .ToList();
    }

    public List<string> GetVersionsForQuery(string queryName)
    {
        return _configurations
            .Where(c => c.QueryName.Equals(queryName, StringComparison.OrdinalIgnoreCase))
            .Select(c => c.Version)
            .ToList();
    }

    public QueryConfiguration? GetConfiguration(string queryName, string version)
    {
        return _configurations.FirstOrDefault(c =>
            c.QueryName.Equals(queryName, StringComparison.OrdinalIgnoreCase) &&
            c.Version.Equals(version, StringComparison.OrdinalIgnoreCase));
    }

    public QueryConfiguration? GetFirstConfigurationForQuery(string queryName)
    {
        return _configurations.FirstOrDefault(c =>
            c.QueryName.Equals(queryName, StringComparison.OrdinalIgnoreCase));
    }
}
