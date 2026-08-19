using AlgoTrade.Core.Trading;

namespace AlgoTrade.Core.Trading.Core;

/// <summary>
/// Sanal pozisyon beklerken çakışan (ters yönlü) bir ham sinyal geldiğinde ne yapılacağını belirler.
/// </summary>
public enum SignalConflictMode
{
    /// <summary>
    /// Çakışan sinyal, bekleyen sanal pozisyonu iptal edip yeni yönde sıfırdan takip başlatır.
    /// Eski projenin (AlgoTradeWithOptimizationSupport) ConfirmingSingleTrader davranışıyla aynı.
    /// </summary>
    CancelAndRestart = 0,

    /// <summary>
    /// Çakışan sinyal tamamen görmezden gelinir — sanal pozisyon orijinal yönünde, orijinal
    /// giriş fiyatında, kendi eşiğine ulaşana kadar değişmeden beklemeye devam eder.
    /// </summary>
    LockAndIgnore = 1
}

/// <summary>
/// Bir ham Al/Sat/Flat sinyal kaynağını (tek bir stratejinin kendisi veya bir consensus/composite
/// sinyal — kaynağın kendisi önemli değil) gerçek pozisyona çevirmeden önce sanal (paper) bir
/// pozisyonla "konfirme" eden paylaşılan state machine. <see cref="ConfirmingSingleTrader"/> ve
/// <see cref="ConfirmingMultipleTrader"/> arasında ORTAK — mantık ikisinde de birebir aynı, sadece
/// "ham sinyal kaynağı" farklı (biri tek bir SingleTrader'ın stratejisi, diğeri bir MultipleTrader'ın
/// consensus'u). Kod tekrarını (ve onunla gelen çift bakım/çift hata riskini) önlemek için buraya
/// çıkarıldı — bkz. docs/todo.md, "Getiri Eğrisi / KarZarar Eğrisi Konfirmasyonu (Madde 3)".
///
/// Kullanım: her bar'da <see cref="Resolve"/> çağrılır — kaynağın o barki yönü ("A"/"S"/"F"),
/// ham komutu (TradeSignals) ve o barki fiyat verilir; mainTrader'a gönderilecek sinyal döner.
/// </summary>
public class VirtualPositionConfirmer
{
    #region Config

    /// <summary>false: Değer bazlı eşik (puan/fiyat farkı), true: Yüzde bazlı.</summary>
    public bool ThresholdIsPercentage { get; set; } = false;

    /// <summary>Sanal pozisyonun konfirme sayılması için ulaşması gereken kâr eşiği (trend teyidi).</summary>
    public double ProfitThreshold { get; set; } = 5000.0;

    /// <summary>Sanal pozisyonun konfirme sayılması için ulaşması gereken zarar eşiği (dip teyidi). NEGATİF girilmeli.</summary>
    public double LossThreshold { get; set; } = -3000.0;

    /// <summary>Hangi eşik(ler) konfirmasyonu tetikleyebilir.</summary>
    public ConfirmationTrigger Trigger { get; set; } = ConfirmationTrigger.Both;

    /// <summary>Sanal pozisyon beklerken çakışan (ters yönlü) bir ham sinyal geldiğinde izlenecek davranış.</summary>
    public SignalConflictMode ConflictMode { get; set; } = SignalConflictMode.CancelAndRestart;

    /// <summary>
    /// true: Flat ham sinyali bekleyen sanal pozisyonu anında iptal eder (eski proje davranışı).
    /// false: Flat görmezden gelinir — sanal pozisyon kendi eşiğine ulaşana kadar beklemeye devam eder.
    /// </summary>
    public bool FlattenImmediatelyOnFlatSignal { get; set; } = true;

    #endregion

    #region State

    private string? _virtualYon;          // "A" / "S" / null (bekleyen sanal pozisyon yok)
    private double _virtualEntryPrice;
    private bool _confirmed;

    /// <summary>Sanal pozisyonun o anki yönü ("A"/"S"), bekleyen sanal pozisyon yoksa null.</summary>
    public string? VirtualYon => _virtualYon;
    public double VirtualEntryPrice => _virtualEntryPrice;
    public bool IsConfirmed => _confirmed;

    #endregion

    public void Reset()
    {
        _virtualYon = null;
        _virtualEntryPrice = 0.0;
        _confirmed = false;
    }

    /// <summary>
    /// Sanal pozisyon state'ini günceller ve mainTrader'a bu bar'da gönderilecek sinyali döner.
    /// </summary>
    /// <param name="currentYon">Ham sinyal kaynağının o barki yönü: "A" / "S" / "F".</param>
    /// <param name="rawSignal">Ham sinyal kaynağının o barki komutu (konfirme SONRASI olduğu gibi geçirilir).</param>
    /// <param name="currentPrice">O barın fiyatı (sanal pozisyon giriş/K-Z hesabı için).</param>
    public TradeSignals Resolve(string currentYon, TradeSignals rawSignal, double currentPrice)
    {
        // ── Zaten konfirme: bu katman artık devrede değil, ham sinyal olduğu gibi geçer ──
        if (_confirmed)
        {
            if (currentYon == "F")
            {
                _confirmed = false;
                _virtualYon = null;
            }

            return rawSignal;
        }

        // ── Henüz konfirme değil ──
        if (currentYon == "F")
        {
            if (_virtualYon != null && !FlattenImmediatelyOnFlatSignal)
            {
                // Flat görmezden gelinir - sanal pozisyon değişmeden bekler
                return TradeSignals.None;
            }

            _virtualYon = null;
            return TradeSignals.None;
        }

        // currentYon == "A" veya "S"
        if (_virtualYon == null)
        {
            // Yeni sanal pozisyon başlat
            _virtualYon = currentYon;
            _virtualEntryPrice = currentPrice;
            return TradeSignals.None;
        }

        if (_virtualYon != currentYon)
        {
            // Çakışan yön değişikliği (Al<->Sat)
            if (ConflictMode == SignalConflictMode.CancelAndRestart)
            {
                _virtualYon = currentYon;
                _virtualEntryPrice = currentPrice;
            }
            // LockAndIgnore: hiçbir şey değişmez, sanal pozisyon orijinal yönünde beklemeye devam eder

            return TradeSignals.None;
        }

        // Aynı yönde bekliyor - eşik kontrolü
        if (IsThresholdReached(_virtualYon, _virtualEntryPrice, currentPrice))
        {
            _confirmed = true;

            // rawSignal DEĞİL — sanal pozisyon genelde birkaç bar önce (ilk sinyal geldiğinde)
            // açıldığı için kaynağın konfirme anındaki ham komutu genelde "None"/tekrar eden bir
            // değer oluyor (kaynak yeni bir emir tekrar yayınlamıyor). Gerçek girişi biz burada,
            // sanal pozisyonun yönüne göre kendimiz üretiyoruz — konfirme anının kendisi giriş
            // komutudur. (Gerçek veride bulunmuş bir hatanın düzeltmesi — bkz. docs/todo.md.)
            return _virtualYon == "A" ? TradeSignals.Buy : TradeSignals.Sell;
        }

        return TradeSignals.None;
    }

    private bool IsThresholdReached(string yon, double entryPrice, double currentPrice)
    {
        double karZararFiyat = yon == "A"
            ? currentPrice - entryPrice
            : entryPrice - currentPrice;

        double karZararYuzde = entryPrice != 0.0
            ? 100.0 * karZararFiyat / entryPrice
            : 0.0;

        double karZarar = ThresholdIsPercentage ? karZararYuzde : karZararFiyat;

        bool karTetiklendi = karZarar >= ProfitThreshold;
        bool zararTetiklendi = karZarar <= LossThreshold;

        return Trigger switch
        {
            ConfirmationTrigger.ProfitOnly => karTetiklendi,
            ConfirmationTrigger.LossOnly => zararTetiklendi,
            _ => karTetiklendi || zararTetiklendi
        };
    }
}
