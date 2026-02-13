using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AlgoTrade.Core.Trading.Strategies
{
    /// <summary>
    /// Represents a single parameter definition for a strategy
    /// </summary>
    public class ParameterInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public object DefaultValue { get; set; } = null!;

        public ParameterInfo() { }

        public ParameterInfo(string name, string type, object defaultValue)
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
    /// Represents a complete strategy configuration with version and parameters
    /// </summary>
    public class StrategyConfiguration
    {
        public string StrategyName { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public Dictionary<string, ParameterInfo> Parameters { get; set; } = new Dictionary<string, ParameterInfo>();

        public StrategyConfiguration() { }

        public StrategyConfiguration(string strategyName, string version)
        {
            StrategyName = strategyName;
            Version = version;
        }

        /// <summary>
        /// Get parameters as a dictionary suitable for strategy factory
        /// Converts string values to appropriate types
        /// </summary>
        public Dictionary<string, object> GetParameterValues()
        {
            return Parameters.ToDictionary(
                kvp => kvp.Key,
                kvp =>
                {
                    // If DefaultValue is already the correct type, use it
                    // Otherwise parse it from string
                    var value = kvp.Value.DefaultValue;
                    if (value is string strValue)
                    {
                        return ParameterInfo.ParseValue(kvp.Value.Type, strValue);
                    }
                    return value;
                }
            );
        }

        /// <summary>
        /// Get display string for DataGridView
        /// </summary>
        public string GetParametersDisplayString()
        {
            var paramStrings = Parameters.Select(kvp =>
                $"{kvp.Value.Name}={kvp.Value.DefaultValue} ({kvp.Value.Type})");
            return string.Join(", ", paramStrings);
        }
    }

    /// <summary>
    /// Loads and manages strategy configurations from StrategyConfig.txt file
    /// </summary>
    public class StrategyConfigLoader
    {
        private readonly string _configFilePath;
        private List<StrategyConfiguration> _configurations = new List<StrategyConfiguration>();

        public StrategyConfigLoader(string configFilePath)
        {
            _configFilePath = configFilePath;
        }

        /// <summary>
        /// Load all strategy configurations from file
        /// </summary>
        public void LoadFromFile()
        {
            _configurations.Clear();

            if (!File.Exists(_configFilePath))
            {
                throw new FileNotFoundException($"Strategy configuration file not found: {_configFilePath}");
            }

            var lines = File.ReadAllLines(_configFilePath);

            foreach (var line in lines)
            {
                // Skip empty lines and comments
                var trimmedLine = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmedLine) || trimmedLine.StartsWith("#"))
                {
                    continue;
                }

                try
                {
                    var config = ParseConfigurationLine(trimmedLine);
                    _configurations.Add(config);
                }
                catch (Exception ex)
                {
                    // Log error but continue processing other lines
                    Console.WriteLine($"Error parsing line '{line}': {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Parse a single configuration line
        /// Format: StrategyName|Version|param1:type:value|param2:type:value|...
        /// </summary>
        private StrategyConfiguration ParseConfigurationLine(string line)
        {
            var parts = line.Split('|');

            if (parts.Length < 2)
            {
                throw new FormatException($"Invalid configuration line format. Expected at least 2 parts (StrategyName|Version), got {parts.Length}");
            }

            var config = new StrategyConfiguration
            {
                StrategyName = parts[0].Trim(),
                Version = parts[1].Trim()
            };

            // Parse parameters (if any)
            for (int i = 2; i < parts.Length; i++)
            {
                var paramParts = parts[i].Split(':');
                if (paramParts.Length != 3)
                {
                    throw new FormatException($"Invalid parameter format: {parts[i]}. Expected 'name:type:value'");
                }

                var paramName = paramParts[0].Trim();
                var paramType = paramParts[1].Trim();
                var paramValueStr = paramParts[2].Trim();

                var paramValue = ParameterInfo.ParseValue(paramType, paramValueStr);

                config.Parameters[paramName] = new ParameterInfo(paramName, paramType, paramValue);
            }

            return config;
        }

        /// <summary>
        /// Get all loaded configurations
        /// </summary>
        public List<StrategyConfiguration> GetAllConfigurations()
        {
            return _configurations.ToList();
        }

        /// <summary>
        /// Get unique strategy names (without versions)
        /// </summary>
        public List<string> GetUniqueStrategyNames()
        {
            return _configurations
                .Select(c => c.StrategyName)
                .Distinct()
                .OrderBy(name => name)
                .ToList();
        }

        /// <summary>
        /// Get all versions for a specific strategy
        /// </summary>
        public List<string> GetVersionsForStrategy(string strategyName)
        {
            return _configurations
                .Where(c => c.StrategyName.Equals(strategyName, StringComparison.OrdinalIgnoreCase))
                .Select(c => c.Version)
                .ToList();
        }

        /// <summary>
        /// Get a specific configuration by strategy name and version
        /// </summary>
        public StrategyConfiguration? GetConfiguration(string strategyName, string version)
        {
            return _configurations.FirstOrDefault(c =>
                c.StrategyName.Equals(strategyName, StringComparison.OrdinalIgnoreCase) &&
                c.Version.Equals(version, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Get the first configuration for a strategy (useful for default selection)
        /// </summary>
        public StrategyConfiguration? GetFirstConfigurationForStrategy(string strategyName)
        {
            return _configurations.FirstOrDefault(c =>
                c.StrategyName.Equals(strategyName, StringComparison.OrdinalIgnoreCase));
        }
    }
}
