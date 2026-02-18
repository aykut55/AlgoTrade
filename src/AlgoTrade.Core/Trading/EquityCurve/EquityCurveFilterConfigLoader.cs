using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AlgoTrade.Core.Trading.EquityCurve
{
    /// <summary>
    /// Represents a complete EquityCurveFilter configuration with version
    /// </summary>
    public class EquityCurveFilterConfiguration
    {
        public string Version { get; set; } = string.Empty;
        public bool Enabled { get; set; }
        public bool ThresholdTypeIsPercent { get; set; }
        public double ProfitThreshold { get; set; }
        public double LossThreshold { get; set; }
        public ConfirmationTrigger Trigger { get; set; }

        public EquityCurveFilterConfiguration() { }

        public EquityCurveFilterConfiguration(string version)
        {
            Version = version;
        }

        /// <summary>
        /// Get display string for console menu
        /// </summary>
        public string GetDisplayString()
        {
            string type = ThresholdTypeIsPercent ? "percent" : "absolute";
            string enabledStr = Enabled ? "ON" : "OFF";
            return $"enabled={enabledStr}, type={type}, profit={ProfitThreshold}, loss={LossThreshold}, trigger={Trigger}";
        }
    }

    /// <summary>
    /// Loads and manages EquityCurveFilter configurations from EquityCurveFilterConfig.txt file
    /// Format: Version|enabled:bool:value|thresholdType:string:value|profitThreshold:double:value|lossThreshold:double:value|trigger:string:value
    /// </summary>
    public class EquityCurveFilterConfigLoader
    {
        private readonly string _configFilePath;
        private List<EquityCurveFilterConfiguration> _configurations = new List<EquityCurveFilterConfiguration>();

        public EquityCurveFilterConfigLoader(string configFilePath)
        {
            _configFilePath = configFilePath;
        }

        /// <summary>
        /// Load all configurations from file
        /// </summary>
        public void LoadFromFile()
        {
            _configurations.Clear();

            if (!File.Exists(_configFilePath))
            {
                throw new FileNotFoundException($"EquityCurveFilter configuration file not found: {_configFilePath}");
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
        /// Format: Version|enabled:bool:value|thresholdType:string:value|profitThreshold:double:value|lossThreshold:double:value|trigger:string:value
        /// </summary>
        private EquityCurveFilterConfiguration ParseConfigurationLine(string line)
        {
            var parts = line.Split('|');

            if (parts.Length < 6)
            {
                throw new FormatException($"Invalid configuration line format. Expected 6 parts (Version|enabled|thresholdType|profitThreshold|lossThreshold|trigger), got {parts.Length}");
            }

            var config = new EquityCurveFilterConfiguration
            {
                Version = parts[0].Trim()
            };

            // Parse each key:type:value field
            for (int i = 1; i < parts.Length; i++)
            {
                var paramParts = parts[i].Split(':');
                if (paramParts.Length != 3)
                {
                    throw new FormatException($"Invalid parameter format: {parts[i]}. Expected 'name:type:value'");
                }

                var paramName = paramParts[0].Trim().ToLower();
                var paramType = paramParts[1].Trim().ToLower();
                var paramValue = paramParts[2].Trim();

                switch (paramName)
                {
                    case "enabled":
                        config.Enabled = bool.Parse(paramValue);
                        break;
                    case "thresholdtype":
                        config.ThresholdTypeIsPercent = paramValue.Equals("percent", StringComparison.OrdinalIgnoreCase);
                        break;
                    case "profitthreshold":
                        config.ProfitThreshold = double.Parse(paramValue, System.Globalization.CultureInfo.InvariantCulture);
                        break;
                    case "lossthreshold":
                        config.LossThreshold = double.Parse(paramValue, System.Globalization.CultureInfo.InvariantCulture);
                        break;
                    case "trigger":
                        config.Trigger = Enum.Parse<ConfirmationTrigger>(paramValue, ignoreCase: true);
                        break;
                    default:
                        throw new FormatException($"Unknown parameter: {paramName}");
                }
            }

            return config;
        }

        /// <summary>
        /// Get all loaded configurations
        /// </summary>
        public List<EquityCurveFilterConfiguration> GetAllConfigurations()
        {
            return _configurations.ToList();
        }

        /// <summary>
        /// Get all versions
        /// </summary>
        public List<string> GetVersions()
        {
            return _configurations
                .Select(c => c.Version)
                .ToList();
        }

        /// <summary>
        /// Get a specific configuration by version
        /// </summary>
        public EquityCurveFilterConfiguration? GetConfiguration(string version)
        {
            return _configurations.FirstOrDefault(c =>
                c.Version.Equals(version, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Get the first configuration (useful for default selection)
        /// </summary>
        public EquityCurveFilterConfiguration? GetFirstConfiguration()
        {
            return _configurations.FirstOrDefault();
        }
    }
}
