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
            TimeFilteringEnabled   = cfg.Signals.TimeFilteringEnabled,
            StartDateTime          = cfg.Signals.StartDateTime,
            StopDateTime           = cfg.Signals.StopDateTime,
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
            TimeFilteringEnabled   = cfg.MainTrader.Signals.TimeFilteringEnabled,
            StartDateTime          = cfg.MainTrader.Signals.StartDateTime,
            StopDateTime           = cfg.MainTrader.Signals.StopDateTime,
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
                TimeFilteringEnabled   = cs.TimeFilteringEnabled,
                StartDateTime          = cs.StartDateTime,
                StopDateTime           = cs.StopDateTime,
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
            TimeFilteringEnabled   = cfg.Signals.TimeFilteringEnabled,
            StartDateTime          = cfg.Signals.StartDateTime,
            StopDateTime           = cfg.Signals.StopDateTime,
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

        return p;
    }
}
