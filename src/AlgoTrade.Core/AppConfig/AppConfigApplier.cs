using AlgoTrade.Core.StockDataReader;
using AlgoTrade.Core.Trading;
using AlgoTrade.Core.Trading.Core;
using AlgoTrade.Core.Trading.EquityCurve;
using AlgoTrade.Core.Trading.Strategies;
using AlgoTrade.Core.Trading.Queries;
using TradingOptRangeConfig      = AlgoTrade.Core.Trading.SingleTraderOptRangeConfig;
using TradingOptTradeParamsConfig = AlgoTrade.Core.Trading.SingleTraderOptTradeParamsConfig;

namespace AlgoTrade.Core.AppConfig;

/// <summary>
/// AppConfig.json'dan yüklenen konfigürasyonu AlgoTrader'a uygular.
/// AlgoTrade.Console ve AlgoTrade.WinForms her ikisi de bu sınıfı kullanır.
/// </summary>
public static class AppConfigApplier
{
    // =========================================================================
    // Top-level Apply
    // =========================================================================

    /// <summary>
    /// AppSettings bölümünü uygular. StockDataFile'ı döner.
    /// </summary>
    public static string ApplyAppSettings(AppSettingsConfig cfg)
        => cfg.StockDataFile;

    /// <summary>
    /// SingleTrader bölümünü AlgoTrader'a uygular.
    /// configsDir: StrategyConfig.txt, QueryConfig.txt vb. dosyaların bulunduğu klasör.
    /// </summary>
    public static void ApplySingleTrader(AlgoTrader algoTrader, SingleTraderConfig cfg, string configsDir)
    {
        // RunMode (AppConfig baseline — çağıran taraf sonradan override edebilir)
        if (Enum.TryParse<TraderRunMode>(cfg.RunMode, ignoreCase: true, out var runMode))
            algoTrader.SingleTraderRunMode = runMode;

        // Strategy
        string stratPath = Path.Combine(configsDir, cfg.Strategy.ConfigFile);
        algoTrader.ConfigureStrategyFromConfig(stratPath, cfg.Strategy.Name, cfg.Strategy.Version);

        // Query (opsiyonel)
        if (cfg.Query is not null)
        {
            string queryPath = Path.Combine(configsDir, cfg.Query.ConfigFile);
            algoTrader.ConfigureQueryFromConfig(queryPath, cfg.Query.Name, cfg.Query.Version);
        }

        // EquityCurveFilter (opsiyonel)
        if (cfg.EquityCurveFilter is not null)
        {
            algoTrader.ClearEquityCurveFilterConfigs();
            string ecfPath = Path.Combine(configsDir, cfg.EquityCurveFilter.ConfigFile);
            algoTrader.ConfigureEquityCurveFilterFromConfig(ecfPath, cfg.EquityCurveFilter.Version, id: 0);
        }

        // TradeParams → SingleTrader override
        var tradeParams = BuildInitialTradeParams(cfg.TradeParams);
        algoTrader.SetSingleTraderTradeParams(tradeParams);

        // Signals (OnApplyUserFlags karşılığı)
        algoTrader.SetSingleTraderSignalsConfig(new SingleTraderSignalsConfig
        {
            AlEnabled              = cfg.Signals.AlEnabled,
            SatEnabled             = cfg.Signals.SatEnabled,
            FlatOlEnabled          = cfg.Signals.FlatOlEnabled,
            PasGecEnabled          = cfg.Signals.PasGecEnabled,
            KarAlEnabled           = cfg.Signals.KarAlEnabled,
            ZararKesEnabled        = cfg.Signals.ZararKesEnabled,
            GunSonuPozKapatEnabled = cfg.Signals.GunSonuPozKapatEnabled,
            TimeFilteringEnabled      = cfg.Signals.TimeFilteringEnabled,
            StartDateTime             = cfg.Signals.StartDateTime,
            StopDateTime              = cfg.Signals.StopDateTime,
            TradeStartBarIndexEnabled = cfg.Signals.TradeStartBarIndexEnabled,
            TradeStartBarIndex        = cfg.Signals.TradeStartBarIndex,
        });

        // Optimization
        algoTrader.SetSingleTraderOptimizationConfig(new SingleTraderOptimizationConfig
        {
            OptimizationEnabled = cfg.Optimization.OptimizationEnabled,
        });

        // Save (OnApplyUserFlags2 karşılığı)
        algoTrader.SetSingleTraderSaveConfig(new SingleTraderSaveConfig
        {
            SaveStatisticsToFile                = cfg.Save.SaveStatisticsToFile,
            SaveFullStatsTxtEnabled             = cfg.Save.SaveFullStatsTxtEnabled,
            SaveFullStatsCsvEnabled             = cfg.Save.SaveFullStatsCsvEnabled,
            SaveMinimalStatsTxtEnabled          = cfg.Save.SaveMinimalStatsTxtEnabled,
            SaveMinimalStatsCsvEnabled          = cfg.Save.SaveMinimalStatsCsvEnabled,
            SaveFullListsTxtEnabled             = cfg.Save.SaveFullListsTxtEnabled,
            SaveFullListsCsvEnabled             = cfg.Save.SaveFullListsCsvEnabled,
            SaveMinimalListsTxtEnabled          = cfg.Save.SaveMinimalListsTxtEnabled,
            SaveMinimalListsCsvEnabled          = cfg.Save.SaveMinimalListsCsvEnabled,
            SaveFullStatsTxtFormattedEnabled    = cfg.Save.SaveFullStatsTxtFormattedEnabled,
            SaveMinimalStatsTxtFormattedEnabled = cfg.Save.SaveMinimalStatsTxtFormattedEnabled,
            SavePerformansTxtEnabled            = cfg.Save.SavePerformansTxtEnabled,
            SavePerformansCsvEnabled            = cfg.Save.SavePerformansCsvEnabled,
            FullStatsTxtFileName                = cfg.Save.FullStatsTxtFileName,
            FullStatsCsvFileName                = cfg.Save.FullStatsCsvFileName,
            MinimalStatsTxtFileName             = cfg.Save.MinimalStatsTxtFileName,
            MinimalStatsCsvFileName             = cfg.Save.MinimalStatsCsvFileName,
            FullListsTxtFileName                = cfg.Save.FullListsTxtFileName,
            FullListsCsvFileName                = cfg.Save.FullListsCsvFileName,
            MinimalListsTxtFileName             = cfg.Save.MinimalListsTxtFileName,
            MinimalListsCsvFileName             = cfg.Save.MinimalListsCsvFileName,
            FullStatsTxtFormattedFileName       = cfg.Save.FullStatsTxtFormattedFileName,
            MinimalStatsTxtFormattedFileName    = cfg.Save.MinimalStatsTxtFormattedFileName,
            PerformansTxtFileName               = cfg.Save.PerformansTxtFileName,
            PerformansCsvFileName               = cfg.Save.PerformansCsvFileName,
        });

        // Plot
        algoTrader.SetSingleTraderPlotConfig(new SingleTraderPlotConfig
        {
            PlotEnabled = cfg.Plot.PlotEnabled,
        });

        // Export
        if (cfg.Export is not null)
        {
            algoTrader.SetSingleTraderExportConfig(new SingleTraderExportConfig
            {
                ExportEnabled    = cfg.Export.ExportEnabled,
                ExportConfigFile = Path.Combine(configsDir, cfg.Export.ConfigFile),
                ExportVersion    = cfg.Export.Version,
            });
        }
    }

    /// <summary>
    /// MultipleTrader bölümünü AlgoTrader'a uygular.
    /// MainTrader config SetSingle* metodlarıyla uygulanır.
    /// Her ChildTraderEntry için strategy, query, ecf, signals ve save uygulanır.
    /// TradeParams tüm child'lara MainTrader'dan aktarılır.
    /// </summary>
    public static void ApplyMultipleTrader(AlgoTrader algoTrader, MultipleTraderConfig cfg, string configsDir)
    {
        var children = cfg.ChildTraders;
        if (children.Count == 0)
            throw new InvalidOperationException("MultipleTrader.ChildTraders boş — en az 1 child tanımlanmalı.");

        // RunMode
        if (Enum.TryParse<TraderRunMode>(cfg.RunMode, ignoreCase: true, out var runMode))
            algoTrader.SingleTraderRunMode = runMode;

        // MultipleTrader nesnesi kayıt ayarları
        algoTrader.SetMultipleTraderSaveConfig(new MultipleTraderObjectSaveConfig
        {
            SaveStatisticsToFile              = cfg.Save.SaveStatisticsToFile,
            SaveMultipleTraderListsTxtEnabled = cfg.Save.SaveMultipleTraderListsTxtEnabled,
            SaveMultipleTraderListsCsvEnabled = cfg.Save.SaveMultipleTraderListsCsvEnabled,
            MultipleTraderListsTxtFileName    = cfg.Save.MultipleTraderListsTxtFileName,
            MultipleTraderListsCsvFileName    = cfg.Save.MultipleTraderListsCsvFileName,
            WriteChildTradersDataToFiles      = cfg.Save.WriteChildTradersDataToFiles,
        });

        // Consensus ayarları (Mode: Net|Majority|All|Any, MinNetCount)
        algoTrader.SetMultipleTraderConsensusConfig(new MultipleTraderConsensusConfig
        {
            Mode        = cfg.Consensus.Mode,
            MinNetCount = cfg.Consensus.MinNetCount,
        });

        // =====================================================================
        // MainTrader config → algoTrader (SetSingle* metodları ile)
        // =====================================================================

        // TradeParams
        var mainTradeParams = BuildInitialTradeParams(cfg.MainTrader.TradeParams);
        algoTrader.SetSingleTraderTradeParams(mainTradeParams);

        // Signals
        algoTrader.SetSingleTraderSignalsConfig(new SingleTraderSignalsConfig
        {
            AlEnabled              = cfg.MainTrader.Signals.AlEnabled,
            SatEnabled             = cfg.MainTrader.Signals.SatEnabled,
            FlatOlEnabled          = cfg.MainTrader.Signals.FlatOlEnabled,
            PasGecEnabled          = cfg.MainTrader.Signals.PasGecEnabled,
            KarAlEnabled           = cfg.MainTrader.Signals.KarAlEnabled,
            ZararKesEnabled        = cfg.MainTrader.Signals.ZararKesEnabled,
            GunSonuPozKapatEnabled = cfg.MainTrader.Signals.GunSonuPozKapatEnabled,
            TimeFilteringEnabled      = cfg.MainTrader.Signals.TimeFilteringEnabled,
            StartDateTime             = cfg.MainTrader.Signals.StartDateTime,
            StopDateTime              = cfg.MainTrader.Signals.StopDateTime,
            TradeStartBarIndexEnabled = cfg.MainTrader.Signals.TradeStartBarIndexEnabled,
            TradeStartBarIndex        = cfg.MainTrader.Signals.TradeStartBarIndex,
        });

        // Optimization
        algoTrader.SetSingleTraderOptimizationConfig(new SingleTraderOptimizationConfig
        {
            OptimizationEnabled = cfg.MainTrader.Optimization.OptimizationEnabled,
        });

        // Plot
        algoTrader.SetSingleTraderPlotConfig(new SingleTraderPlotConfig
        {
            PlotEnabled = cfg.MainTrader.Plot.PlotEnabled,
        });

        // Export — MainTrader
        if (cfg.MainTrader.Export is not null)
        {
            algoTrader.SetSingleTraderExportConfig(new SingleTraderExportConfig
            {
                ExportEnabled    = cfg.MainTrader.Export.ExportEnabled,
                ExportConfigFile = Path.Combine(configsDir, cfg.MainTrader.Export.ConfigFile),
                ExportVersion    = cfg.MainTrader.Export.Version,
            });
        }

        // FilePrefix — MainTrader: {prefix}_Main_{file}, Child: {prefix}_Child{i}_{file}
        string filePrefix = cfg.Save.FilePrefix;

        // Save
        var ms = cfg.MainTrader.Save;
        algoTrader.SetSingleTraderSaveConfig(new SingleTraderSaveConfig
        {
            SaveStatisticsToFile                = ms.SaveStatisticsToFile,
            SaveFullStatsTxtEnabled             = ms.SaveFullStatsTxtEnabled,
            SaveFullStatsCsvEnabled             = ms.SaveFullStatsCsvEnabled,
            SaveMinimalStatsTxtEnabled          = ms.SaveMinimalStatsTxtEnabled,
            SaveMinimalStatsCsvEnabled          = ms.SaveMinimalStatsCsvEnabled,
            SaveFullListsTxtEnabled             = ms.SaveFullListsTxtEnabled,
            SaveFullListsCsvEnabled             = ms.SaveFullListsCsvEnabled,
            SaveMinimalListsTxtEnabled          = ms.SaveMinimalListsTxtEnabled,
            SaveMinimalListsCsvEnabled          = ms.SaveMinimalListsCsvEnabled,
            SaveFullStatsTxtFormattedEnabled    = ms.SaveFullStatsTxtFormattedEnabled,
            SaveMinimalStatsTxtFormattedEnabled = ms.SaveMinimalStatsTxtFormattedEnabled,
            SaveGridStatsTxtEnabled             = ms.SaveGridStatsTxtEnabled,
            SaveMinimalGridStatsTxtEnabled      = ms.SaveMinimalGridStatsTxtEnabled,
            SavePerformansTxtEnabled            = ms.SavePerformansTxtEnabled,
            SavePerformansCsvEnabled            = ms.SavePerformansCsvEnabled,
            FullStatsTxtFileName                = $"{filePrefix}_Main_{ms.FullStatsTxtFileName}",
            FullStatsCsvFileName                = $"{filePrefix}_Main_{ms.FullStatsCsvFileName}",
            MinimalStatsTxtFileName             = $"{filePrefix}_Main_{ms.MinimalStatsTxtFileName}",
            MinimalStatsCsvFileName             = $"{filePrefix}_Main_{ms.MinimalStatsCsvFileName}",
            FullListsTxtFileName                = $"{filePrefix}_Main_{ms.FullListsTxtFileName}",
            FullListsCsvFileName                = $"{filePrefix}_Main_{ms.FullListsCsvFileName}",
            MinimalListsTxtFileName             = $"{filePrefix}_Main_{ms.MinimalListsTxtFileName}",
            MinimalListsCsvFileName             = $"{filePrefix}_Main_{ms.MinimalListsCsvFileName}",
            FullStatsTxtFormattedFileName       = $"{filePrefix}_Main_{ms.FullStatsTxtFormattedFileName}",
            MinimalStatsTxtFormattedFileName    = $"{filePrefix}_Main_{ms.MinimalStatsTxtFormattedFileName}",
            GridStatsTxtFileName                = $"{filePrefix}_Main_{ms.GridStatsTxtFileName}",
            MinimalGridStatsTxtFileName         = $"{filePrefix}_Main_{ms.MinimalGridStatsTxtFileName}",
            PerformansTxtFileName               = $"{filePrefix}_Main_{ms.PerformansTxtFileName}",
            PerformansCsvFileName               = $"{filePrefix}_Main_{ms.PerformansCsvFileName}",
        });

        // =====================================================================
        // Stratejileri yükle (benzersiz Name+Version → tek _strategyConfigs girişi)
        // =====================================================================
        algoTrader.ClearStrategyConfigs();
        var strategyIndexMap = new Dictionary<(string name, string version), int>(StringComparer.OrdinalIgnoreCase as IEqualityComparer<(string, string)>);
        int nextStratId = 0;

        foreach (var child in children)
        {
            var key = (child.Strategy.Name, child.Strategy.Version);
            if (!strategyIndexMap.ContainsKey(key))
            {
                string stratPath = Path.Combine(configsDir, child.Strategy.ConfigFile);
                var loader = new StrategyConfigLoader(stratPath);
                loader.LoadFromFile();
                var stratCfg = loader.GetConfiguration(child.Strategy.Name, child.Strategy.Version)
                    ?? throw new InvalidOperationException($"Strategy bulunamadı: {child.Strategy.Name} / {child.Strategy.Version}");
                algoTrader.AddStrategyConfig(nextStratId, stratCfg.StrategyName, stratCfg.GetParameterValues());
                strategyIndexMap[key] = nextStratId;
                nextStratId++;
            }
        }

        // =====================================================================
        // QueryConfigs yükle
        // =====================================================================
        algoTrader.ClearQueryConfigs();
        var queryIndexMap = new Dictionary<(string name, string version), int>(StringComparer.OrdinalIgnoreCase as IEqualityComparer<(string, string)>);
        int nextQueryId = 0;

        foreach (var child in children)
        {
            if (child.Query is null) continue;
            var key = (child.Query.Name, child.Query.Version);
            if (!queryIndexMap.ContainsKey(key))
            {
                string queryPath = Path.Combine(configsDir, child.Query.ConfigFile);
                var loader = new QueryConfigLoader(queryPath);
                loader.LoadFromFile();
                var qCfg = loader.GetConfiguration(child.Query.Name, child.Query.Version)
                    ?? throw new InvalidOperationException($"Query bulunamadı: {child.Query.Name} / {child.Query.Version}");
                algoTrader.AddQueryConfig(nextQueryId, qCfg.QueryName, qCfg.GetParameterValues());
                queryIndexMap[key] = nextQueryId;
                nextQueryId++;
            }
        }

        // =====================================================================
        // ECF configs yükle
        // MainTrader ECF → id=0 (SetSingleTraderConfigureEquityCurveFilter default)
        // Child ECFs     → id=1,2,...
        // =====================================================================
        algoTrader.ClearEquityCurveFilterConfigs();
        int nextEcfId = 0;

        // MainTrader ECF (id=0)
        if (cfg.MainTrader.EquityCurveFilter is not null)
        {
            string ecfPath = Path.Combine(configsDir, cfg.MainTrader.EquityCurveFilter.ConfigFile);
            algoTrader.ConfigureEquityCurveFilterFromConfig(ecfPath, cfg.MainTrader.EquityCurveFilter.Version, id: 0);
            nextEcfId = 1;
        }

        // Child ECFs
        var ecfIndexMap = new Dictionary<(string configFile, string version), int>(StringComparer.OrdinalIgnoreCase as IEqualityComparer<(string, string)>);

        foreach (var child in children)
        {
            if (child.EquityCurveFilter is null) continue;
            var key = (child.EquityCurveFilter.ConfigFile, child.EquityCurveFilter.Version);
            if (!ecfIndexMap.ContainsKey(key))
            {
                string ecfPath = Path.Combine(configsDir, child.EquityCurveFilter.ConfigFile);
                algoTrader.ConfigureEquityCurveFilterFromConfig(ecfPath, child.EquityCurveFilter.Version, id: nextEcfId);
                ecfIndexMap[key] = nextEcfId;
                nextEcfId++;
            }
        }

        // =====================================================================
        // ChildTraderConfigs oluştur
        // =====================================================================
        algoTrader.SetChildTraderCount(children.Count, (entry, i) =>
        {
            var child = children[i];

            // StrategyId
            var stratKey = (child.Strategy.Name, child.Strategy.Version);
            entry.StrategyId = strategyIndexMap[stratKey];

            // QueryId
            if (child.Query is not null)
            {
                var qKey = (child.Query.Name, child.Query.Version);
                entry.QueryId = queryIndexMap[qKey];
            }

            // EcfConfigId
            if (child.EquityCurveFilter is not null)
            {
                var ecfKey = (child.EquityCurveFilter.ConfigFile, child.EquityCurveFilter.Version);
                entry.EcfConfigId = ecfIndexMap[ecfKey];
            }

            // TradeParams — MainTrader'dan (tüm child'lar aynı parametreleri kullanır)
            entry.TradeParams.ApplyFrom(mainTradeParams);

            // Signals — per-child
            var cs = child.Signals;
            entry.Signals = new SingleTraderSignalsConfig
            {
                AlEnabled              = cs.AlEnabled,
                SatEnabled             = cs.SatEnabled,
                FlatOlEnabled          = cs.FlatOlEnabled,
                PasGecEnabled          = cs.PasGecEnabled,
                KarAlEnabled           = cs.KarAlEnabled,
                ZararKesEnabled        = cs.ZararKesEnabled,
                GunSonuPozKapatEnabled = cs.GunSonuPozKapatEnabled,
                TimeFilteringEnabled      = cs.TimeFilteringEnabled,
                StartDateTime             = cs.StartDateTime,
                StopDateTime              = cs.StopDateTime,
                TradeStartBarIndexEnabled = cs.TradeStartBarIndexEnabled,
                TradeStartBarIndex        = cs.TradeStartBarIndex,
            };

            // Save — per-child, dosya adlarına {filePrefix}_Child{i}_ ön eki eklenir
            var sv = child.Save;
            string cp = $"{filePrefix}_Child{i}";
            entry.Save = new SingleTraderSaveConfig
            {
                SaveStatisticsToFile                = sv.SaveStatisticsToFile,
                SaveFullStatsTxtEnabled             = sv.SaveFullStatsTxtEnabled,
                SaveFullStatsCsvEnabled             = sv.SaveFullStatsCsvEnabled,
                SaveMinimalStatsTxtEnabled          = sv.SaveMinimalStatsTxtEnabled,
                SaveMinimalStatsCsvEnabled          = sv.SaveMinimalStatsCsvEnabled,
                SaveFullListsTxtEnabled             = sv.SaveFullListsTxtEnabled,
                SaveFullListsCsvEnabled             = sv.SaveFullListsCsvEnabled,
                SaveMinimalListsTxtEnabled          = sv.SaveMinimalListsTxtEnabled,
                SaveMinimalListsCsvEnabled          = sv.SaveMinimalListsCsvEnabled,
                SaveFullStatsTxtFormattedEnabled    = sv.SaveFullStatsTxtFormattedEnabled,
                SaveMinimalStatsTxtFormattedEnabled = sv.SaveMinimalStatsTxtFormattedEnabled,
                SaveGridStatsTxtEnabled             = sv.SaveGridStatsTxtEnabled,
                SaveMinimalGridStatsTxtEnabled      = sv.SaveMinimalGridStatsTxtEnabled,
                SavePerformansTxtEnabled            = sv.SavePerformansTxtEnabled,
                SavePerformansCsvEnabled            = sv.SavePerformansCsvEnabled,
                FullStatsTxtFileName                = $"{cp}_{sv.FullStatsTxtFileName}",
                FullStatsCsvFileName                = $"{cp}_{sv.FullStatsCsvFileName}",
                MinimalStatsTxtFileName             = $"{cp}_{sv.MinimalStatsTxtFileName}",
                MinimalStatsCsvFileName             = $"{cp}_{sv.MinimalStatsCsvFileName}",
                FullListsTxtFileName                = $"{cp}_{sv.FullListsTxtFileName}",
                FullListsCsvFileName                = $"{cp}_{sv.FullListsCsvFileName}",
                MinimalListsTxtFileName             = $"{cp}_{sv.MinimalListsTxtFileName}",
                MinimalListsCsvFileName             = $"{cp}_{sv.MinimalListsCsvFileName}",
                FullStatsTxtFormattedFileName       = $"{cp}_{sv.FullStatsTxtFormattedFileName}",
                MinimalStatsTxtFormattedFileName    = $"{cp}_{sv.MinimalStatsTxtFormattedFileName}",
                GridStatsTxtFileName                = $"{cp}_{sv.GridStatsTxtFileName}",
                MinimalGridStatsTxtFileName         = $"{cp}_{sv.MinimalGridStatsTxtFileName}",
                PerformansTxtFileName               = $"{cp}_{sv.PerformansTxtFileName}",
                PerformansCsvFileName               = $"{cp}_{sv.PerformansCsvFileName}",
            };

            // Export — per-child
            if (child.Export is not null)
            {
                entry.Export = new SingleTraderExportConfig
                {
                    ExportEnabled    = child.Export.ExportEnabled,
                    ExportConfigFile = Path.Combine(configsDir, child.Export.ConfigFile),
                    ExportVersion    = child.Export.Version,
                };
            }
        });
    }

    /// <summary>
    /// ConfirmingSingleTrader bölümünü AlgoTrader'a uygular.
    /// SignalTrader (ham sinyal üreten strateji) kendi ayrı Signals/Save/Plot/Export slotlarını
    /// kullanır (SetConfirmingSignalTrader*). MainTrader ise SingleTrader/MultipleTrader'ın
    /// mainTrader'ıyla aynı paylaşılan _singleTrader*Config slotlarını reuse eder — MultipleTrader
    /// ile aynı desen (bkz. ApplyMultipleTrader).
    /// </summary>
    public static void ApplyConfirmingSingleTrader(AlgoTrader algoTrader, ConfirmingSingleTraderConfig cfg, string configsDir)
    {
        // RunMode (şimdilik sadece TradeOnly desteklenir)
        if (Enum.TryParse<TraderRunMode>(cfg.RunMode, ignoreCase: true, out var runMode))
            algoTrader.SingleTraderRunMode = runMode;

        // ConfirmingSingleTrader nesnesi kayıt ayarları (composite bar-by-bar lists)
        algoTrader.SetConfirmingSingleTraderSaveConfig(new ConfirmingSingleTraderObjectSaveConfig
        {
            SaveStatisticsToFile                      = cfg.Save.SaveStatisticsToFile,
            SaveConfirmingSingleTraderListsTxtEnabled = cfg.Save.SaveConfirmingSingleTraderListsTxtEnabled,
            SaveConfirmingSingleTraderListsCsvEnabled = cfg.Save.SaveConfirmingSingleTraderListsCsvEnabled,
            ConfirmingSingleTraderListsTxtFileName    = cfg.Save.ConfirmingSingleTraderListsTxtFileName,
            ConfirmingSingleTraderListsCsvFileName    = cfg.Save.ConfirmingSingleTraderListsCsvFileName,
        });

        // Sanal pozisyon konfirmasyon ayarları
        algoTrader.SetConfirmingSingleTraderConfirmationConfig(new ConfirmingSingleTraderConfirmationConfig
        {
            ThresholdIsPercentage         = cfg.Confirmation.ThresholdIsPercentage,
            ProfitThreshold               = cfg.Confirmation.ProfitThreshold,
            LossThreshold                 = cfg.Confirmation.LossThreshold,
            Trigger                       = cfg.Confirmation.Trigger,
            ConflictMode                  = cfg.Confirmation.ConflictMode,
            FlattenImmediatelyOnFlatSignal = cfg.Confirmation.FlattenImmediatelyOnFlatSignal,
        });

        string filePrefix = cfg.Save.FilePrefix;

        // =====================================================================
        // SignalTrader — Strategy + kendi ayrı Signals/Save/Plot/Export slotları
        // =====================================================================

        string stratPath = Path.Combine(configsDir, cfg.SignalTrader.Strategy.ConfigFile);
        algoTrader.ConfigureStrategyFromConfig(stratPath, cfg.SignalTrader.Strategy.Name, cfg.SignalTrader.Strategy.Version);

        algoTrader.SetConfirmingSignalTraderSignalsConfig(new SingleTraderSignalsConfig
        {
            AlEnabled              = cfg.SignalTrader.Signals.AlEnabled,
            SatEnabled             = cfg.SignalTrader.Signals.SatEnabled,
            FlatOlEnabled          = cfg.SignalTrader.Signals.FlatOlEnabled,
            PasGecEnabled          = cfg.SignalTrader.Signals.PasGecEnabled,
            KarAlEnabled           = cfg.SignalTrader.Signals.KarAlEnabled,
            ZararKesEnabled        = cfg.SignalTrader.Signals.ZararKesEnabled,
            GunSonuPozKapatEnabled = cfg.SignalTrader.Signals.GunSonuPozKapatEnabled,
            TimeFilteringEnabled      = cfg.SignalTrader.Signals.TimeFilteringEnabled,
            StartDateTime             = cfg.SignalTrader.Signals.StartDateTime,
            StopDateTime              = cfg.SignalTrader.Signals.StopDateTime,
            TradeStartBarIndexEnabled = cfg.SignalTrader.Signals.TradeStartBarIndexEnabled,
            TradeStartBarIndex        = cfg.SignalTrader.Signals.TradeStartBarIndex,
        });

        var ssv = cfg.SignalTrader.Save;
        algoTrader.SetConfirmingSignalTraderSaveConfig(new SingleTraderSaveConfig
        {
            SaveStatisticsToFile                = ssv.SaveStatisticsToFile,
            SaveFullStatsTxtEnabled             = ssv.SaveFullStatsTxtEnabled,
            SaveFullStatsCsvEnabled             = ssv.SaveFullStatsCsvEnabled,
            SaveMinimalStatsTxtEnabled          = ssv.SaveMinimalStatsTxtEnabled,
            SaveMinimalStatsCsvEnabled          = ssv.SaveMinimalStatsCsvEnabled,
            SaveFullListsTxtEnabled             = ssv.SaveFullListsTxtEnabled,
            SaveFullListsCsvEnabled             = ssv.SaveFullListsCsvEnabled,
            SaveMinimalListsTxtEnabled          = ssv.SaveMinimalListsTxtEnabled,
            SaveMinimalListsCsvEnabled          = ssv.SaveMinimalListsCsvEnabled,
            SaveFullStatsTxtFormattedEnabled    = ssv.SaveFullStatsTxtFormattedEnabled,
            SaveMinimalStatsTxtFormattedEnabled = ssv.SaveMinimalStatsTxtFormattedEnabled,
            SaveGridStatsTxtEnabled             = ssv.SaveGridStatsTxtEnabled,
            SaveMinimalGridStatsTxtEnabled      = ssv.SaveMinimalGridStatsTxtEnabled,
            SavePerformansTxtEnabled            = ssv.SavePerformansTxtEnabled,
            SavePerformansCsvEnabled            = ssv.SavePerformansCsvEnabled,
            FullStatsTxtFileName                = $"{filePrefix}_Signal_{ssv.FullStatsTxtFileName}",
            FullStatsCsvFileName                = $"{filePrefix}_Signal_{ssv.FullStatsCsvFileName}",
            MinimalStatsTxtFileName             = $"{filePrefix}_Signal_{ssv.MinimalStatsTxtFileName}",
            MinimalStatsCsvFileName             = $"{filePrefix}_Signal_{ssv.MinimalStatsCsvFileName}",
            FullListsTxtFileName                = $"{filePrefix}_Signal_{ssv.FullListsTxtFileName}",
            FullListsCsvFileName                = $"{filePrefix}_Signal_{ssv.FullListsCsvFileName}",
            MinimalListsTxtFileName             = $"{filePrefix}_Signal_{ssv.MinimalListsTxtFileName}",
            MinimalListsCsvFileName             = $"{filePrefix}_Signal_{ssv.MinimalListsCsvFileName}",
            FullStatsTxtFormattedFileName       = $"{filePrefix}_Signal_{ssv.FullStatsTxtFormattedFileName}",
            MinimalStatsTxtFormattedFileName    = $"{filePrefix}_Signal_{ssv.MinimalStatsTxtFormattedFileName}",
            GridStatsTxtFileName                = $"{filePrefix}_Signal_{ssv.GridStatsTxtFileName}",
            MinimalGridStatsTxtFileName         = $"{filePrefix}_Signal_{ssv.MinimalGridStatsTxtFileName}",
            PerformansTxtFileName               = $"{filePrefix}_Signal_{ssv.PerformansTxtFileName}",
            PerformansCsvFileName               = $"{filePrefix}_Signal_{ssv.PerformansCsvFileName}",
        });

        algoTrader.SetConfirmingSignalTraderPlotConfig(new SingleTraderPlotConfig
        {
            PlotEnabled = cfg.SignalTrader.Plot.PlotEnabled,
        });

        if (cfg.SignalTrader.Export is not null)
        {
            algoTrader.SetConfirmingSignalTraderExportConfig(new SingleTraderExportConfig
            {
                ExportEnabled    = cfg.SignalTrader.Export.ExportEnabled,
                ExportConfigFile = Path.Combine(configsDir, cfg.SignalTrader.Export.ConfigFile),
                ExportVersion    = cfg.SignalTrader.Export.Version,
            });
        }

        // =====================================================================
        // MainTrader — paylaşılan _singleTrader*Config slotları (SingleTrader/MultipleTrader ile aynı)
        // =====================================================================

        // TradeParams — signalTrader ve mainTrader aynı parametreleri kullanır
        var tradeParams = BuildInitialTradeParams(cfg.MainTrader.TradeParams);
        algoTrader.SetSingleTraderTradeParams(tradeParams);

        algoTrader.SetSingleTraderSignalsConfig(new SingleTraderSignalsConfig
        {
            AlEnabled              = cfg.MainTrader.Signals.AlEnabled,
            SatEnabled             = cfg.MainTrader.Signals.SatEnabled,
            FlatOlEnabled          = cfg.MainTrader.Signals.FlatOlEnabled,
            PasGecEnabled          = cfg.MainTrader.Signals.PasGecEnabled,
            KarAlEnabled           = cfg.MainTrader.Signals.KarAlEnabled,
            ZararKesEnabled        = cfg.MainTrader.Signals.ZararKesEnabled,
            GunSonuPozKapatEnabled = cfg.MainTrader.Signals.GunSonuPozKapatEnabled,
            TimeFilteringEnabled      = cfg.MainTrader.Signals.TimeFilteringEnabled,
            StartDateTime             = cfg.MainTrader.Signals.StartDateTime,
            StopDateTime              = cfg.MainTrader.Signals.StopDateTime,
            TradeStartBarIndexEnabled = cfg.MainTrader.Signals.TradeStartBarIndexEnabled,
            TradeStartBarIndex        = cfg.MainTrader.Signals.TradeStartBarIndex,
        });

        var ms = cfg.MainTrader.Save;
        algoTrader.SetSingleTraderSaveConfig(new SingleTraderSaveConfig
        {
            SaveStatisticsToFile                = ms.SaveStatisticsToFile,
            SaveFullStatsTxtEnabled             = ms.SaveFullStatsTxtEnabled,
            SaveFullStatsCsvEnabled             = ms.SaveFullStatsCsvEnabled,
            SaveMinimalStatsTxtEnabled          = ms.SaveMinimalStatsTxtEnabled,
            SaveMinimalStatsCsvEnabled          = ms.SaveMinimalStatsCsvEnabled,
            SaveFullListsTxtEnabled             = ms.SaveFullListsTxtEnabled,
            SaveFullListsCsvEnabled             = ms.SaveFullListsCsvEnabled,
            SaveMinimalListsTxtEnabled          = ms.SaveMinimalListsTxtEnabled,
            SaveMinimalListsCsvEnabled          = ms.SaveMinimalListsCsvEnabled,
            SaveFullStatsTxtFormattedEnabled    = ms.SaveFullStatsTxtFormattedEnabled,
            SaveMinimalStatsTxtFormattedEnabled = ms.SaveMinimalStatsTxtFormattedEnabled,
            SaveGridStatsTxtEnabled             = ms.SaveGridStatsTxtEnabled,
            SaveMinimalGridStatsTxtEnabled      = ms.SaveMinimalGridStatsTxtEnabled,
            SavePerformansTxtEnabled            = ms.SavePerformansTxtEnabled,
            SavePerformansCsvEnabled            = ms.SavePerformansCsvEnabled,
            FullStatsTxtFileName                = $"{filePrefix}_Main_{ms.FullStatsTxtFileName}",
            FullStatsCsvFileName                = $"{filePrefix}_Main_{ms.FullStatsCsvFileName}",
            MinimalStatsTxtFileName             = $"{filePrefix}_Main_{ms.MinimalStatsTxtFileName}",
            MinimalStatsCsvFileName             = $"{filePrefix}_Main_{ms.MinimalStatsCsvFileName}",
            FullListsTxtFileName                = $"{filePrefix}_Main_{ms.FullListsTxtFileName}",
            FullListsCsvFileName                = $"{filePrefix}_Main_{ms.FullListsCsvFileName}",
            MinimalListsTxtFileName             = $"{filePrefix}_Main_{ms.MinimalListsTxtFileName}",
            MinimalListsCsvFileName             = $"{filePrefix}_Main_{ms.MinimalListsCsvFileName}",
            FullStatsTxtFormattedFileName       = $"{filePrefix}_Main_{ms.FullStatsTxtFormattedFileName}",
            MinimalStatsTxtFormattedFileName    = $"{filePrefix}_Main_{ms.MinimalStatsTxtFormattedFileName}",
            GridStatsTxtFileName                = $"{filePrefix}_Main_{ms.GridStatsTxtFileName}",
            MinimalGridStatsTxtFileName         = $"{filePrefix}_Main_{ms.MinimalGridStatsTxtFileName}",
            PerformansTxtFileName               = $"{filePrefix}_Main_{ms.PerformansTxtFileName}",
            PerformansCsvFileName               = $"{filePrefix}_Main_{ms.PerformansCsvFileName}",
        });

        algoTrader.SetSingleTraderPlotConfig(new SingleTraderPlotConfig
        {
            PlotEnabled = cfg.MainTrader.Plot.PlotEnabled,
        });

        if (cfg.MainTrader.Export is not null)
        {
            algoTrader.SetSingleTraderExportConfig(new SingleTraderExportConfig
            {
                ExportEnabled    = cfg.MainTrader.Export.ExportEnabled,
                ExportConfigFile = Path.Combine(configsDir, cfg.MainTrader.Export.ConfigFile),
                ExportVersion    = cfg.MainTrader.Export.Version,
            });
        }

        // MainTrader ECF (opsiyonel, id=0 — SetSingleTraderConfigureEquityCurveFilter default)
        algoTrader.ClearEquityCurveFilterConfigs();
        if (cfg.MainTrader.EquityCurveFilter is not null)
        {
            string ecfPath = Path.Combine(configsDir, cfg.MainTrader.EquityCurveFilter.ConfigFile);
            algoTrader.ConfigureEquityCurveFilterFromConfig(ecfPath, cfg.MainTrader.EquityCurveFilter.Version, id: 0);
        }
    }

    /// <summary>
    /// ConfirmingMultipleTrader bölümünü AlgoTrader'a uygular.
    /// Consensus/ChildTraders yükleme mantığı ApplyMultipleTrader ile birebir aynı desen
    /// (paylaşılan _strategyConfigs/_childTraderConfigs slotları) — MainTrader ise
    /// ApplyConfirmingSingleTrader'daki gibi paylaşılan _singleTrader*Config slotlarını kullanır.
    /// </summary>
    public static void ApplyConfirmingMultipleTrader(AlgoTrader algoTrader, ConfirmingMultipleTraderConfig cfg, string configsDir)
    {
        var children = cfg.ChildTraders;
        if (children.Count == 0)
            throw new InvalidOperationException("ConfirmingMultipleTrader.ChildTraders boş — en az 1 child tanımlanmalı.");

        // RunMode (şimdilik sadece TradeOnly desteklenir)
        if (Enum.TryParse<TraderRunMode>(cfg.RunMode, ignoreCase: true, out var runMode))
            algoTrader.SingleTraderRunMode = runMode;

        // ConfirmingMultipleTrader nesnesi kayıt ayarları
        algoTrader.SetConfirmingMultipleTraderSaveConfig(new ConfirmingMultipleTraderObjectSaveConfig
        {
            SaveStatisticsToFile                        = cfg.Save.SaveStatisticsToFile,
            SaveConfirmingMultipleTraderListsTxtEnabled = cfg.Save.SaveConfirmingMultipleTraderListsTxtEnabled,
            SaveConfirmingMultipleTraderListsCsvEnabled = cfg.Save.SaveConfirmingMultipleTraderListsCsvEnabled,
            ConfirmingMultipleTraderListsTxtFileName    = cfg.Save.ConfirmingMultipleTraderListsTxtFileName,
            ConfirmingMultipleTraderListsCsvFileName    = cfg.Save.ConfirmingMultipleTraderListsCsvFileName,
            FilePrefix                                   = cfg.Save.FilePrefix,
            WriteSignalMultipleTraderListsToFiles       = cfg.Save.WriteSignalMultipleTraderListsToFiles,
            WriteSignalChildTradersDataToFiles          = cfg.Save.WriteSignalChildTradersDataToFiles,
        });

        // Consensus ayarları — MultipleTrader ile paylaşılan slot
        algoTrader.SetMultipleTraderConsensusConfig(new MultipleTraderConsensusConfig
        {
            Mode        = cfg.Consensus.Mode,
            MinNetCount = cfg.Consensus.MinNetCount,
        });

        // Sanal pozisyon konfirmasyon ayarları
        algoTrader.SetConfirmingMultipleTraderConfirmationConfig(new ConfirmingMultipleTraderConfirmationConfig
        {
            ThresholdIsPercentage         = cfg.Confirmation.ThresholdIsPercentage,
            ProfitThreshold               = cfg.Confirmation.ProfitThreshold,
            LossThreshold                 = cfg.Confirmation.LossThreshold,
            Trigger                       = cfg.Confirmation.Trigger,
            ConflictMode                  = cfg.Confirmation.ConflictMode,
            FlattenImmediatelyOnFlatSignal = cfg.Confirmation.FlattenImmediatelyOnFlatSignal,
        });

        // =====================================================================
        // MainTrader — paylaşılan _singleTrader*Config slotları (ApplyConfirmingSingleTrader ile aynı)
        // =====================================================================

        var mainTradeParams = BuildInitialTradeParams(cfg.MainTrader.TradeParams);
        algoTrader.SetSingleTraderTradeParams(mainTradeParams);

        algoTrader.SetSingleTraderSignalsConfig(new SingleTraderSignalsConfig
        {
            AlEnabled              = cfg.MainTrader.Signals.AlEnabled,
            SatEnabled             = cfg.MainTrader.Signals.SatEnabled,
            FlatOlEnabled          = cfg.MainTrader.Signals.FlatOlEnabled,
            PasGecEnabled          = cfg.MainTrader.Signals.PasGecEnabled,
            KarAlEnabled           = cfg.MainTrader.Signals.KarAlEnabled,
            ZararKesEnabled        = cfg.MainTrader.Signals.ZararKesEnabled,
            GunSonuPozKapatEnabled = cfg.MainTrader.Signals.GunSonuPozKapatEnabled,
            TimeFilteringEnabled      = cfg.MainTrader.Signals.TimeFilteringEnabled,
            StartDateTime             = cfg.MainTrader.Signals.StartDateTime,
            StopDateTime              = cfg.MainTrader.Signals.StopDateTime,
            TradeStartBarIndexEnabled = cfg.MainTrader.Signals.TradeStartBarIndexEnabled,
            TradeStartBarIndex        = cfg.MainTrader.Signals.TradeStartBarIndex,
        });

        string filePrefix = cfg.Save.FilePrefix;

        var ms = cfg.MainTrader.Save;
        algoTrader.SetSingleTraderSaveConfig(new SingleTraderSaveConfig
        {
            SaveStatisticsToFile                = ms.SaveStatisticsToFile,
            SaveFullStatsTxtEnabled             = ms.SaveFullStatsTxtEnabled,
            SaveFullStatsCsvEnabled             = ms.SaveFullStatsCsvEnabled,
            SaveMinimalStatsTxtEnabled          = ms.SaveMinimalStatsTxtEnabled,
            SaveMinimalStatsCsvEnabled          = ms.SaveMinimalStatsCsvEnabled,
            SaveFullListsTxtEnabled             = ms.SaveFullListsTxtEnabled,
            SaveFullListsCsvEnabled             = ms.SaveFullListsCsvEnabled,
            SaveMinimalListsTxtEnabled          = ms.SaveMinimalListsTxtEnabled,
            SaveMinimalListsCsvEnabled          = ms.SaveMinimalListsCsvEnabled,
            SaveFullStatsTxtFormattedEnabled    = ms.SaveFullStatsTxtFormattedEnabled,
            SaveMinimalStatsTxtFormattedEnabled = ms.SaveMinimalStatsTxtFormattedEnabled,
            SaveGridStatsTxtEnabled             = ms.SaveGridStatsTxtEnabled,
            SaveMinimalGridStatsTxtEnabled      = ms.SaveMinimalGridStatsTxtEnabled,
            SavePerformansTxtEnabled            = ms.SavePerformansTxtEnabled,
            SavePerformansCsvEnabled            = ms.SavePerformansCsvEnabled,
            FullStatsTxtFileName                = $"{filePrefix}_Main_{ms.FullStatsTxtFileName}",
            FullStatsCsvFileName                = $"{filePrefix}_Main_{ms.FullStatsCsvFileName}",
            MinimalStatsTxtFileName             = $"{filePrefix}_Main_{ms.MinimalStatsTxtFileName}",
            MinimalStatsCsvFileName             = $"{filePrefix}_Main_{ms.MinimalStatsCsvFileName}",
            FullListsTxtFileName                = $"{filePrefix}_Main_{ms.FullListsTxtFileName}",
            FullListsCsvFileName                = $"{filePrefix}_Main_{ms.FullListsCsvFileName}",
            MinimalListsTxtFileName             = $"{filePrefix}_Main_{ms.MinimalListsTxtFileName}",
            MinimalListsCsvFileName             = $"{filePrefix}_Main_{ms.MinimalListsCsvFileName}",
            FullStatsTxtFormattedFileName       = $"{filePrefix}_Main_{ms.FullStatsTxtFormattedFileName}",
            MinimalStatsTxtFormattedFileName    = $"{filePrefix}_Main_{ms.MinimalStatsTxtFormattedFileName}",
            GridStatsTxtFileName                = $"{filePrefix}_Main_{ms.GridStatsTxtFileName}",
            MinimalGridStatsTxtFileName         = $"{filePrefix}_Main_{ms.MinimalGridStatsTxtFileName}",
            PerformansTxtFileName               = $"{filePrefix}_Main_{ms.PerformansTxtFileName}",
            PerformansCsvFileName               = $"{filePrefix}_Main_{ms.PerformansCsvFileName}",
        });

        algoTrader.SetSingleTraderPlotConfig(new SingleTraderPlotConfig
        {
            PlotEnabled = cfg.MainTrader.Plot.PlotEnabled,
        });

        if (cfg.MainTrader.Export is not null)
        {
            algoTrader.SetSingleTraderExportConfig(new SingleTraderExportConfig
            {
                ExportEnabled    = cfg.MainTrader.Export.ExportEnabled,
                ExportConfigFile = Path.Combine(configsDir, cfg.MainTrader.Export.ConfigFile),
                ExportVersion    = cfg.MainTrader.Export.Version,
            });
        }

        // =====================================================================
        // Child stratejileri yükle (benzersiz Name+Version → tek _strategyConfigs girişi)
        // ApplyMultipleTrader ile birebir aynı desen — paylaşılan _strategyConfigs slotu.
        // =====================================================================
        algoTrader.ClearStrategyConfigs();
        var strategyIndexMap = new Dictionary<(string name, string version), int>(StringComparer.OrdinalIgnoreCase as IEqualityComparer<(string, string)>);
        int nextStratId = 0;

        foreach (var child in children)
        {
            var key = (child.Strategy.Name, child.Strategy.Version);
            if (!strategyIndexMap.ContainsKey(key))
            {
                string stratPath = Path.Combine(configsDir, child.Strategy.ConfigFile);
                var loader = new StrategyConfigLoader(stratPath);
                loader.LoadFromFile();
                var stratCfg = loader.GetConfiguration(child.Strategy.Name, child.Strategy.Version)
                    ?? throw new InvalidOperationException($"Strategy bulunamadı: {child.Strategy.Name} / {child.Strategy.Version}");
                algoTrader.AddStrategyConfig(nextStratId, stratCfg.StrategyName, stratCfg.GetParameterValues());
                strategyIndexMap[key] = nextStratId;
                nextStratId++;
            }
        }

        // MainTrader ECF (opsiyonel, id=0)
        algoTrader.ClearEquityCurveFilterConfigs();
        int nextEcfId = 0;
        if (cfg.MainTrader.EquityCurveFilter is not null)
        {
            string ecfPath = Path.Combine(configsDir, cfg.MainTrader.EquityCurveFilter.ConfigFile);
            algoTrader.ConfigureEquityCurveFilterFromConfig(ecfPath, cfg.MainTrader.EquityCurveFilter.Version, id: 0);
            nextEcfId = 1;
        }

        // Child ECF'ler
        var ecfIndexMap = new Dictionary<(string configFile, string version), int>(StringComparer.OrdinalIgnoreCase as IEqualityComparer<(string, string)>);
        foreach (var child in children)
        {
            if (child.EquityCurveFilter is null) continue;
            var key = (child.EquityCurveFilter.ConfigFile, child.EquityCurveFilter.Version);
            if (!ecfIndexMap.ContainsKey(key))
            {
                string ecfPath = Path.Combine(configsDir, child.EquityCurveFilter.ConfigFile);
                algoTrader.ConfigureEquityCurveFilterFromConfig(ecfPath, child.EquityCurveFilter.Version, id: nextEcfId);
                ecfIndexMap[key] = nextEcfId;
                nextEcfId++;
            }
        }

        // =====================================================================
        // ChildTraderConfigs oluştur — ApplyMultipleTrader ile birebir aynı
        // =====================================================================
        algoTrader.SetChildTraderCount(children.Count, (entry, i) =>
        {
            var child = children[i];

            var stratKey = (child.Strategy.Name, child.Strategy.Version);
            entry.StrategyId = strategyIndexMap[stratKey];

            if (child.EquityCurveFilter is not null)
            {
                var ecfKey = (child.EquityCurveFilter.ConfigFile, child.EquityCurveFilter.Version);
                entry.EcfConfigId = ecfIndexMap[ecfKey];
            }

            // TradeParams — MainTrader'dan (tüm child'lar aynı parametreleri kullanır)
            entry.TradeParams.ApplyFrom(mainTradeParams);

            var cs = child.Signals;
            entry.Signals = new SingleTraderSignalsConfig
            {
                AlEnabled              = cs.AlEnabled,
                SatEnabled             = cs.SatEnabled,
                FlatOlEnabled          = cs.FlatOlEnabled,
                PasGecEnabled          = cs.PasGecEnabled,
                KarAlEnabled           = cs.KarAlEnabled,
                ZararKesEnabled        = cs.ZararKesEnabled,
                GunSonuPozKapatEnabled = cs.GunSonuPozKapatEnabled,
                TimeFilteringEnabled      = cs.TimeFilteringEnabled,
                StartDateTime             = cs.StartDateTime,
                StopDateTime              = cs.StopDateTime,
                TradeStartBarIndexEnabled = cs.TradeStartBarIndexEnabled,
                TradeStartBarIndex        = cs.TradeStartBarIndex,
            };

            var sv = child.Save;
            string cp = $"{filePrefix}_SignalChild{i}";
            entry.Save = new SingleTraderSaveConfig
            {
                SaveStatisticsToFile                = sv.SaveStatisticsToFile,
                SaveFullStatsTxtEnabled             = sv.SaveFullStatsTxtEnabled,
                SaveFullStatsCsvEnabled             = sv.SaveFullStatsCsvEnabled,
                SaveMinimalStatsTxtEnabled          = sv.SaveMinimalStatsTxtEnabled,
                SaveMinimalStatsCsvEnabled          = sv.SaveMinimalStatsCsvEnabled,
                SaveFullListsTxtEnabled             = sv.SaveFullListsTxtEnabled,
                SaveFullListsCsvEnabled             = sv.SaveFullListsCsvEnabled,
                SaveMinimalListsTxtEnabled          = sv.SaveMinimalListsTxtEnabled,
                SaveMinimalListsCsvEnabled          = sv.SaveMinimalListsCsvEnabled,
                SaveFullStatsTxtFormattedEnabled    = sv.SaveFullStatsTxtFormattedEnabled,
                SaveMinimalStatsTxtFormattedEnabled = sv.SaveMinimalStatsTxtFormattedEnabled,
                SaveGridStatsTxtEnabled             = sv.SaveGridStatsTxtEnabled,
                SaveMinimalGridStatsTxtEnabled      = sv.SaveMinimalGridStatsTxtEnabled,
                SavePerformansTxtEnabled            = sv.SavePerformansTxtEnabled,
                SavePerformansCsvEnabled            = sv.SavePerformansCsvEnabled,
                FullStatsTxtFileName                = $"{cp}_{sv.FullStatsTxtFileName}",
                FullStatsCsvFileName                = $"{cp}_{sv.FullStatsCsvFileName}",
                MinimalStatsTxtFileName             = $"{cp}_{sv.MinimalStatsTxtFileName}",
                MinimalStatsCsvFileName             = $"{cp}_{sv.MinimalStatsCsvFileName}",
                FullListsTxtFileName                = $"{cp}_{sv.FullListsTxtFileName}",
                FullListsCsvFileName                = $"{cp}_{sv.FullListsCsvFileName}",
                MinimalListsTxtFileName             = $"{cp}_{sv.MinimalListsTxtFileName}",
                MinimalListsCsvFileName             = $"{cp}_{sv.MinimalListsCsvFileName}",
                FullStatsTxtFormattedFileName       = $"{cp}_{sv.FullStatsTxtFormattedFileName}",
                MinimalStatsTxtFormattedFileName    = $"{cp}_{sv.MinimalStatsTxtFormattedFileName}",
                GridStatsTxtFileName                = $"{cp}_{sv.GridStatsTxtFileName}",
                MinimalGridStatsTxtFileName         = $"{cp}_{sv.MinimalGridStatsTxtFileName}",
                PerformansTxtFileName               = $"{cp}_{sv.PerformansTxtFileName}",
                PerformansCsvFileName               = $"{cp}_{sv.PerformansCsvFileName}",
            };

            if (child.Export is not null)
            {
                entry.Export = new SingleTraderExportConfig
                {
                    ExportEnabled    = child.Export.ExportEnabled,
                    ExportConfigFile = Path.Combine(configsDir, child.Export.ConfigFile),
                    ExportVersion    = child.Export.Version,
                };
            }
        });
    }

    /// <summary>
    /// SingleTraderOpt bölümünü AlgoTrader'a uygular.
    /// </summary>
    public static void ApplySingleTraderOpt(AlgoTrader algoTrader, SingleTraderOptConfig cfg, string configsDir)
    {
        // Strategy
        string stratPath = Path.Combine(configsDir, cfg.Strategy.ConfigFile);
        algoTrader.ConfigureStrategyFromConfig(stratPath, cfg.Strategy.Name, cfg.Strategy.Version);

        // Optimization
        string optPath = Path.Combine(configsDir, cfg.Optimization.ConfigFile);
        algoTrader.ConfigureOptimizationFromConfig(optPath, cfg.Optimization.Name, cfg.Optimization.Version);

        // Range (PartialOpt)
        algoTrader.SetSingleTraderOptRangeConfig(new TradingOptRangeConfig
        {
            OptimizationFrom = cfg.Range.OptimizationFrom,
            OptimizationTo   = cfg.Range.OptimizationTo,
        });

        // TradeParams (MarketType dahil tam params — createSingleTrader() TradeParamsOverride olarak kullanır)
        algoTrader.SetSingleTraderTradeParams(BuildInitialTradeParams(cfg.TradeParams));

        algoTrader.SetSingleTraderOptTradeParamsConfig(new TradingOptTradeParamsConfig
        {
            IlkBakiye      = cfg.TradeParams.IlkBakiye,
            KontratSayisi  = (int)cfg.TradeParams.KontratSayisi,
            KomisyonCarpan = cfg.TradeParams.KomisyonCarpan,
            KaymaMiktari   = cfg.TradeParams.KaymaMiktari,
        });

        // EquityCurveFilter (opsiyonel)
        if (cfg.EquityCurveFilter is not null)
        {
            algoTrader.ClearEquityCurveFilterConfigs();
            string ecfPath = Path.Combine(configsDir, cfg.EquityCurveFilter.ConfigFile);
            algoTrader.ConfigureEquityCurveFilterFromConfig(ecfPath, cfg.EquityCurveFilter.Version, id: 0);
        }

        // Signals (her test trader'ına uygulanır)
        algoTrader.SetSingleTraderOptSignalsConfig(new SingleTraderSignalsConfig
        {
            AlEnabled              = cfg.Signals.AlEnabled,
            SatEnabled             = cfg.Signals.SatEnabled,
            FlatOlEnabled          = cfg.Signals.FlatOlEnabled,
            PasGecEnabled          = cfg.Signals.PasGecEnabled,
            KarAlEnabled           = cfg.Signals.KarAlEnabled,
            ZararKesEnabled        = cfg.Signals.ZararKesEnabled,
            GunSonuPozKapatEnabled = cfg.Signals.GunSonuPozKapatEnabled,
            TimeFilteringEnabled      = cfg.Signals.TimeFilteringEnabled,
            StartDateTime             = cfg.Signals.StartDateTime,
            StopDateTime              = cfg.Signals.StopDateTime,
            TradeStartBarIndexEnabled = cfg.Signals.TradeStartBarIndexEnabled,
            TradeStartBarIndex        = cfg.Signals.TradeStartBarIndex,
        });

        // Optimizer log
        algoTrader.SetSingleTraderOptLogConfig(new SingleTraderOptLogConfig
        {
            CsvFileLoggingEnabled               = cfg.Save.CsvFileLoggingEnabled,
            CsvFileName                         = cfg.Save.CsvFileName,
            TxtFileLoggingEnabled               = cfg.Save.TxtFileLoggingEnabled,
            TxtFileName                         = cfg.Save.TxtFileName,
            AppendEnabled                       = cfg.Save.AppendEnabled,
            StatisticsExporterConfigFileEnabled = cfg.Save.StatisticsExporterConfigFileEnabled,
            StatisticsExporterConfigFile        = cfg.Save.StatisticsExporterConfigFile,
            FileFlushIntervalMs                 = cfg.Save.FileFlushIntervalMs,
        });

        // Sort
        algoTrader.SetSingleTraderOptSortOutputConfig(new SingleTraderOptSortOutputConfig
        {
            SortField         = cfg.Sort.SortField,
            SortedCsvFileName = cfg.Sort.SortedCsvFileName,
            SortedTxtFileName = cfg.Sort.SortedTxtFileName,
        });

        // Best trader — Plot
        algoTrader.SetSingleTraderPlotConfig(new SingleTraderPlotConfig
        {
            PlotEnabled = cfg.SingleTrader.Plot.PlotEnabled,
        });

        // Best trader — Optimization
        algoTrader.SetSingleTraderOptimizationConfig(new SingleTraderOptimizationConfig
        {
            OptimizationEnabled = cfg.SingleTrader.Optimization.OptimizationEnabled,
        });

        // Best trader — Save
        algoTrader.SetSingleTraderSaveConfig(new SingleTraderSaveConfig
        {
            SaveStatisticsToFile                = cfg.SingleTrader.Save.SaveStatisticsToFile,
            SaveFullStatsTxtEnabled             = cfg.SingleTrader.Save.SaveFullStatsTxtEnabled,
            SaveFullStatsCsvEnabled             = cfg.SingleTrader.Save.SaveFullStatsCsvEnabled,
            SaveMinimalStatsTxtEnabled          = cfg.SingleTrader.Save.SaveMinimalStatsTxtEnabled,
            SaveMinimalStatsCsvEnabled          = cfg.SingleTrader.Save.SaveMinimalStatsCsvEnabled,
            SaveFullListsTxtEnabled             = cfg.SingleTrader.Save.SaveFullListsTxtEnabled,
            SaveFullListsCsvEnabled             = cfg.SingleTrader.Save.SaveFullListsCsvEnabled,
            SaveMinimalListsTxtEnabled          = cfg.SingleTrader.Save.SaveMinimalListsTxtEnabled,
            SaveMinimalListsCsvEnabled          = cfg.SingleTrader.Save.SaveMinimalListsCsvEnabled,
            SaveFullStatsTxtFormattedEnabled    = cfg.SingleTrader.Save.SaveFullStatsTxtFormattedEnabled,
            SaveMinimalStatsTxtFormattedEnabled = cfg.SingleTrader.Save.SaveMinimalStatsTxtFormattedEnabled,
            SavePerformansTxtEnabled            = cfg.SingleTrader.Save.SavePerformansTxtEnabled,
            SavePerformansCsvEnabled            = cfg.SingleTrader.Save.SavePerformansCsvEnabled,
            FullStatsTxtFileName                = cfg.SingleTrader.Save.FullStatsTxtFileName,
            FullStatsCsvFileName                = cfg.SingleTrader.Save.FullStatsCsvFileName,
            MinimalStatsTxtFileName             = cfg.SingleTrader.Save.MinimalStatsTxtFileName,
            MinimalStatsCsvFileName             = cfg.SingleTrader.Save.MinimalStatsCsvFileName,
            FullListsTxtFileName                = cfg.SingleTrader.Save.FullListsTxtFileName,
            FullListsCsvFileName                = cfg.SingleTrader.Save.FullListsCsvFileName,
            MinimalListsTxtFileName             = cfg.SingleTrader.Save.MinimalListsTxtFileName,
            MinimalListsCsvFileName             = cfg.SingleTrader.Save.MinimalListsCsvFileName,
            FullStatsTxtFormattedFileName       = cfg.SingleTrader.Save.FullStatsTxtFormattedFileName,
            MinimalStatsTxtFormattedFileName    = cfg.SingleTrader.Save.MinimalStatsTxtFormattedFileName,
            PerformansTxtFileName               = cfg.SingleTrader.Save.PerformansTxtFileName,
            PerformansCsvFileName               = cfg.SingleTrader.Save.PerformansCsvFileName,
        });

        // Best trader — Export
        if (cfg.SingleTrader.Export is not null)
        {
            algoTrader.SetSingleTraderExportConfig(new SingleTraderExportConfig
            {
                ExportEnabled    = cfg.SingleTrader.Export.ExportEnabled,
                ExportConfigFile = Path.Combine(configsDir, cfg.SingleTrader.Export.ConfigFile),
                ExportVersion    = cfg.SingleTrader.Export.Version,
            });
        }
    }

    // =========================================================================
    // SymbolScan (Tarama)
    // =========================================================================

    /// <summary>
    /// SymbolScanConfig'i, SymbolScanner.Run()'a doğrudan verilebilecek bir SymbolScanOptions'a
    /// çevirir. SymbolScanner bilinçli olarak AlgoTrader'a bağlı değil (bkz.
    /// docs/tarama-motoru-plan.md), o yüzden bu dönüşüm AlgoTrader üzerinden değil,
    /// diğer Build* metodları gibi doğrudan yapılır.
    /// </summary>
    public static SymbolScanOptions BuildSymbolScanOptions(SymbolScanConfig cfg, string configsDir)
    {
        string stratPath = Path.Combine(configsDir, cfg.Strategy.ConfigFile);
        var stratLoader  = new StrategyConfigLoader(stratPath);
        stratLoader.LoadFromFile();
        var stratCfg = stratLoader.GetConfiguration(cfg.Strategy.Name, cfg.Strategy.Version)
            ?? throw new InvalidOperationException($"Strategy bulunamadı: {cfg.Strategy.Name} / {cfg.Strategy.Version}");

        var options = new SymbolScanOptions
        {
            DataFolder             = cfg.DataFolder,
            AutoDiscover           = cfg.AutoDiscover,
            SymbolList             = cfg.SymbolList,
            StrategyName           = stratCfg.StrategyName,
            StrategyParameters     = stratCfg.GetParameterValues(),
            TradeParams            = BuildInitialTradeParams(cfg.TradeParams),
            AlEnabled              = cfg.Signals.AlEnabled,
            SatEnabled             = cfg.Signals.SatEnabled,
            FlatOlEnabled          = cfg.Signals.FlatOlEnabled,
            PasGecEnabled          = cfg.Signals.PasGecEnabled,
            KarAlEnabled           = cfg.Signals.KarAlEnabled,
            ZararKesEnabled        = cfg.Signals.ZararKesEnabled,
            GunSonuPozKapatEnabled = cfg.Signals.GunSonuPozKapatEnabled,
            WriteFullStatsPerSymbol = cfg.WriteFullStatsPerSymbol,
            SortField              = cfg.Sort.SortField,
            SortDescending         = cfg.Sort.SortDescending,
        };

        if (Enum.TryParse<AlgoTrade.Core.StockDataReader.StockDataReader.FilterMode>(cfg.ReadData.FilterMode, ignoreCase: true, out var filterMode))
            options.ReadFilterMode = filterMode;
        options.N1 = cfg.ReadData.N1;
        options.N2 = cfg.ReadData.N2;
        if (!string.IsNullOrWhiteSpace(cfg.ReadData.Dt1)) options.Dt1 = DateTime.Parse(cfg.ReadData.Dt1);
        if (!string.IsNullOrWhiteSpace(cfg.ReadData.Dt2)) options.Dt2 = DateTime.Parse(cfg.ReadData.Dt2);

        return options;
    }

    /// <summary>
    /// TimeframeScanConfig'i, TimeframeScanner.Run()'a doğrudan verilebilecek bir
    /// TimeframeScannerOptions'a çevirir. BuildSymbolScanOptions ile aynı desende —
    /// TimeframeScanner de bilinçli olarak AlgoTrader'a bağlı değil.
    /// </summary>
    public static TimeframeScannerOptions BuildTimeframeScanOptions(TimeframeScanConfig cfg, string configsDir)
    {
        string stratPath = Path.Combine(configsDir, cfg.Strategy.ConfigFile);
        var stratLoader  = new StrategyConfigLoader(stratPath);
        stratLoader.LoadFromFile();
        var stratCfg = stratLoader.GetConfiguration(cfg.Strategy.Name, cfg.Strategy.Version)
            ?? throw new InvalidOperationException($"Strategy bulunamadı: {cfg.Strategy.Name} / {cfg.Strategy.Version}");

        var options = new TimeframeScannerOptions
        {
            BaseFolder             = cfg.BaseFolder,
            Symbol                 = cfg.Symbol,
            Timeframes             = cfg.Timeframes,
            StrategyName           = stratCfg.StrategyName,
            StrategyParameters     = stratCfg.GetParameterValues(),
            TradeParams            = BuildInitialTradeParams(cfg.TradeParams),
            AlEnabled              = cfg.Signals.AlEnabled,
            SatEnabled             = cfg.Signals.SatEnabled,
            FlatOlEnabled          = cfg.Signals.FlatOlEnabled,
            PasGecEnabled          = cfg.Signals.PasGecEnabled,
            KarAlEnabled           = cfg.Signals.KarAlEnabled,
            ZararKesEnabled        = cfg.Signals.ZararKesEnabled,
            GunSonuPozKapatEnabled = cfg.Signals.GunSonuPozKapatEnabled,
            WriteFullStatsPerTimeframe = cfg.WriteFullStatsPerTimeframe,
            SortField              = cfg.Sort.SortField,
            SortDescending         = cfg.Sort.SortDescending,
        };

        if (Enum.TryParse<AlgoTrade.Core.StockDataReader.StockDataReader.FilterMode>(cfg.ReadData.FilterMode, ignoreCase: true, out var filterMode))
            options.ReadFilterMode = filterMode;
        options.N1 = cfg.ReadData.N1;
        options.N2 = cfg.ReadData.N2;
        if (!string.IsNullOrWhiteSpace(cfg.ReadData.Dt1)) options.Dt1 = DateTime.Parse(cfg.ReadData.Dt1);
        if (!string.IsNullOrWhiteSpace(cfg.ReadData.Dt2)) options.Dt2 = DateTime.Parse(cfg.ReadData.Dt2);

        return options;
    }

    /// <summary>
    /// SymbolTimeframeScanConfig'i, SymbolTimeframeScanner.Run()'a doğrudan verilebilecek bir
    /// SymbolTimeframeScanOptions'a çevirir. BuildSymbolScanOptions/BuildTimeframeScanOptions ile
    /// aynı desende — SymbolTimeframeScanner de bilinçli olarak AlgoTrader'a bağlı değil.
    /// </summary>
    public static SymbolTimeframeScanOptions BuildSymbolTimeframeScanOptions(SymbolTimeframeScanConfig cfg, string configsDir)
    {
        string stratPath = Path.Combine(configsDir, cfg.Strategy.ConfigFile);
        var stratLoader  = new StrategyConfigLoader(stratPath);
        stratLoader.LoadFromFile();
        var stratCfg = stratLoader.GetConfiguration(cfg.Strategy.Name, cfg.Strategy.Version)
            ?? throw new InvalidOperationException($"Strategy bulunamadı: {cfg.Strategy.Name} / {cfg.Strategy.Version}");

        var options = new SymbolTimeframeScanOptions
        {
            BaseFolder             = cfg.BaseFolder,
            AutoDiscover           = cfg.AutoDiscover,
            ReferenceTimeframe     = cfg.ReferenceTimeframe,
            SymbolList             = cfg.SymbolList,
            Timeframes             = cfg.Timeframes,
            StrategyName           = stratCfg.StrategyName,
            StrategyParameters     = stratCfg.GetParameterValues(),
            TradeParams            = BuildInitialTradeParams(cfg.TradeParams),
            AlEnabled              = cfg.Signals.AlEnabled,
            SatEnabled             = cfg.Signals.SatEnabled,
            FlatOlEnabled          = cfg.Signals.FlatOlEnabled,
            PasGecEnabled          = cfg.Signals.PasGecEnabled,
            KarAlEnabled           = cfg.Signals.KarAlEnabled,
            ZararKesEnabled        = cfg.Signals.ZararKesEnabled,
            GunSonuPozKapatEnabled = cfg.Signals.GunSonuPozKapatEnabled,
            WriteFullStatsPerCell  = cfg.WriteFullStatsPerCell,
            SortField              = cfg.Sort.SortField,
            SortDescending         = cfg.Sort.SortDescending,
        };

        if (Enum.TryParse<AlgoTrade.Core.StockDataReader.StockDataReader.FilterMode>(cfg.ReadData.FilterMode, ignoreCase: true, out var filterMode))
            options.ReadFilterMode = filterMode;
        options.N1 = cfg.ReadData.N1;
        options.N2 = cfg.ReadData.N2;
        if (!string.IsNullOrWhiteSpace(cfg.ReadData.Dt1)) options.Dt1 = DateTime.Parse(cfg.ReadData.Dt1);
        if (!string.IsNullOrWhiteSpace(cfg.ReadData.Dt2)) options.Dt2 = DateTime.Parse(cfg.ReadData.Dt2);

        return options;
    }

    // =========================================================================
    // QuerySymbolScan (Sorgu Tarama Matrisi — Senaryo 5)
    // =========================================================================

    /// <summary>
    /// QuerySymbolScanConfig'i, QuerySymbolScanner.Run()'a doğrudan verilebilecek bir
    /// QuerySymbolScanOptions'a çevirir. BuildSymbolScanOptions ile aynı desende — Strategy
    /// yerine Query, TradeParams/Signals yok (QueryOnly modda ikisi de kullanılmıyor).
    /// </summary>
    public static QuerySymbolScanOptions BuildQuerySymbolScanOptions(QuerySymbolScanConfig cfg, string configsDir)
    {
        string queryPath   = Path.Combine(configsDir, cfg.Query.ConfigFile);
        var    queryLoader = new QueryConfigLoader(queryPath);
        queryLoader.LoadFromFile();
        var queryCfg = queryLoader.GetConfiguration(cfg.Query.Name, cfg.Query.Version)
            ?? throw new InvalidOperationException($"Query bulunamadı: {cfg.Query.Name} / {cfg.Query.Version}");

        var options = new QuerySymbolScanOptions
        {
            DataFolder       = cfg.DataFolder,
            AutoDiscover     = cfg.AutoDiscover,
            SymbolList       = cfg.SymbolList,
            QueryName        = queryCfg.QueryName,
            QueryParameters  = queryCfg.GetParameterValues(),
        };

        if (Enum.TryParse<AlgoTrade.Core.StockDataReader.StockDataReader.FilterMode>(cfg.ReadData.FilterMode, ignoreCase: true, out var filterMode))
            options.ReadFilterMode = filterMode;
        options.N1 = cfg.ReadData.N1;
        options.N2 = cfg.ReadData.N2;
        if (!string.IsNullOrWhiteSpace(cfg.ReadData.Dt1)) options.Dt1 = DateTime.Parse(cfg.ReadData.Dt1);
        if (!string.IsNullOrWhiteSpace(cfg.ReadData.Dt2)) options.Dt2 = DateTime.Parse(cfg.ReadData.Dt2);

        return options;
    }

    // =========================================================================
    // QueryTimeframeScan (Sorgu Tarama Matrisi — Senaryo 2)
    // =========================================================================

    /// <summary>
    /// QueryTimeframeScanConfig'i, QueryTimeframeScanner.Run()'a doğrudan verilebilecek bir
    /// QueryTimeframeScannerOptions'a çevirir. BuildQuerySymbolScanOptions ile aynı desende.
    /// </summary>
    public static QueryTimeframeScannerOptions BuildQueryTimeframeScanOptions(QueryTimeframeScanConfig cfg, string configsDir)
    {
        string queryPath   = Path.Combine(configsDir, cfg.Query.ConfigFile);
        var    queryLoader = new QueryConfigLoader(queryPath);
        queryLoader.LoadFromFile();
        var queryCfg = queryLoader.GetConfiguration(cfg.Query.Name, cfg.Query.Version)
            ?? throw new InvalidOperationException($"Query bulunamadı: {cfg.Query.Name} / {cfg.Query.Version}");

        var options = new QueryTimeframeScannerOptions
        {
            BaseFolder      = cfg.BaseFolder,
            Symbol          = cfg.Symbol,
            Timeframes      = cfg.Timeframes,
            QueryName       = queryCfg.QueryName,
            QueryParameters = queryCfg.GetParameterValues(),
        };

        if (Enum.TryParse<AlgoTrade.Core.StockDataReader.StockDataReader.FilterMode>(cfg.ReadData.FilterMode, ignoreCase: true, out var filterMode))
            options.ReadFilterMode = filterMode;
        options.N1 = cfg.ReadData.N1;
        options.N2 = cfg.ReadData.N2;
        if (!string.IsNullOrWhiteSpace(cfg.ReadData.Dt1)) options.Dt1 = DateTime.Parse(cfg.ReadData.Dt1);
        if (!string.IsNullOrWhiteSpace(cfg.ReadData.Dt2)) options.Dt2 = DateTime.Parse(cfg.ReadData.Dt2);

        return options;
    }

    // =========================================================================
    // MultiQueryTimeframeScan (Sorgu Tarama Matrisi — Senaryo 4)
    // =========================================================================

    /// <summary>
    /// MultiQueryTimeframeScanConfig'i, MultiQueryTimeframeScanner.Run()'a doğrudan verilebilecek
    /// bir MultiQueryTimeframeScannerOptions'a çevirir. cfg.Queries listesindeki her QueryRef,
    /// kendi QueryConfig dosyasından yüklenip sırayla 0,1,2... QueryId ile bir QueryEntry'ye
    /// dönüştürülür.
    /// </summary>
    public static MultiQueryTimeframeScannerOptions BuildMultiQueryTimeframeScanOptions(MultiQueryTimeframeScanConfig cfg, string configsDir)
    {
        var options = new MultiQueryTimeframeScannerOptions
        {
            BaseFolder = cfg.BaseFolder,
            Symbol     = cfg.Symbol,
            Timeframes = cfg.Timeframes,
            Queries    = BuildQueryEntries(cfg.Queries, configsDir),
        };

        if (Enum.TryParse<AlgoTrade.Core.StockDataReader.StockDataReader.FilterMode>(cfg.ReadData.FilterMode, ignoreCase: true, out var filterMode))
            options.ReadFilterMode = filterMode;
        options.N1 = cfg.ReadData.N1;
        options.N2 = cfg.ReadData.N2;
        if (!string.IsNullOrWhiteSpace(cfg.ReadData.Dt1)) options.Dt1 = DateTime.Parse(cfg.ReadData.Dt1);
        if (!string.IsNullOrWhiteSpace(cfg.ReadData.Dt2)) options.Dt2 = DateTime.Parse(cfg.ReadData.Dt2);

        return options;
    }

    // =========================================================================
    // QuerySymbolTimeframeScan (Sorgu Tarama Matrisi — Senaryo 6)
    // =========================================================================

    /// <summary>
    /// QuerySymbolTimeframeScanConfig'i, QuerySymbolTimeframeScanner.Run()'a doğrudan
    /// verilebilecek bir QuerySymbolTimeframeScannerOptions'a çevirir.
    /// </summary>
    public static QuerySymbolTimeframeScannerOptions BuildQuerySymbolTimeframeScanOptions(QuerySymbolTimeframeScanConfig cfg, string configsDir)
    {
        string queryPath   = Path.Combine(configsDir, cfg.Query.ConfigFile);
        var    queryLoader = new QueryConfigLoader(queryPath);
        queryLoader.LoadFromFile();
        var queryCfg = queryLoader.GetConfiguration(cfg.Query.Name, cfg.Query.Version)
            ?? throw new InvalidOperationException($"Query bulunamadı: {cfg.Query.Name} / {cfg.Query.Version}");

        var options = new QuerySymbolTimeframeScannerOptions
        {
            BaseFolder          = cfg.BaseFolder,
            AutoDiscover        = cfg.AutoDiscover,
            ReferenceTimeframe  = cfg.ReferenceTimeframe,
            SymbolList          = cfg.SymbolList,
            Timeframes          = cfg.Timeframes,
            QueryName           = queryCfg.QueryName,
            QueryParameters     = queryCfg.GetParameterValues(),
        };

        if (Enum.TryParse<AlgoTrade.Core.StockDataReader.StockDataReader.FilterMode>(cfg.ReadData.FilterMode, ignoreCase: true, out var filterMode))
            options.ReadFilterMode = filterMode;
        options.N1 = cfg.ReadData.N1;
        options.N2 = cfg.ReadData.N2;
        if (!string.IsNullOrWhiteSpace(cfg.ReadData.Dt1)) options.Dt1 = DateTime.Parse(cfg.ReadData.Dt1);
        if (!string.IsNullOrWhiteSpace(cfg.ReadData.Dt2)) options.Dt2 = DateTime.Parse(cfg.ReadData.Dt2);

        return options;
    }

    // =========================================================================
    // MultiQuerySymbolScan (Sorgu Tarama Matrisi — Senaryo 7)
    // =========================================================================

    /// <summary>
    /// MultiQuerySymbolScanConfig'i, MultiQuerySymbolScanner.Run()'a doğrudan verilebilecek bir
    /// MultiQuerySymbolScannerOptions'a çevirir. BuildMultiQueryTimeframeScanOptions ile aynı
    /// desende — döngü değişkeni TF yerine sembol.
    /// </summary>
    public static MultiQuerySymbolScannerOptions BuildMultiQuerySymbolScanOptions(MultiQuerySymbolScanConfig cfg, string configsDir)
    {
        var options = new MultiQuerySymbolScannerOptions
        {
            DataFolder   = cfg.DataFolder,
            AutoDiscover = cfg.AutoDiscover,
            SymbolList   = cfg.SymbolList,
            Queries      = BuildQueryEntries(cfg.Queries, configsDir),
        };

        if (Enum.TryParse<AlgoTrade.Core.StockDataReader.StockDataReader.FilterMode>(cfg.ReadData.FilterMode, ignoreCase: true, out var filterMode))
            options.ReadFilterMode = filterMode;
        options.N1 = cfg.ReadData.N1;
        options.N2 = cfg.ReadData.N2;
        if (!string.IsNullOrWhiteSpace(cfg.ReadData.Dt1)) options.Dt1 = DateTime.Parse(cfg.ReadData.Dt1);
        if (!string.IsNullOrWhiteSpace(cfg.ReadData.Dt2)) options.Dt2 = DateTime.Parse(cfg.ReadData.Dt2);

        return options;
    }

    // =========================================================================
    // MultiQuerySymbolTimeframeScan (Sorgu Tarama Matrisi — Senaryo 8)
    // =========================================================================

    /// <summary>
    /// MultiQuerySymbolTimeframeScanConfig'i, MultiQuerySymbolTimeframeScanner.Run()'a doğrudan
    /// verilebilecek bir MultiQuerySymbolTimeframeScannerOptions'a çevirir.
    /// </summary>
    public static MultiQuerySymbolTimeframeScannerOptions BuildMultiQuerySymbolTimeframeScanOptions(MultiQuerySymbolTimeframeScanConfig cfg, string configsDir)
    {
        var options = new MultiQuerySymbolTimeframeScannerOptions
        {
            BaseFolder         = cfg.BaseFolder,
            AutoDiscover       = cfg.AutoDiscover,
            ReferenceTimeframe = cfg.ReferenceTimeframe,
            SymbolList         = cfg.SymbolList,
            Timeframes         = cfg.Timeframes,
            Queries            = BuildQueryEntries(cfg.Queries, configsDir),
        };

        if (Enum.TryParse<AlgoTrade.Core.StockDataReader.StockDataReader.FilterMode>(cfg.ReadData.FilterMode, ignoreCase: true, out var filterMode))
            options.ReadFilterMode = filterMode;
        options.N1 = cfg.ReadData.N1;
        options.N2 = cfg.ReadData.N2;
        if (!string.IsNullOrWhiteSpace(cfg.ReadData.Dt1)) options.Dt1 = DateTime.Parse(cfg.ReadData.Dt1);
        if (!string.IsNullOrWhiteSpace(cfg.ReadData.Dt2)) options.Dt2 = DateTime.Parse(cfg.ReadData.Dt2);

        return options;
    }

    /// <summary>
    /// Bir QueryRef listesini (AppConfig.json'daki "Queries" bloğu) sırayla 0,1,2... QueryId
    /// atanmış QueryEntry listesine çevirir — MultiQueryTimeframeScan/MultiQuerySymbolScan/
    /// MultiQuerySymbolTimeframeScan tarafından ortak kullanılır.
    /// </summary>
    private static List<QueryEntry> BuildQueryEntries(List<QueryRef> queryRefs, string configsDir)
    {
        var entries = new List<QueryEntry>();

        for (int i = 0; i < queryRefs.Count; i++)
        {
            var qRef = queryRefs[i];
            string queryPath   = Path.Combine(configsDir, qRef.ConfigFile);
            var    queryLoader = new QueryConfigLoader(queryPath);
            queryLoader.LoadFromFile();
            var queryCfg = queryLoader.GetConfiguration(qRef.Name, qRef.Version)
                ?? throw new InvalidOperationException($"Query bulunamadı: {qRef.Name} / {qRef.Version}");

            entries.Add(new QueryEntry
            {
                QueryId         = i,
                QueryName       = queryCfg.QueryName,
                QueryParameters = queryCfg.GetParameterValues(),
            });
        }

        return entries;
    }

    // =========================================================================
    // TradeParamsConfig → InitialTradeParams dönüşümü
    // =========================================================================

    /// <summary>
    /// TradeParamsConfig'den tam olarak yapılandırılmış bir InitialTradeParams oluşturur.
    /// MarketType string'i enum'a çevrilir, ilgili SetKontratParams* metodu çağrılır.
    /// </summary>
    public static InitialTradeParams BuildInitialTradeParams(TradeParamsConfig cfg)
    {
        var p = new InitialTradeParams();
        p.Reset();
        p.SetBakiyeParams(ilkBakiye: cfg.IlkBakiye);
        p.SetKomisyonParams(komisyonCarpan: cfg.KomisyonCarpan);
        p.SetKaymaParams(kaymaMiktari: cfg.KaymaMiktari);
        p.PyramidingEnabled = cfg.PyramidingEnabled;

        if (!Enum.TryParse<MarketTypes>(cfg.MarketType, ignoreCase: true, out var marketType))
            throw new ArgumentException($"Geçersiz MarketType: '{cfg.MarketType}'. " +
                "Geçerli değerler: BistEndex, BistHisse, BistParite, BistMetal, " +
                "ViopEndex, ViopHisse, ViopParite, ViopMetal, " +
                "FxEndex, FxHisse, FxParite, FxMetal, FxCrypto, Crypto");

        switch (marketType)
        {
            case MarketTypes.BistEndex:   p.SetKontratParamsBistEndex(hisseSayisi: cfg.HisseSayisi); break;
            case MarketTypes.BistHisse:   p.SetKontratParamsBistHisse(hisseSayisi: cfg.HisseSayisi); break;
            case MarketTypes.BistParite:  p.SetKontratParamsBistParite(hisseSayisi: cfg.HisseSayisi); break;
            case MarketTypes.BistMetal:   p.SetKontratParamsBistMetal(hisseSayisi: cfg.HisseSayisi); break;
            case MarketTypes.ViopEndex:   p.SetKontratParamsViopEndex(kontratSayisi: cfg.KontratSayisi); break;
            case MarketTypes.ViopHisse:   p.SetKontratParamsViopHisse(kontratSayisi: cfg.KontratSayisi); break;
            case MarketTypes.ViopParite:  p.SetKontratParamsViopParite(kontratSayisi: cfg.KontratSayisi); break;
            case MarketTypes.ViopMetal:   p.SetKontratParamsViopMetal(kontratSayisi: cfg.KontratSayisi); break;
            case MarketTypes.FxEndex:     p.SetKontratParamsFxEndex(lotSayisi: cfg.LotSayisi); break;
            case MarketTypes.FxHisse:     p.SetKontratParamsFxHisse(lotSayisi: cfg.LotSayisi); break;
            case MarketTypes.FxParite:    p.SetKontratParamsFxParite(lotSayisi: cfg.LotSayisi); break;
            case MarketTypes.FxMetal:     p.SetKontratParamsFxMetal(lotSayisi: cfg.LotSayisi); break;
            case MarketTypes.FxCrypto:    p.SetKontratParamsFxCrypto(lotSayisi: cfg.LotSayisi); break;
            case MarketTypes.Crypto:      p.SetKontratParamsCrypto(lotSayisi: cfg.LotSayisi); break;
        }

        // SetKontratParams* komisyon sıfırlayabilir, tekrar set et
        p.SetKomisyonParams(komisyonCarpan: cfg.KomisyonCarpan);
        p.SetKaymaParams(kaymaMiktari: cfg.KaymaMiktari);

        // MicroLot için VarlikAdedSayisiMicro / KomisyonVarlikAdedSayisiMicro hesapla
        p.CalculateVarlikAdedSayisi();

        return p;
    }
}
