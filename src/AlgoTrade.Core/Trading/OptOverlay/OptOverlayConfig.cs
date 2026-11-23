using System.Text.Json;
using AlgoTrade.Core.Trading;
using AlgoTrade.Core.Trading.Core;

namespace AlgoTrade.Core.Trading.OptOverlay;

/// <summary>
/// "Opt Sonuçları Overlay" menü maddesinin okuduğu JSON dosyasının şeması.
/// Bkz. docs/opt-sonuclari-overlay-plot-plani.md.
///
/// Kullanıcı, opt sonuçlarından (Seçim01/Seçim02 vb.) elle seçtiği parametre
/// kombinasyonlarını bu dosyaya "runs" listesi olarak ekler. Her run, var olan
/// StrategyRegistry.CreateStrategy(name, paramsDict) (ham sözlük, StrategyConfig.txt
/// versiyonlarından BAĞIMSIZ) yoluyla çalıştırılır - "version" alanı burada sadece
/// bilgi/etiket amaçlıdır, StrategyConfig.txt'teki bir presete karşılık GELMEZ.
///
/// Stock data dosyası BU CONFIG'DE YOK - diğer "Read Data + X" menü maddeleriyle (5/6/7/23/25/
/// 27/29) aynı kural: menü [31] da önce handleReadData()'yı çağırır, veri AppConfig.json'daki
/// ReadData ayarından gelir. Tüm run'lar o an belleğe yüklü olan AYNI veriyi kullanır.
/// </summary>
public sealed class OptOverlayConfig
{
    /// <summary>Tüm run'lara ortak varsayılanlar (strateji adı/versiyon/sinyal override'ı). Her run dilediğini override edebilir.</summary>
    public OptOverlayBaseConfig? BaseConfig { get; set; }

    public List<OptOverlayRunEntry> Runs { get; set; } = new();

    /// <summary>
    /// OHLC/Signals panelindeki "ana" (mainTrader) sinyal çizgisinin NEREDEN geleceğini seçen
    /// TEK alan - iki olasılık:
    ///   - "composite" (case-insensitive) -> CompositeSignal ayarına göre N run'dan bileşke türetilir.
    ///   - runs[].Id'den biri (örn. "3") -> o run'ın KENDİ sinyali ana çizgi olur.
    /// Boş/null bırakılırsa varsayılan davranış korunur: mainTrader = overlayTraders[0]'ın
    /// kendi sinyali (bkz. plan dokümanı §3.1 "run[0]" notu). Bkz. §2.5 (bileşke sinyal tasarımı).
    /// </summary>
    public string? MainSignalSource { get; set; }

    /// <summary>MainSignalSource="composite" iken kullanılan kural ayarları.</summary>
    public OptOverlayCompositeSignalConfig? CompositeSignal { get; set; }
}

/// <summary>
/// v1 kural seti (2026-11-22, kullanıcı ile birlikte tasarlandı - İLERİDE GENİŞLETİLECEK,
/// bkz. plan dokümanı §2.5): katılımcı run'ların HER BARDAKİ kendi yönü (A/S/F, SonYon) baz
/// alınır - hepsi A ise bileşke A, hepsi S ise bileşke S, AKSİ HALDE (karışık ya da herhangi
/// biri F) bileşke F. Bu, kullanıcının o anki yön duruşuna (long-only/short-only/nötr) göre
/// "hepsi AL dese de ben FLAT isterim" gibi farklı eşlemeler istemesine açık bırakılmış bir
/// başlangıç noktası - "Mode" alanı ileride "unanimous" dışında başka kural setleri (örn.
/// çoğunluk oyu, yön-duruşuna-göre-ters-eşleme) eklenebilsin diye şimdiden var.
/// </summary>
public sealed class OptOverlayCompositeSignalConfig
{
    /// <summary>
    /// "unanimous" (hepsi A -> A, hepsi S -> S, aksi halde F) ya da "majority" (en çok oyu alan
    /// yön - eşitlikte F). Bkz. OptOverlaySignalCompositor.Compute().
    /// </summary>
    public string Mode { get; set; } = "unanimous";

    /// <summary>Bileşkeye katılacak run id'leri. Null/boş ise TÜM çalıştırılan run'lar katılır.</summary>
    public List<string>? RunIds { get; set; }
}

public sealed class OptOverlayBaseConfig
{
    public string? StrategyName { get; set; }
    public string? Version { get; set; }
    public OptOverlaySignalsOverride? Signals { get; set; }
}

public sealed class OptOverlayRunEntry
{
    /// <summary>Bu run'ı diğer config alanlarından (örn. ileride eklenecek bileşke/mainSignalSource
    /// tanımından) referans almak için kullanılan kimlik. Boş bırakılabilir - ama referans
    /// verilecekse (id ile seçim/bileşke tanımı) doldurulmalı.</summary>
    public string? Id { get; set; }

    /// <summary>Plot legend'ında görünecek metin. Boş bırakılırsa Params'tan otomatik üretilir.</summary>
    public string? Label { get; set; }

    /// <summary>Boş bırakılırsa BaseConfig.StrategyName kullanılır.</summary>
    public string? StrategyName { get; set; }

    /// <summary>Sadece bilgi/etiket amaçlı - çalıştırmayı etkilemez (bkz. sınıf başı not).</summary>
    public string? Version { get; set; }

    /// <summary>Strateji constructor parametreleri (case-insensitive isim eşleşmesi, StrategyRegistry ile aynı).</summary>
    public Dictionary<string, object>? Params { get; set; }

    /// <summary>Bu run için BaseConfig.Signals'ı override eden alanlar (sadece null olmayanlar uygulanır).</summary>
    public OptOverlaySignalsOverride? Signals { get; set; }
}

/// <summary>
/// AppConfigApplier.ApplySingleTrader()'ın uyguladığı Signals baseline'ının üzerine,
/// run bazında SADECE belirtilen (null olmayan) alanları override eden küçük bir yama seti.
/// Seçim01 ("sadece al/sat") ve Seçim02 ("al/sat + gün sonu kapat") gibi farklı
/// konfigürasyonları AYNI JSON listesinde karıştırabilmek için eklendi
/// (bkz. docs/opt-sonuclari-overlay-plot-plani.md §2.3).
/// </summary>
public sealed class OptOverlaySignalsOverride
{
    public bool? AlEnabled { get; set; }
    public bool? SatEnabled { get; set; }
    public bool? FlatOlEnabled { get; set; }
    public bool? PasGecEnabled { get; set; }
    public bool? KarAlEnabled { get; set; }
    public bool? ZararKesEnabled { get; set; }
    public bool? GunSonuPozKapatEnabled { get; set; }
}

public static class OptOverlayConfigLoader
{
    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling         = JsonCommentHandling.Skip,
        AllowTrailingCommas         = true
    };

    public static OptOverlayConfig Load(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Opt overlay config bulunamadı: {filePath}", filePath);

        string json = File.ReadAllText(filePath);
        var config = JsonSerializer.Deserialize<OptOverlayConfig>(json, _options)
            ?? throw new InvalidOperationException($"Opt overlay config parse edilemedi: {filePath}");

        if (config.Runs.Count == 0)
            throw new InvalidOperationException($"'{filePath}' içindeki 'runs' listesi boş.");

        return config;
    }

    /// <summary>Run'ın efektif strateji adı + parametreleri + sinyal override'ı (BaseConfig ile birleştirilmiş).</summary>
    public static (string strategyName, Dictionary<string, object> parameters, OptOverlaySignalsOverride? signals)
        Resolve(OptOverlayConfig config, OptOverlayRunEntry run)
    {
        string? strategyName = run.StrategyName ?? config.BaseConfig?.StrategyName;
        if (string.IsNullOrWhiteSpace(strategyName))
            throw new InvalidOperationException(
                $"Run '{ResolveLabel(run, -1)}' için strategyName belirtilmedi (ne run'da ne baseConfig'de).");

        var parameters = run.Params is null
            ? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, object>(run.Params, StringComparer.OrdinalIgnoreCase);

        var signals = run.Signals ?? config.BaseConfig?.Signals;

        return (strategyName, parameters, signals);
    }

    /// <summary>
    /// Label (varsa) her zaman önek olarak kullanılır, Params'tan ("period=21, multiplier=4.25"
    /// gibi) üretilen metinle birleştirilir - örn. label="100" + params={period:21,multiplier:4.25}
    /// -> "100: period=21, multiplier=4.25". Label verilmezse önek olarak 1-based sıra numarası
    /// (index+1) kullanılır. Params yoksa sadece önek (ya da "Run N") döner.
    /// </summary>
    public static string ResolveLabel(OptOverlayRunEntry run, int index)
    {
        string? prefix = !string.IsNullOrWhiteSpace(run.Label)
            ? run.Label!.Trim()
            : (index >= 0 ? $"{index + 1}" : null);

        if (run.Params is { Count: > 0 })
        {
            var parts = run.Params.Select(kv => $"{kv.Key}={JsonValueToDisplayString(kv.Value)}");
            string paramsText = string.Join(", ", parts);
            return prefix != null ? $"{prefix}: {paramsText}" : paramsText;
        }

        if (prefix != null)
            return prefix;

        return "Run";
    }

    private static string JsonValueToDisplayString(object? value)
    {
        if (value is JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString() ?? "",
                JsonValueKind.Number => element.ToString(),
                JsonValueKind.True   => "true",
                JsonValueKind.False  => "false",
                _                    => element.ToString()
            };
        }
        return value?.ToString() ?? "";
    }
}

/// <summary>
/// Bileşke kuralı hesaplayıcıları (bkz. OptOverlayCompositeSignalConfig, plan dokümanı §2.5).
/// Katılımcı run'ların HER BARDAKİ SinyalList'ine (SonYon; 1.0=A, -1.0=S, 0.0=F - bkz.
/// SingleTrader.cs) bakar.
/// </summary>
public static class OptOverlaySignalCompositor
{
    /// <summary>OptOverlayCompositeSignalConfig.Mode değerine göre doğru hesaplayıcıyı çağırır.</summary>
    public static double[] Compute(string? mode, IReadOnlyList<SingleTrader> participants)
    {
        return (mode ?? "unanimous").Trim().ToLowerInvariant() switch
        {
            "majority" or "cogunluk" or "çoğunluk" => ComputeMajority(participants),
            _ => ComputeUnanimous(participants),
        };
    }

    /// <summary>Hepsi A ise bileşke A, hepsi S ise bileşke S, aksi halde (karışık/herhangi biri F) bileşke F.</summary>
    public static double[] ComputeUnanimous(IReadOnlyList<SingleTrader> participants)
    {
        if (participants == null || participants.Count == 0)
            return Array.Empty<double>();

        int barCount = participants.Min(t => t.lists?.SinyalList?.Count ?? 0);
        var composite = new double[barCount];

        for (int i = 0; i < barCount; i++)
        {
            bool allA = true, allS = true;
            foreach (var trader in participants)
            {
                double v = trader.lists!.SinyalList[i];
                if (v != 1.0)  allA = false;
                if (v != -1.0) allS = false;
            }

            composite[i] = allA ? 1.0 : (allS ? -1.0 : 0.0);
        }

        return composite;
    }

    /// <summary>
    /// Her barda A/S/F sayılarına bakar, en çok oyu alan yön bileşke olur. Eşitlik durumunda
    /// (örn. 1 A, 1 S, 1 F) kazanan belirsiz olduğu için güvenli varsayılan F seçilir.
    /// </summary>
    public static double[] ComputeMajority(IReadOnlyList<SingleTrader> participants)
    {
        if (participants == null || participants.Count == 0)
            return Array.Empty<double>();

        int barCount = participants.Min(t => t.lists?.SinyalList?.Count ?? 0);
        var composite = new double[barCount];

        for (int i = 0; i < barCount; i++)
        {
            int countA = 0, countS = 0, countF = 0;
            foreach (var trader in participants)
            {
                double v = trader.lists!.SinyalList[i];
                if (v == 1.0) countA++;
                else if (v == -1.0) countS++;
                else countF++;
            }

            if (countA > countS && countA > countF)
                composite[i] = 1.0;
            else if (countS > countA && countS > countF)
                composite[i] = -1.0;
            else
                composite[i] = 0.0; // esitlik ya da F cogunlukta
        }

        return composite;
    }

    /// <summary>
    /// Sürekli yön dizisini (1.0=A/-1.0=S/0.0=F, her bar tekrar eden) kenar-tetikli komut dizisine
    /// çevirir — gerçek stratejilerin "sadece kesişim barında Buy/Sell" davranışıyla aynı. Bar -1
    /// (dizi başlangıcı) F kabul edilir. Precomputed-signal trader'a (composite'e kendi P&L'ini
    /// ürettirmek için) beslenir - bkz. AlgoTrade.Core.Trading.Strategies.ReplayStrategies.ReplaySignalStrategy,
    /// docs/opt-sonuclari-overlay-plot-plani.md §3.7.
    /// </summary>
    public static TradeSignals[] ToEdgeTriggeredCommands(double[] directions)
    {
        var commands = new TradeSignals[directions.Length];
        double prev = 0.0;
        for (int i = 0; i < directions.Length; i++)
        {
            double cur = directions[i];
            if (cur == prev)
                commands[i] = TradeSignals.None;
            else if (cur == 1.0)
                commands[i] = TradeSignals.Buy;
            else if (cur == -1.0)
                commands[i] = TradeSignals.Sell;
            else
                commands[i] = TradeSignals.Flat;
            prev = cur;
        }
        return commands;
    }
}
