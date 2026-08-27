using AlgoTrade.Core;
using AlgoTrade.Core.Trading.Indicators;
using AlgoTrade.Core.Trading.Core;
using AlgoTrade.Core.Trading.Strategy;
using System;
using System.Collections.Generic;

namespace AlgoTrade.Core.Trading.Strategies
{
    /// <summary>
    /// Combo Stratejisi (ISKELET) - Sabit bir "seri katalogu" (MA/RSI/MACD/SuperTrend...) uzerinden,
    /// farkli AL/SAT kurallarini (Comparison listeleri) ruleIndex ile secip calistirir.
    ///
    /// Tasarim amaci: yeni bir indikator veya yeni bir kural denemek icin ne constructor imzasi
    /// ne de yeni bir Strategy sinifi gerekiyor:
    /// - Yeni indikator eklemek  -> BuildSeriesCatalog()'a bir satir eklenir.
    /// - Yeni kural denemek      -> BuildRuleCatalog()'a bir (Buy, Sell) elemani eklenir.
    /// Optimizer sadece ruleIndex'i tarar (AddOptimizationParameterRange("ruleIndex", 0, N-1, 1)).
    ///
    /// Comparison, hem farkli serileri ayni bar'da (MA5 vs MA8) hem de ayni serinin farkli
    /// lag'lerini (Rsi[-3] vs Rsi[-1]) tek bir mekanizmayla karsilastirir - ozel durum yok.
    ///
    /// Parametreler:
    /// - ruleIndex: BuildRuleCatalog()'daki hangi kural setinin kullanilacagi (varsayilan 0)
    /// </summary>
    public class SimpleComboStrategy : BaseStrategy
    {
        public override string Name => "Simple Combo Strategy";

        // =========================================================================
        // Rule primitives - tum karsilastirmalar bu iki tip uzerinden ifade edilir
        // =========================================================================

        /// <summary>Bir seriye (indikator adi) ve bar lag'ine referans. Lag=0 -> currentIndex.</summary>
        public readonly record struct SeriesRef(string SeriesName, int Lag = 0);

        public enum CompareOp { GT, LT, GTE, LTE }

        /// <summary>Iki SeriesRef arasindaki tek bir karsilastirma (AND'lenerek kural olusturur).</summary>
        public readonly record struct Comparison(SeriesRef Left, CompareOp Op, SeriesRef Right);

        private readonly int _ruleIndex;
        private Dictionary<string, double[]>? _series;
        private List<Comparison>? _buyRule;
        private List<Comparison>? _sellRule;

        public SimpleComboStrategy(List<StockData> data, IndicatorManager indicators, int ruleIndex = 0)
        {
            _ruleIndex = ruleIndex;
            Parameters["RuleIndex"] = ruleIndex;

            Initialize(data, indicators);
        }

        public override void OnInit()
        {
            if (!IsInitialized)
                return;

            _series = BuildSeriesCatalog(Indicators);

            var ruleCatalog = BuildRuleCatalog();
            if (_ruleIndex < 0 || _ruleIndex >= ruleCatalog.Count)
                throw new ArgumentOutOfRangeException(nameof(_ruleIndex),
                    $"ruleIndex {_ruleIndex} gecersiz (0-{ruleCatalog.Count - 1} arasinda olmali).");

            (_buyRule, _sellRule) = ruleCatalog[_ruleIndex];
        }

        // =========================================================================
        // 1) SERI KATALOGU - TODO: kendi indikator setini burada tanimla
        //    (periyotlar burada SABIT kalir, optimizer bunlari degil ruleIndex'i tarar)
        // =========================================================================
        private static Dictionary<string, double[]> BuildSeriesCatalog(IndicatorManager ind)
        {
            var closes = ind.GetClosePrices();

            var rsi  = ind.Momentum.RSI(closes, 14);
            var macd = ind.Momentum.MACD(closes, 12, 26, 9);
            var st   = ind.Trend.SuperTrend(10, 3.0);

            return new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["MA5"]        = ind.MA.SMA(closes, 5),
                ["MA8"]        = ind.MA.SMA(closes, 8),
                ["MA13"]       = ind.MA.SMA(closes, 13),
                ["MA21"]       = ind.MA.SMA(closes, 21),
                ["MA34"]       = ind.MA.SMA(closes, 34),
                ["MA55"]       = ind.MA.SMA(closes, 55),
                ["MA86"]       = ind.MA.SMA(closes, 86),
                ["RSI"]        = rsi.Values,
                ["MACD"]       = macd.MACD,
                ["MACDSignal"] = macd.Signal,
                ["SuperTrend"] = st.SuperTrend,
                ["Close"]      = closes,
            };
        }

        // =========================================================================
        // 2) KURAL KATALOGU - TODO: kendi AL/SAT kurallarini burada listele
        //    ruleIndex bu listedeki elemanlardan birini secer
        // =========================================================================
        private static List<(List<Comparison> Buy, List<Comparison> Sell)> BuildRuleCatalog()
        {
            return new List<(List<Comparison>, List<Comparison>)>
            {
                // ruleIndex = 0: Golden/Death cross (MA5 x MA8)
                (
                    new List<Comparison> { new(new SeriesRef("MA5"), CompareOp.GT, new SeriesRef("MA8")) },
                    new List<Comparison> { new(new SeriesRef("MA5"), CompareOp.LT, new SeriesRef("MA8")) }
                ),

                // ruleIndex = 1: MA5 > MA8 > MA13 siralamasi + RSI'de yukselen momentum (Rsi[-3] > Rsi[-1])
                (
                    new List<Comparison>
                    {
                        new(new SeriesRef("MA5"), CompareOp.GT, new SeriesRef("MA8")),
                        new(new SeriesRef("MA8"), CompareOp.GT, new SeriesRef("MA13")),
                        new(new SeriesRef("RSI", 3), CompareOp.GT, new SeriesRef("RSI", 1)),
                    },
                    new List<Comparison>
                    {
                        new(new SeriesRef("MA5"), CompareOp.LT, new SeriesRef("MA8")),
                        new(new SeriesRef("MA8"), CompareOp.LT, new SeriesRef("MA13")),
                        new(new SeriesRef("RSI", 3), CompareOp.LT, new SeriesRef("RSI", 1)),
                    }
                ),

                // ruleIndex = 2: MACD sinyal cizgisini kesiyor + SuperTrend fiyatin altinda/ustunde
                (
                    new List<Comparison>
                    {
                        new(new SeriesRef("MACD"), CompareOp.GT, new SeriesRef("MACDSignal")),
                        new(new SeriesRef("SuperTrend"), CompareOp.LT, new SeriesRef("Close")),
                    },
                    new List<Comparison>
                    {
                        new(new SeriesRef("MACD"), CompareOp.LT, new SeriesRef("MACDSignal")),
                        new(new SeriesRef("SuperTrend"), CompareOp.GT, new SeriesRef("Close")),
                    }
                ),
            };
        }

        // =========================================================================
        // 3) EVALUATOR - cross-series ve ayni-seri-farkli-lag karsilastirmalari
        //    ayni yoldan gecer, ozel durum yok
        // =========================================================================
        private bool EvaluateRule(List<Comparison> rule, int currentIndex)
        {
            foreach (var c in rule)
            {
                int li = currentIndex - c.Left.Lag;
                int ri = currentIndex - c.Right.Lag;
                if (li < 0 || ri < 0)
                    return false;

                double left  = _series![c.Left.SeriesName][li];
                double right = _series![c.Right.SeriesName][ri];
                if (double.IsNaN(left) || double.IsNaN(right))
                    return false;

                bool ok = c.Op switch
                {
                    CompareOp.GT  => left > right,
                    CompareOp.LT  => left < right,
                    CompareOp.GTE => left >= right,
                    CompareOp.LTE => left <= right,
                    _ => false
                };

                if (!ok)
                    return false;
            }

            return true;
        }

        public override TradeSignals OnStep(int currentIndex)
        {
            if (_series == null || _buyRule == null || _sellRule == null)
                return TradeSignals.None;

            if (EvaluateRule(_buyRule, currentIndex))
                return TradeSignals.Buy;

            if (EvaluateRule(_sellRule, currentIndex))
                return TradeSignals.Sell;

            return TradeSignals.None;
        }

        public override Dictionary<string, double[]>? GetPlotIndicators() => _series;
    }
}
