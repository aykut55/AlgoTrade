using AlgoTrade.Core.Trading;
using AlgoTrade.Core.Trading.Core;
using AlgoTrade.Core.Trading.EquityCurve;
using AlgoTrade.Core.Trading.Strategies;
using AlgoTrade.Core.Trading.Queries;

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

        // Save (OnApplyUserFlags2 karşılığı)
        algoTrader.SetSingleTraderSaveConfig(new SingleTraderSaveConfig
        {
            OptimizationEnabled                 = cfg.Save.OptimizationEnabled,
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
        });
    }

    /// <summary>
    /// MultipleTrader bölümünü AlgoTrader'a uygular.
    /// Her ChildTraderEntry için strategy, query, ecf ve trade params uygulanır.
    /// </summary>
    public static void ApplyMultipleTrader(AlgoTrader algoTrader, MultipleTraderConfig cfg, string configsDir)
    {
        var children = cfg.ChildTraders;
        if (children.Count == 0)
            throw new InvalidOperationException("MultipleTrader.ChildTraders boş — en az 1 child tanımlanmalı.");

        // Stratejileri yükle (benzersiz Name+Version → tek _strategyConfigs girişi)
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

        // QueryConfigs yükle
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

        // ECF configs yükle
        algoTrader.ClearEquityCurveFilterConfigs();
        var ecfIndexMap = new Dictionary<(string configFile, string version), int>(StringComparer.OrdinalIgnoreCase as IEqualityComparer<(string, string)>);
        int nextEcfId = 0;

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

        // ChildTraderConfigs oluştur
        algoTrader.SetChildTraderCount(children.Count, (entry, i) =>
        {
            var child = children[i];

            // StrategyId
            var stratKey = (child.Strategy.Name, child.Strategy.Version);
            entry.StrategyId = strategyIndexMap[stratKey];

            // QueryId (null → createChildTraders childId kullanır)
            if (child.Query is not null)
            {
                var qKey = (child.Query.Name, child.Query.Version);
                entry.QueryId = queryIndexMap[qKey];
            }

            // EcfConfigId (null → createChildTraders childId kullanır)
            if (child.EquityCurveFilter is not null)
            {
                var ecfKey = (child.EquityCurveFilter.ConfigFile, child.EquityCurveFilter.Version);
                entry.EcfConfigId = ecfIndexMap[ecfKey];
            }

            // TradeParams
            var tradeParams = BuildInitialTradeParams(child.TradeParams);
            entry.TradeParams.ApplyFrom(tradeParams);
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

        // EquityCurveFilter (opsiyonel)
        if (cfg.EquityCurveFilter is not null)
        {
            algoTrader.ClearEquityCurveFilterConfigs();
            string ecfPath = Path.Combine(configsDir, cfg.EquityCurveFilter.ConfigFile);
            algoTrader.ConfigureEquityCurveFilterFromConfig(ecfPath, cfg.EquityCurveFilter.Version, id: 0);
        }

        // TradeParams → Optimization trade params
        var tp = cfg.TradeParams;
        algoTrader.SetOptimizationTradeParams(
            ilkBakiye:      tp.IlkBakiye,
            kontratSayisi:  (int)tp.KontratSayisi,
            komisyonCarpan: tp.KomisyonCarpan,
            kaymaMiktari:   tp.KaymaMiktari);
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
