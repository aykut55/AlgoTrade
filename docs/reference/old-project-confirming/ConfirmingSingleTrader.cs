// =============================================================================
// REFERANS KOPYA — AKTİF KOD DEĞİL
// =============================================================================
// Kaynak: D:\Aykut\Projects\AlgoTradeWithOptimizationSupport\
//         AlgoTradeWithOptimizationSupportWinFormsApp\AlgoTradeWithOptimizationSupportWinFormsApp\
//         src\Trading\Traders\ConfirmingSingleTrader.cs
// Kopyalanma tarihi: 2026-08-18
// Amaç: Yeni projede yazılacak ConfirmingSingleTrader/ConfirmingMultipleTrader/
//       ConfirmingSingleTraderOptimizer/ConfirmingMultipleTraderOptimizer sınıfları için
//       implementasyon referansı — eski projedeki TEK ÇALIŞAN "sanal pozisyon konfirmasyonu"
//       mekanizması budur (bkz. docs/todo.md, "Getiri Eğrisi / KarZarar Eğrisi Konfirmasyonu"
//       bölümü). Diğer projede ("ConfirmingSingleTrader"/"ConfirmingMultipleTrader" tab'ları)
//       bu sınıfı KULLANMAYAN, ölü koda bağlı iki sekme daha vardı — onlar kopyalanmadı, bu
//       dosya gerçek/işleyen tek implementasyon.
// Namespace/tip isimleri (StockData, IndicatorManager, SingleTrader, TradeSignals,
// ConfirmationTrigger, IAlgoTraderLogger) eski projeye ait, yeni projedeki karşılıklarıyla
// birebir aynı olmayabilir — doğrudan derlenmek üzere değil, okunmak/anlaşılmak üzere buraya
// kondu.
// =============================================================================

using AlgoTradeWithOptimizationSupportWinFormsApp.Definitions;
using AlgoTradeWithOptimizationSupportWinFormsApp.Indicators;
using AlgoTradeWithOptimizationSupportWinFormsApp.Trading;
using AlgoTradeWithOptimizationSupportWinFormsApp.Trading.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AlgoTradeWithOptimizationSupportWinFormsApp.Trading.Traders
{
    /// <summary>
    /// ConfirmingSingleTrader - MultipleTrader benzeri yapı
    /// Farkı: buildConsensusSignal içinde eşik kontrolü yapar
    /// Trader'ların kar/zararı belirli eşiğe ulaşmadıysa sinyalleri YOK sayılır
    /// </summary>
    public class ConfirmingSingleTrader
    {
        #region Properties

        public int Id { get; private set; }
        public List<StockData> Data { get; private set; }
        public IndicatorManager Indicators { get; private set; }
        public IAlgoTraderLogger? Logger { get; private set; }

        public List<SingleTrader> Traders { get; private set; }
        public bool IsInitialized { get; private set; }
        public int CurrentIndex { get; private set; }

        // State flags
        public bool IsStarted { get; internal set; }
        public bool IsRunning { get; internal set; }
        public bool IsStopped { get; internal set; }
        public bool IsStopRequested { get; internal set; }

        private SingleTrader _mainTrader;

        /// <summary>
        /// Dinamik pozisyon büyüklüğü desteği
        /// </summary>
        public bool DynamicPositionSizeEnabled { get; set; } = false;

        public Action<ConfirmingSingleTrader, int, int>? OnProgress { get; set; }

        // ═══════════════════════════════════════════════════════════════════════
        // CONFIRMATION MODE SETTINGS
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Eşik tipi - Değer mi Yüzde mi?
        /// false: Değer bazlı (örn: 5000 puan kar, -3000 puan zarar)
        /// true: Yüzde bazlı (örn: %4 kar, %-2.5 zarar)
        /// </summary>
        public bool EsikTipiYuzde { get; set; } = false;

        /// <summary>
        /// Kar eşiği (Değer veya Yüzde - EsikTipiYuzde'ye göre)
        /// Örnek Değer: 5000 (5000 puan kar)
        /// Örnek Yüzde: 4.0 (%4 kar)
        /// </summary>
        public double KarEsigi { get; set; } = 5000.0;

        /// <summary>
        /// Zarar eşiği (Değer veya Yüzde - EsikTipiYuzde'ye göre)
        /// NEGATİF değer girilmeli!
        /// Örnek Değer: -3000 (-3000 puan zarar)
        /// Örnek Yüzde: -2.5 (%-2.5 zarar)
        /// </summary>
        public double ZararEsigi { get; set; } = -3000.0;

        /// <summary>
        /// Tetikleyici modu
        /// Both: Kar veya Zarar eşiğinden biri tetiklenirse sinyal geçerli
        /// KarOnly: Sadece kar eşiği tetiklenirse sinyal geçerli
        /// ZararOnly: Sadece zarar eşiği tetiklenirse sinyal geçerli
        /// </summary>
        public ConfirmationTrigger Tetikleyici { get; set; } = ConfirmationTrigger.Both;

        // ═══════════════════════════════════════════════════════════════════════
        // CONFIRMATION TRACKING - Her trader için yön ve onay durumu takibi
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Her trader'ın önceki yönünü tutar (A/S/F)
        /// Key: Trader.Id, Value: Önceki yön
        /// </summary>
        private Dictionary<int, string> _traderPreviousYon = new Dictionary<int, string>();

        /// <summary>
        /// Her trader için eşik onay durumu
        /// Key: Trader.Id, Value: Eşik geçildi ve onaylandı mı?
        /// </summary>
        private Dictionary<int, bool> _traderConfirmed = new Dictionary<int, bool>();

        #endregion

        #region Constructor

        public ConfirmingSingleTrader()
        {
            Traders = new List<SingleTrader>();
            IsInitialized = false;
        }

        /// <summary>
        /// Parametreli constructor - mainTrader ile birlikte oluşturulur
        /// </summary>
        public ConfirmingSingleTrader(int id, List<StockData> data, IndicatorManager indicators, IAlgoTraderLogger? logger)
        {
            Id = id;
            Data = data;
            Indicators = indicators;
            Logger = logger;

            Traders = new List<SingleTrader>();

            // Create mainTrader with ID = -1 to distinguish from other traders
            _mainTrader = new SingleTrader(-1, "mainTrader", data, indicators, logger);

            IsInitialized = true;
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Initialize with market data
        /// </summary>
        public void Initialize(List<StockData> data)
        {
            if (data == null || data.Count == 0)
                throw new ArgumentException("Data cannot be null or empty");

            Data = data;
            CurrentIndex = 0;
            IsInitialized = true;
        }

        /// <summary>
        /// Add a trader
        /// </summary>
        public void AddTrader(SingleTrader trader)
        {
            if (!IsInitialized)
                throw new InvalidOperationException("ConfirmingSingleTrader not initialized");

            Traders.Add(trader);

            // Initialize trader with same data
            if (!trader.IsInitialized)
            {
                trader.SetData(Data);
            }
        }

        /// <summary>
        /// Reset all traders
        /// </summary>
        public void Reset()
        {
            CurrentIndex = 0;
            foreach (var trader in Traders)
            {

            }

            // Clear confirmation tracking dictionaries
            _traderPreviousYon.Clear();
            _traderConfirmed.Clear();

            // Reset state flags
            IsStarted = false;
            IsRunning = false;
            IsStopped = false;
            IsStopRequested = false;
        }

        public void Init()
        {
            CurrentIndex = 0;
            foreach (var trader in Traders)
            {

            }
        }

        public void Initialize()
        {
            CurrentIndex = 0;
            foreach (var trader in Traders)
            {
                trader.Initialize();
            }
        }

        /// <summary>
        /// Consensus signal oluşturur - EŞİK KONTROLÜ ile
        /// Trader'ların kar/zararı eşiğe ulaşmadıysa sinyalleri YOK sayılır
        /// Bir kez onaylandıktan sonra yön değişene kadar onaylı kalır
        /// </summary>
        public TradeSignals buildConsensusSignal(ref double varlikAdedSayisiFinal)
        {
            TradeSignals consensusSignal = TradeSignals.None;

            double sonYonACount = 0;
            double sonYonSCount = 0;
            double sonYonFCount = 0;

            foreach (var trader in Traders)
            {
                double varlikAdedSayisi = 0.0;
                var isSonYonA = trader.is_son_yon_a();
                var isSonYonS = trader.is_son_yon_s();
                var isSonYonF = trader.is_son_yon_f();

                // ═══════════════════════════════════════════════════════════════════════
                // MEVCUT YÖN BELİRLE
                // ═══════════════════════════════════════════════════════════════════════
                string currentYon = "F";
                if (isSonYonA) currentYon = "A";
                else if (isSonYonS) currentYon = "S";

                // ═══════════════════════════════════════════════════════════════════════
                // ÖNCEKİ YÖN İLE KARŞILAŞTIR - YÖN DEĞİŞTİ Mİ?
                // ═══════════════════════════════════════════════════════════════════════
                string previousYon = "F";
                if (_traderPreviousYon.ContainsKey(trader.Id))
                    previousYon = _traderPreviousYon[trader.Id];

                bool yonDegisti = (currentYon != previousYon);

                // Yön değiştiyse confirmed durumunu sıfırla
                if (yonDegisti)
                {
                    _traderConfirmed[trader.Id] = false;
                }

                // Önceki yönü güncelle
                _traderPreviousYon[trader.Id] = currentYon;

                // ═══════════════════════════════════════════════════════════════════════
                // CONFIRMED DURUMUNU KONTROL ET
                // ═══════════════════════════════════════════════════════════════════════
                bool isConfirmed = false;
                if (_traderConfirmed.ContainsKey(trader.Id))
                    isConfirmed = _traderConfirmed[trader.Id];

                // ═══════════════════════════════════════════════════════════════════════
                // FLAT İSE - Direkt geç, confirmed = false
                // ═══════════════════════════════════════════════════════════════════════
                if (isSonYonF)
                {
                    _traderConfirmed[trader.Id] = false;
                    // Flat sinyali direkt gönderilir
                }
                // ═══════════════════════════════════════════════════════════════════════
                // LONG veya SHORT - Eşik kontrolü yap (sadece confirmed değilse)
                // ═══════════════════════════════════════════════════════════════════════
                else if (isSonYonA || isSonYonS)
                {
                    if (isConfirmed)
                    {
                        // Zaten onaylı - eşik kontrolü YAPMA, sinyal devam etsin
                        // isSonYonA veya isSonYonS olduğu gibi kalır
                    }
                    else
                    {
                        // Henüz onaylı değil - eşik kontrolü YAP
                        double karZararFiyat = trader.status.KarZararFiyat;
                        double karZararFiyatYuzde = trader.status.KarZararFiyatYuzde;

                        bool karTetiklendi = false;
                        bool zararTetiklendi = false;

                        if (EsikTipiYuzde)
                        {
                            // YÜZDE BAZLI eşik kontrolü
                            karTetiklendi = karZararFiyatYuzde >= KarEsigi;
                            zararTetiklendi = karZararFiyatYuzde <= ZararEsigi;
                        }
                        else
                        {
                            // DEĞER BAZLI eşik kontrolü
                            karTetiklendi = karZararFiyat >= KarEsigi;
                            zararTetiklendi = karZararFiyat <= ZararEsigi;
                        }

                        // Tetikleyici moduna göre eşik geçildi mi?
                        bool esikGecildi = false;
                        switch (Tetikleyici)
                        {
                            case ConfirmationTrigger.Both:
                                esikGecildi = karTetiklendi || zararTetiklendi;
                                break;
                            case ConfirmationTrigger.KarOnly:
                                esikGecildi = karTetiklendi;
                                break;
                            case ConfirmationTrigger.ZararOnly:
                                esikGecildi = zararTetiklendi;
                                break;
                        }

                        if (esikGecildi)
                        {
                            // Eşik geçildi - ONAYLA
                            _traderConfirmed[trader.Id] = true;
                            // isSonYonA veya isSonYonS olduğu gibi kalır - sinyal gönderilecek
                        }
                        else
                        {
                            // Eşik geçilmedi - FLAT olarak katkı yap
                            // Yeni yön onaylanmadı, eski yönde de kalamayız → FLAT
                            isSonYonA = false;
                            isSonYonS = false;
                            isSonYonF = true;  // FLAT olarak consensus'a katıl
                        }
                    }
                }

                // ═══════════════════════════════════════════════════════════════════════
                // Consensus hesaplama (eşik kontrolü sonrası)
                // ═══════════════════════════════════════════════════════════════════════

                varlikAdedSayisi = trader.pozisyonBuyuklugu.VarlikAdedSayisi;
                if (trader.pozisyonBuyuklugu.MicroLotSizeEnabled)
                    varlikAdedSayisi = trader.pozisyonBuyuklugu.VarlikAdedSayisiMicro;

                if (isSonYonA)
                    sonYonACount += varlikAdedSayisi;

                if (isSonYonS)
                    sonYonSCount += varlikAdedSayisi;

                if (isSonYonF)
                    sonYonFCount += varlikAdedSayisi;
            }

            double sonYonCountFinal = (sonYonACount * 1) + (sonYonSCount * -1) + (sonYonFCount * 0);

            if (sonYonCountFinal > 0.0)
            {
                consensusSignal = TradeSignals.Buy;
                varlikAdedSayisiFinal = Math.Abs(sonYonCountFinal);
            }
            else if (sonYonCountFinal < 0.0)
            {
                consensusSignal = TradeSignals.Sell;
                varlikAdedSayisiFinal = Math.Abs(sonYonCountFinal);
            }
            else
            {
                consensusSignal = TradeSignals.Flat;
                varlikAdedSayisiFinal = Math.Abs(sonYonCountFinal);
            }

            return consensusSignal;
        }

        public void Run(int i)
        {
            int noneSignalCount = 0;
            int alSignalCount = 0;
            int satSignalCount = 0;
            int flatOlSignalCount = 0;
            int passGecSignalCount = 0;
            int karAlSignalCount = 0;
            int zararKesSignalCount = 0;

            // -----------------------------------------------------------
            foreach (var trader in Traders)
            {
                trader.Run(i);

                TradeSignals signal = trader.StrategySignal;

                if (signal == TradeSignals.None)
                {
                    noneSignalCount++;
                }

                if (signal == TradeSignals.Buy)
                {
                    alSignalCount++;
                }

                if (signal == TradeSignals.Sell)
                {
                    satSignalCount++;
                }

                if (signal == TradeSignals.TakeProfit)
                {
                    karAlSignalCount++;
                }

                if (signal == TradeSignals.StopLoss)
                {
                    zararKesSignalCount++;
                }

                if (signal == TradeSignals.Flat)
                {
                    flatOlSignalCount++;
                }

                if (signal == TradeSignals.Skip)
                {
                    passGecSignalCount++;
                }
            }

            // -------------- Consensus Logic ---------------
            var isSonYonA = _mainTrader.is_son_yon_a();
            var isSonYonS = _mainTrader.is_son_yon_s();
            var isSonYonF = _mainTrader.is_son_yon_f();

            double varlikAdedSayisiFinal = 0.0;
            TradeSignals consensusSignal = buildConsensusSignal(ref varlikAdedSayisiFinal);

            if (this.DynamicPositionSizeEnabled)
            {
                if (_mainTrader.pozisyonBuyuklugu.MicroLotSizeEnabled)
                    _mainTrader.pozisyonBuyuklugu.VarlikAdedSayisiMicro = varlikAdedSayisiFinal;
                else
                    _mainTrader.pozisyonBuyuklugu.VarlikAdedSayisi = varlikAdedSayisiFinal;
            }

            // -----------------------------------------------------------
            _mainTrader.OnRun?.Invoke(_mainTrader, 0);

            _mainTrader.emirleri_resetle(i);

            _mainTrader.emir_oncesi_dongu_foksiyonlarini_calistir(i);

            if (i < 1)
                return;

            // Set consensus signal to mainTrader
            _mainTrader.StrategySignal = consensusSignal;

            _mainTrader.emirleri_setle(i, _mainTrader.StrategySignal);

            int filterMode = 3;
            bool isTradeEnabled = false;
            bool isPozKapatEnabled = false;
            int checkResult = 0;
            _mainTrader.islem_zaman_filtresi_uygula(i, filterMode, ref isTradeEnabled, ref isPozKapatEnabled, ref checkResult);

            _mainTrader.emir_sonrasi_dongu_foksiyonlarini_calistir(i);

            _mainTrader.OnRun?.Invoke(_mainTrader, 1);
        }

        public void Finalize(bool saveStatisticsToFile = true)
        {
            CurrentIndex = 0;
            foreach (var trader in Traders)
            {
                trader.Finalize(false);
            }

            if (!IsInitialized)
                throw new InvalidOperationException("Trader not initialized");

            _mainTrader.OnFinal?.Invoke(_mainTrader, 0);

            _mainTrader.istatistikleri_hesapla();

            if (saveStatisticsToFile)
                WriteConfirmingSingleTraderListsToFiles();

            if (saveStatisticsToFile)
                _mainTrader.istatistikleri_dosyaya_yaz();

            _mainTrader.OnFinal?.Invoke(_mainTrader, 1);
        }

        #endregion

        #region ConfirmingSingleTrader Lists Export

        private void WriteConfirmingSingleTraderListsToFiles(bool saveListsToFileTxt = true, bool saveListsToFileCsv = true)
        {
            if (saveListsToFileTxt)
                WriteConfirmingSingleTraderListsToTxt();
            if (saveListsToFileCsv)
                WriteConfirmingSingleTraderListsToCsv();
        }

        private void WriteConfirmingSingleTraderListsToTxt()
        {
            if (_mainTrader == null || Data == null || Data.Count == 0)
                return;

            var logDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            if (!System.IO.Directory.Exists(logDir))
                System.IO.Directory.CreateDirectory(logDir);

            var filePath = System.IO.Path.Combine(logDir, "ConfirmingSingleTraderLists.txt");

            using (var writer = new System.IO.StreamWriter(filePath, append: false, System.Text.Encoding.UTF8))
            {
                writer.WriteLine($"CONFIRMING SINGLE TRADER BAR-BY-BAR DATA");
                writer.WriteLine($"Generated: {DateTime.Now:yyyy.MM.dd HH:mm:ss}");
                writer.WriteLine($"Esik Tipi: {(EsikTipiYuzde ? "Yuzde" : "Deger")}");
                writer.WriteLine($"Kar Esigi: {KarEsigi}, Zarar Esigi: {ZararEsigi}");
                writer.WriteLine($"Tetikleyici: {Tetikleyici}");
                writer.WriteLine("".PadRight(300, '='));

                WriteHeaderTxt(writer);

                for (int i = 0; i < Data.Count; i++)
                {
                    WriteBarDataTxt(writer, i);
                }

                writer.WriteLine("".PadRight(300, '='));
            }

            Logger?.Log($"ConfirmingSingleTraderLists.txt written to: {filePath}");
        }

        private void WriteHeaderTxt(System.IO.StreamWriter writer)
        {
            var header = $"{"BarNo",7} | " +
                        $"{"Date",10} | " +
                        $"{"Time",8} | " +
                        $"{"Open",10} | " +
                        $"{"High",10} | " +
                        $"{"Low",10} | " +
                        $"{"Close",10} | " +
                        $"{"Volume",10}";

            header += $" | {"MainYon",7} | {"MainSvy",10} | {"MainSny",7}";

            for (int t = 0; t < Traders.Count; t++)
            {
                header += $" | {$"T{t}Yon",7} | {$"T{t}Svy",10} | {$"T{t}Sny",7}";
            }

            writer.WriteLine(header);
        }

        private void WriteBarDataTxt(System.IO.StreamWriter writer, int barIndex)
        {
            var bar = Data[barIndex];

            var line = $"{barIndex,7} | " +
                      $"{bar.Date:yyyy.MM.dd} | " +
                      $"{bar.DateTime:HH:mm:ss} | " +
                      $"{bar.Open,10:F2} | " +
                      $"{bar.High,10:F2} | " +
                      $"{bar.Low,10:F2} | " +
                      $"{bar.Close,10:F2} | " +
                      $"{bar.Volume,10:F0}";

            line += $" | {GetYon(_mainTrader, barIndex),7} | " +
                   $"{GetSeviye(_mainTrader, barIndex),10:F2} | " +
                   $"{GetSinyal(_mainTrader, barIndex),7:F2}";

            foreach (var trader in Traders)
            {
                line += $" | {GetYon(trader, barIndex),7} | " +
                       $"{GetSeviye(trader, barIndex),10:F2} | " +
                       $"{GetSinyal(trader, barIndex),7:F2}";
            }

            writer.WriteLine(line);
        }

        private void WriteConfirmingSingleTraderListsToCsv()
        {
            if (_mainTrader == null || Data == null || Data.Count == 0)
                return;

            var logDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            if (!System.IO.Directory.Exists(logDir))
                System.IO.Directory.CreateDirectory(logDir);

            var filePath = System.IO.Path.Combine(logDir, "ConfirmingSingleTraderLists.csv");

            using (var writer = new System.IO.StreamWriter(filePath, append: false, System.Text.Encoding.UTF8))
            {
                WriteHeaderCsv(writer);

                for (int i = 0; i < Data.Count; i++)
                {
                    WriteBarDataCsv(writer, i);
                }
            }

            Logger?.Log($"ConfirmingSingleTraderLists.csv written to: {filePath}");
        }

        private void WriteHeaderCsv(System.IO.StreamWriter writer)
        {
            var header = "BarNo;Date;Time;Open;High;Low;Close;Volume";
            header += ";MainTrader_Yon;MainTrader_Seviye;MainTrader_Sinyal";

            for (int t = 0; t < Traders.Count; t++)
            {
                header += $";Trader{t}_Yon;Trader{t}_Seviye;Trader{t}_Sinyal";
            }

            writer.WriteLine(header);
        }

        private void WriteBarDataCsv(System.IO.StreamWriter writer, int barIndex)
        {
            var bar = Data[barIndex];

            var line = $"{barIndex};" +
                      $"{bar.Date:yyyy.MM.dd};" +
                      $"{bar.DateTime:HH:mm:ss};" +
                      $"{bar.Open:F2};" +
                      $"{bar.High:F2};" +
                      $"{bar.Low:F2};" +
                      $"{bar.Close:F2};" +
                      $"{bar.Volume:F0}";

            line += $";{GetYon(_mainTrader, barIndex)};" +
                   $"{GetSeviye(_mainTrader, barIndex):F2};" +
                   $"{GetSinyal(_mainTrader, barIndex):F2}";

            foreach (var trader in Traders)
            {
                line += $";{GetYon(trader, barIndex)};" +
                       $"{GetSeviye(trader, barIndex):F2};" +
                       $"{GetSinyal(trader, barIndex):F2}";
            }

            writer.WriteLine(line);
        }

        private string GetYon(SingleTrader trader, int barIndex)
        {
            if (trader == null || trader.lists == null || trader.lists.YonList == null)
                return "";

            if (barIndex < 0 || barIndex >= trader.lists.YonList.Count)
                return "";

            return trader.lists.YonList[barIndex] ?? "";
        }

        private double GetSeviye(SingleTrader trader, int barIndex)
        {
            if (trader == null || trader.lists == null || trader.lists.SeviyeList == null)
                return 0.0;

            if (barIndex < 0 || barIndex >= trader.lists.SeviyeList.Count)
                return 0.0;

            return trader.lists.SeviyeList[barIndex];
        }

        private double GetSinyal(SingleTrader trader, int barIndex)
        {
            if (trader == null || trader.lists == null || trader.lists.SinyalList == null)
                return 0.0;

            if (barIndex < 0 || barIndex >= trader.lists.SinyalList.Count)
                return 0.0;

            return trader.lists.SinyalList[barIndex];
        }

        #endregion

        #region Trading Methods

        public void Step()
        {
            if (!IsInitialized)
                throw new InvalidOperationException("ConfirmingSingleTrader not initialized");

            if (CurrentIndex >= Data.Count)
                return;

            foreach (var trader in Traders)
            {
                trader.Step();
            }

            CurrentIndex++;
        }

        public void Run()
        {
            if (!IsInitialized)
                throw new InvalidOperationException("ConfirmingSingleTrader not initialized");

            Reset();

            foreach (var trader in Traders)
            {
                trader.Run(0);
            }
        }

        #endregion

        #region Statistics Methods

        public Dictionary<string, string> GetAllStatistics()
        {
            var stats = new Dictionary<string, string>();

            for (int i = 0; i < Traders.Count; i++)
            {
                stats[$"Trader_{i}"] = Traders[i].GetStatisticsSummary();
            }

            return stats;
        }

        public SingleTrader GetBestTrader()
        {
            if (Traders.Count == 0)
                return null;

            return null;
        }

        #endregion

        #region Main Trader Methods

        public SingleTrader GetMainTrader()
        {
            return _mainTrader;
        }

        public void SetCallbacks(
            Action<SingleTrader, int>? onReset = null,
            Action<SingleTrader, int>? onInit = null,
            Action<SingleTrader, int>? onRun = null,
            Action<SingleTrader, int>? onFinal = null,
            Action<SingleTrader, int>? onBeforeOrders = null,
            Action<SingleTrader, string, int>? onNotifySignal = null,
            Action<SingleTrader, int>? onAfterOrders = null,
            Action<SingleTrader, int, int>? onProgress = null,
            Action<SingleTrader>? onApplyUserFlags = null)
        {
            _mainTrader.SetCallbacks(onReset, onInit, onRun, onFinal, onBeforeOrders, onNotifySignal, onAfterOrders, onProgress, onApplyUserFlags);

            foreach (var trader in Traders)
            {
                trader.SetCallbacks(onReset, onInit, onRun, onFinal, onBeforeOrders, onNotifySignal, onAfterOrders, onProgress, onApplyUserFlags);
            }
        }

        public void Stop()
        {
            if (IsRunning)
            {
                IsStopRequested = true;
                Logger?.Log($"Stop requested for ConfirmingSingleTrader (Id: {Id})");
            }
        }

        public void Dispose()
        {
            _mainTrader?.Dispose();
            _mainTrader = null;

            foreach (var trader in Traders)
            {
                trader?.Dispose();
            }
            Traders.Clear();
        }

        #endregion
    }
}
