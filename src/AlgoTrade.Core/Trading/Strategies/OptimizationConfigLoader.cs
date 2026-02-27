using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AlgoTrade.Core.Trading.Strategies
{
    /// <summary>
    /// Represents a parameter range for optimization (min, max, step)
    /// </summary>
    public class OptimizationParameterRange
    {
        public string Name { get; set; } = string.Empty;
        public double Min { get; set; }
        public double Max { get; set; }
        public double Step { get; set; }

        public OptimizationParameterRange() { }

        public OptimizationParameterRange(string name, double min, double max, double step)
        {
            Name = name;
            Min = min;
            Max = max;
            Step = step;
        }
    }

    /// <summary>
    /// Represents a complete optimization configuration: strategy name, parameter ranges, and fixed parameters
    /// </summary>
    public class OptimizationConfiguration
    {
        public string StrategyName { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public List<OptimizationParameterRange> ParameterRanges { get; set; } = new();
        public Dictionary<string, ParameterInfo> FixedParameters { get; set; } = new();

        public OptimizationConfiguration() { }

        public OptimizationConfiguration(string strategyName, string version, string displayName = "")
        {
            StrategyName = strategyName;
            Version      = version;
            DisplayName  = displayName;
        }

        /// <summary>
        /// Get fixed parameters as a dictionary suitable for strategy factory
        /// </summary>
        public Dictionary<string, object> GetFixedParameterValues()
        {
            return FixedParameters.ToDictionary(
                kvp => kvp.Key,
                kvp =>
                {
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
        /// Get display string
        /// </summary>
        public string GetDisplayString()
        {
            var ranges = ParameterRanges.Select(r => $"{r.Name}:[{r.Min}-{r.Max} step {r.Step}]");
            var fixedParams = FixedParameters.Select(kvp => $"{kvp.Value.Name}={kvp.Value.DefaultValue}");
            return $"{StrategyName} ({Version}) | Ranges: {string.Join(", ", ranges)} | Fixed: {string.Join(", ", fixedParams)}";
        }
    }

    /// <summary>
    /// Loads optimization configurations from OptimizationConfig.txt file
    /// Format: StrategyName|Version|paramName:min:max:step|...|fixed:paramName:type:value|...
    /// </summary>
    public class OptimizationConfigLoader
    {
        private readonly string _configFilePath;
        private List<OptimizationConfiguration> _configurations = new();

        public OptimizationConfigLoader(string configFilePath)
        {
            _configFilePath = configFilePath;
        }

        /// <summary>
        /// Load all optimization configurations from file
        /// </summary>
        public void LoadFromFile()
        {
            _configurations.Clear();

            if (!File.Exists(_configFilePath))
            {
                throw new FileNotFoundException($"Optimization configuration file not found: {_configFilePath}");
            }

            var lines = File.ReadAllLines(_configFilePath);

            foreach (var line in lines)
            {
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
                    Console.WriteLine($"Error parsing line '{line}': {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Parse a single configuration line
        /// Format: StrategyName|Version|DisplayName|paramName:min:max:step|...|fixed:paramName:type:value|...
        /// </summary>
        private OptimizationConfiguration ParseConfigurationLine(string line)
        {
            var parts = line.Split('|');

            if (parts.Length < 4)
            {
                throw new FormatException($"Invalid optimization config line format. Expected at least 4 parts (StrategyName|Version|DisplayName|param...), got {parts.Length}");
            }

            var config = new OptimizationConfiguration
            {
                StrategyName = parts[0].Trim(),
                Version      = parts[1].Trim(),
                DisplayName  = parts[2].Trim()
            };

            for (int i = 3; i < parts.Length; i++)
            {
                var segment = parts[i].Trim();

                if (segment.StartsWith("fixed:", StringComparison.OrdinalIgnoreCase))
                {
                    // Fixed parameter: fixed:paramName:type:value
                    var fixedParts = segment.Split(':');
                    if (fixedParts.Length != 4)
                    {
                        throw new FormatException($"Invalid fixed parameter format: {segment}. Expected 'fixed:name:type:value'");
                    }

                    var paramName = fixedParts[1].Trim();
                    var paramType = fixedParts[2].Trim();
                    var paramValueStr = fixedParts[3].Trim();
                    var paramValue = ParameterInfo.ParseValue(paramType, paramValueStr);

                    config.FixedParameters[paramName] = new ParameterInfo(paramName, paramType, paramValue);
                }
                else
                {
                    // Range parameter: paramName:min:max:step
                    var rangeParts = segment.Split(':');
                    if (rangeParts.Length != 4)
                    {
                        throw new FormatException($"Invalid range parameter format: {segment}. Expected 'name:min:max:step'");
                    }

                    var name = rangeParts[0].Trim();
                    var min = double.Parse(rangeParts[1].Trim(), System.Globalization.CultureInfo.InvariantCulture);
                    var max = double.Parse(rangeParts[2].Trim(), System.Globalization.CultureInfo.InvariantCulture);
                    var step = double.Parse(rangeParts[3].Trim(), System.Globalization.CultureInfo.InvariantCulture);

                    config.ParameterRanges.Add(new OptimizationParameterRange(name, min, max, step));
                }
            }

            return config;
        }

        /// <summary>
        /// Get all loaded configurations
        /// </summary>
        public List<OptimizationConfiguration> GetAllConfigurations()
        {
            return _configurations.ToList();
        }

        /// <summary>
        /// Get a specific configuration by strategy name and version
        /// </summary>
        public OptimizationConfiguration? GetConfiguration(string strategyName, string version)
        {
            return _configurations.FirstOrDefault(c =>
                c.StrategyName.Equals(strategyName, StringComparison.OrdinalIgnoreCase) &&
                c.Version.Equals(version, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Get the first configuration for a strategy
        /// </summary>
        public OptimizationConfiguration? GetFirstConfigurationForStrategy(string strategyName)
        {
            return _configurations.FirstOrDefault(c =>
                c.StrategyName.Equals(strategyName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Get unique strategy names
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
    }
}
