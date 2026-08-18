<!--
REFERANS KOPYA — bu belge AlgoTrade projesinin bir parçası değil.
Kaynak: D:\Aykut\Projects\AlgoTradeWithOptimizationSupport\ConfirmationMode_Implementation_Plan.md
Kopyalanma tarihi: 2026-08-18
Amaç: Eski projedeki "Confirmation Mode" (sanal pozisyon → konfirmasyon → gerçek pozisyon)
özelliğinin ORİJİNAL tasarım niyetini gösteren plan belgesi. Bu plandaki SingleTrader
değişiklikleri (ProcessConfirmationMode/UpdateSanalPozisyon/CheckConfirmation) eski projede
YAZILDI ama Run()'daki çağrı noktası yorum satırında bırakıldığı için hiç devreye girmedi (ölü
kod) — gerçek/çalışan implementasyon bunun yerine ConfirmingSingleTrader.cs'teki
buildConsensusSignal() yaklaşımıydı (bkz. bu klasördeki ConfirmingSingleTrader.cs). Yine de bu
plan, orijinal niyeti (özellikle "kar/zarar konfirmasyonu" terminolojisini ve
ConfirmationTrigger enum'unun yorum satırlarını) net gösterdiği için referans olarak saklandı.
Detaylı analiz ve yeni projeye uyarlanmış karar için bkz. docs/todo.md, "Getiri Eğrisi / KarZarar
Eğrisi Konfirmasyonu" bölümü.
-->

# Confirmation Mode Implementation Plan

## 1. Konsept Ozeti

### Problem
Normal trading'de strateji sinyali geldiginde hemen pozisyon aciliyor. Ancak bazen sinyal yanlis yonde olabiliyor veya zamanlama hatali olabiliyor.

### Cozum: Shadow/Confirmation Trading
Strateji sinyallerini once "sanal" (shadow) olarak uygula, sanal pozisyonun kar/zarar durumuna gore GERCEK sinyali uret.

```
STRATEJI SINYALI (Buy/Sell/Flat)
         |
         v
   SANAL POZISYON
         |
         v
   SANAL P/L TAKIP
         |
    _____|_____
   |           |
   v           v
Kar >= X    Zarar >= Y
   |           |
   v           v
GERCEK BUY  GERCEK BUY
(trend ok)  (reversal)
```

### Kullanim Senaryolari

1. **Tek SingleTrader ile**:
   - Bir strateji sanal calisir
   - Sanal P/L'ye gore gercek sinyal uretilir

2. **MultipleTrader ile (Kombinasyon)**:
   - N strateji sanal calisir
   - Her biri kendi konfirmasyonunu yapar
   - Konfirme edilmis sinyallerin consensus'u alinir

---

## 2. Mimari Tasarim

### 2.1 SingleTrader'a Eklenecek Ozellikler

```csharp
// ═══════════════════════════════════════════════════════════════
// CONFIRMATION MODE - Yeni Ozellikler
// ═══════════════════════════════════════════════════════════════

// Aktif/Pasif
public bool ConfirmationModeEnabled { get; set; } = false;

// Esik Degerleri (puan cinsinden)
public double KarKonfirmasyonEsigi { get; set; } = 10.0;    // Kar bu seviyeye ulasinca konfirme
public double ZararKonfirmasyonEsigi { get; set; } = 5.0;   // Zarar bu seviyeye ulasinca konfirme

// Tetikleyici Modu
public ConfirmationTrigger KonfirmasyonTetikleyici { get; set; } = ConfirmationTrigger.Both;

// Sanal Pozisyon Takibi (private)
private string _sanalYon = "F";           // "A", "S", "F"
private double _sanalGirisFiyati = 0;
private int _sanalGirisBarNo = 0;
private double _sanalKarZarar = 0;
private bool _bekleyenKonfirmasyon = false;

// Konfirme Edilmis Sinyal (public readonly)
public TradeSignals ConfirmedSignal { get; private set; } = TradeSignals.None;
```

### 2.2 ConfirmationTrigger Enum

```csharp
public enum ConfirmationTrigger
{
    KarOnly,      // Sadece kar esiginde konfirme (trend onay)
    ZararOnly,    // Sadece zarar esiginde konfirme (reversal)
    Both          // Her ikisinde de konfirme
}
```

### 2.3 Sinyal Akisi

```
Normal Mod (ConfirmationModeEnabled = false):
┌─────────────────────────────────────────────────────────┐
│ Strategy.OnStep(i) → StrategySignal → emirleri_setle() │
│                                       → emirleri_uygula()│
└─────────────────────────────────────────────────────────┘

Confirmation Mod (ConfirmationModeEnabled = true):
┌─────────────────────────────────────────────────────────┐
│ Strategy.OnStep(i) → StrategySignal                     │
│         │                                               │
│         v                                               │
│ ProcessConfirmationMode()                               │
│   ├── UpdateSanalPozisyon()                             │
│   ├── UpdateSanalKarZarar()                             │
│   └── CheckConfirmation() → ConfirmedSignal             │
│         │                                               │
│         v                                               │
│ emirleri_setle(ConfirmedSignal)                         │
│ emirleri_uygula()                                       │
└─────────────────────────────────────────────────────────┘
```

---

## 3. SingleTrader Degisiklikleri (Detayli Kod)

### 3.1 Yeni Enum - ConfirmationTrigger.cs

Dosya: `src/Trading/Core/ConfirmationTrigger.cs`

```csharp
namespace AlgoTradeWithOptimizationSupportWinFormsApp.Trading.Core
{
    /// <summary>
    /// Konfirmasyon tetikleyici modu
    /// </summary>
    public enum ConfirmationTrigger
    {
        /// <summary>
        /// Sadece kar esiginde konfirme et (trend onay)
        /// Sanal pozisyon kar yaziyorsa, trend dogru yonde demektir
        /// </summary>
        KarOnly,

        /// <summary>
        /// Sadece zarar esiginde konfirme et (reversal beklentisi)
        /// Sanal pozisyon zarar yaziyorsa, fiyat donecek demektir
        /// </summary>
        ZararOnly,

        /// <summary>
        /// Her ikisinde de konfirme et
        /// Kar veya zarar esigine ulasinca gercek sinyal uret
        /// </summary>
        Both
    }
}
```

### 3.2 SingleTrader.cs Degisiklikleri

Eklenecek bolum (public properties):

```csharp
// ═══════════════════════════════════════════════════════════════════════
// CONFIRMATION MODE - Sanal Sinyal ve Geciktirilmis Gercek Sinyal Sistemi
// ═══════════════════════════════════════════════════════════════════════

/// <summary>
/// Confirmation Mode aktif mi?
/// true: Strateji sinyalleri once sanal uygulanir, P/L'ye gore gercek sinyal uretilir
/// false: Normal mod - strateji sinyalleri direkt uygulanir
/// </summary>
public bool ConfirmationModeEnabled { get; set; } = false;

/// <summary>
/// Kar konfirmasyon esigi (puan)
/// Sanal pozisyon bu kadar kar yazinca gercek sinyal uretilir
/// Ornek: 10.0 = 10 puan kar yazinca konfirme
/// </summary>
public double KarKonfirmasyonEsigi { get; set; } = 10.0;

/// <summary>
/// Zarar konfirmasyon esigi (puan) - POZITIF deger girilmeli
/// Sanal pozisyon bu kadar zarar yazinca gercek sinyal uretilir
/// Ornek: 5.0 = 5 puan zarar yazinca konfirme
/// </summary>
public double ZararKonfirmasyonEsigi { get; set; } = 5.0;

/// <summary>
/// Konfirmasyon tetikleyici modu
/// KarOnly: Sadece kar esiginde konfirme (trend onay)
/// ZararOnly: Sadece zarar esiginde konfirme (reversal)
/// Both: Her ikisinde de konfirme
/// </summary>
public ConfirmationTrigger KonfirmasyonTetikleyici { get; set; } = ConfirmationTrigger.Both;

/// <summary>
/// Konfirme edilmis sinyal (readonly)
/// ConfirmationMode aktifken bu sinyal gercek emirlere gonderilir
/// </summary>
public TradeSignals ConfirmedSignal { get; private set; } = TradeSignals.None;

// ═══════════════════════════════════════════════════════════════════════
// CONFIRMATION MODE - Private Sanal Pozisyon Takibi
// ═══════════════════════════════════════════════════════════════════════

private string _sanalYon = "F";              // Sanal pozisyon yonu: "A"=Long, "S"=Short, "F"=Flat
private double _sanalGirisFiyati = 0;        // Sanal giris fiyati
private int _sanalGirisBarNo = 0;            // Sanal giris bar numarasi
private double _sanalKarZarar = 0;           // Sanal anlik kar/zarar (puan)
private bool _bekleyenKonfirmasyon = false;  // Konfirmasyon bekliyor mu?
```

Eklenecek metodlar:

```csharp
// ═══════════════════════════════════════════════════════════════════════
// CONFIRMATION MODE - Islem Metodlari
// ═══════════════════════════════════════════════════════════════════════

/// <summary>
/// Confirmation Mode ana isleme metodu
/// Strateji sinyalini sanal pozisyona uygular ve konfirmasyon kontrolu yapar
/// </summary>
private void ProcessConfirmationMode(int i, TradeSignals strategySignal)
{
    double currentPrice = Data[i].Close;

    // 1. Sanal pozisyonu guncelle (yeni sinyal varsa)
    UpdateSanalPozisyon(i, strategySignal, currentPrice);

    // 2. Sanal P/L hesapla
    UpdateSanalKarZarar(currentPrice);

    // 3. Konfirmasyon kontrolu yap
    ConfirmedSignal = CheckConfirmation();
}

/// <summary>
/// Sanal pozisyonu gunceller
/// Strateji sinyaline gore sanal Long/Short/Flat pozisyon acar
/// </summary>
private void UpdateSanalPozisyon(int i, TradeSignals signal, double price)
{
    // ─────────────────────────────────────────────────────────────────
    // FLAT SINYALI - Direkt gecis (beklemeden)
    // ─────────────────────────────────────────────────────────────────
    if (signal == TradeSignals.Flat)
    {
        if (_sanalYon != "F")
        {
            _sanalYon = "F";
            _sanalKarZarar = 0;
            _bekleyenKonfirmasyon = false;
            ConfirmedSignal = TradeSignals.Flat;  // FLAT direkt konfirme edilir
        }
        return;
    }

    // ─────────────────────────────────────────────────────────────────
    // BUY SINYALI - Sanal Long pozisyon ac
    // ─────────────────────────────────────────────────────────────────
    if (signal == TradeSignals.Buy && _sanalYon != "A")
    {
        // Eski sanal pozisyonu kapat (varsa)
        _sanalYon = "A";
        _sanalGirisFiyati = price;
        _sanalGirisBarNo = i;
        _sanalKarZarar = 0;
        _bekleyenKonfirmasyon = true;
        ConfirmedSignal = TradeSignals.None;  // Henuz konfirme degil, bekle
    }

    // ─────────────────────────────────────────────────────────────────
    // SELL SINYALI - Sanal Short pozisyon ac
    // ─────────────────────────────────────────────────────────────────
    if (signal == TradeSignals.Sell && _sanalYon != "S")
    {
        // Eski sanal pozisyonu kapat (varsa)
        _sanalYon = "S";
        _sanalGirisFiyati = price;
        _sanalGirisBarNo = i;
        _sanalKarZarar = 0;
        _bekleyenKonfirmasyon = true;
        ConfirmedSignal = TradeSignals.None;  // Henuz konfirme degil, bekle
    }
}

/// <summary>
/// Sanal pozisyonun anlik kar/zararini hesaplar
/// </summary>
private void UpdateSanalKarZarar(double currentPrice)
{
    if (_sanalYon == "A")
    {
        // Long pozisyon: Fiyat artarsa kar
        _sanalKarZarar = currentPrice - _sanalGirisFiyati;
    }
    else if (_sanalYon == "S")
    {
        // Short pozisyon: Fiyat duserse kar
        _sanalKarZarar = _sanalGirisFiyati - currentPrice;
    }
    else
    {
        _sanalKarZarar = 0;
    }
}

/// <summary>
/// Konfirmasyon kontrolu yapar
/// Esik degerlerine gore gercek sinyal uretir veya None doner
/// </summary>
private TradeSignals CheckConfirmation()
{
    // Bekleyen konfirmasyon yoksa None don
    if (!_bekleyenKonfirmasyon)
        return TradeSignals.None;

    // Esik kontrolu
    bool karTetiklendi = _sanalKarZarar >= KarKonfirmasyonEsigi;
    bool zararTetiklendi = _sanalKarZarar <= -ZararKonfirmasyonEsigi;

    // Tetikleyici moduna gore kontrol
    bool konfirmeEt = KonfirmasyonTetikleyici switch
    {
        ConfirmationTrigger.KarOnly => karTetiklendi,
        ConfirmationTrigger.ZararOnly => zararTetiklendi,
        ConfirmationTrigger.Both => karTetiklendi || zararTetiklendi,
        _ => false
    };

    // Konfirme edildi mi?
    if (konfirmeEt)
    {
        _bekleyenKonfirmasyon = false;  // Artik bekleme yok

        // Sanal yonu gercek sinyale cevir
        if (_sanalYon == "A")
            return TradeSignals.Buy;
        if (_sanalYon == "S")
            return TradeSignals.Sell;
    }

    return TradeSignals.None;  // Henuz konfirme olmadi
}

/// <summary>
/// Confirmation Mode durumunu sifirlar
/// Reset veya yeni backtest baslatildiginda cagrilir
/// </summary>
public void ResetConfirmationMode()
{
    _sanalYon = "F";
    _sanalGirisFiyati = 0;
    _sanalGirisBarNo = 0;
    _sanalKarZarar = 0;
    _bekleyenKonfirmasyon = false;
    ConfirmedSignal = TradeSignals.None;
}
```

Run metodu degisikligi:

```csharp
// Run metodunda degisiklik (mevcut koda ekleme)
public void Run(int i)
{
    // ... mevcut kodlar ...

    // Strateji sinyalini al
    this.StrategySignal = this.Strategy.OnStep(i);

    // ═══════════════════════════════════════════════════════════════
    // CONFIRMATION MODE KONTROLU
    // ═══════════════════════════════════════════════════════════════
    if (ConfirmationModeEnabled)
    {
        // Sanal islem ve konfirmasyon kontrolu
        ProcessConfirmationMode(i, this.StrategySignal);

        // Gercek emirlere SADECE konfirme edilmis sinyali gonder
        emirleri_setle(i, this.ConfirmedSignal);
    }
    else
    {
        // Normal mod - strateji sinyalini direkt uygula
        emirleri_setle(i, this.StrategySignal);
    }

    // ... devami ...
}
```

---

## 4. GUI Tasarimi - ConfirmingSingleTrader Tab

### 4.1 Tab Yapisi

```
tabPageConfirmingSingleTrader
└── panelConfirmingSingleTrader
    ├── groupBoxConfirmationSettings
    │   ├── chkConfirmationModeEnabled (CheckBox)
    │   ├── lblKarEsigi + txtKarEsigi (Label + TextBox)
    │   ├── lblZararEsigi + txtZararEsigi (Label + TextBox)
    │   └── lblTetikleyici + cmbTetikleyici (Label + ComboBox)
    │
    ├── btnStartConfirmingSingleTrader (Button)
    ├── btnStopConfirmingSingleTrader (Button)
    ├── btnPlotConfirmingSingleTraderData (Button)
    │
    ├── progressBarConfirmingSingleTrader (ProgressBar)
    ├── lblConfirmingSingleTraderProgress (Label)
    │
    └── richTextBoxConfirmingSingleTrader (RichTextBox)
```

### 4.2 Kontrol Isimleri (Naming Convention)

| Mevcut (SingleTrader)         | Yeni (ConfirmingSingleTrader)              |
|-------------------------------|-------------------------------------------|
| tabPageSingleTrader           | tabPageConfirmingSingleTrader             |
| panel4                        | panelConfirmingSingleTrader               |
| btnStartSingleTrader          | btnStartConfirmingSingleTrader            |
| btnStopSingleTrader           | btnStopConfirmingSingleTrader             |
| btnPlotSingleTraderData       | btnPlotConfirmingSingleTraderData         |
| progressBarSingleTrader       | progressBarConfirmingSingleTrader         |
| lblSingleTraderProgress       | lblConfirmingSingleTraderProgress         |
| richTextBoxSingleTrader       | richTextBoxConfirmingSingleTrader         |

---

## 5. GUI Tasarimi - ConfirmingMultipleTrader Tab

### 5.1 Tab Yapisi

```
tabPageConfirmingMultipleTrader
└── panelConfirmingMultipleTrader
    ├── groupBoxConfirmationSettingsMulti
    │   ├── chkConfirmationModeEnabledMulti (CheckBox)
    │   ├── lblKarEsigiMulti + txtKarEsigiMulti (Label + TextBox)
    │   ├── lblZararEsigiMulti + txtZararEsigiMulti (Label + TextBox)
    │   └── lblTetikleyiciMulti + cmbTetikleyiciMulti (Label + ComboBox)
    │
    ├── btnStartConfirmingMultipleTrader (Button)
    ├── btnStopConfirmingMultipleTrader (Button)
    ├── btnPlotConfirmingMultipleTraderData (Button)
    │
    ├── progressBarConfirmingMultipleTrader (ProgressBar)
    ├── lblConfirmingMultipleTraderProgress (Label)
    │
    └── richTextBoxConfirmingMultipleTrader (RichTextBox)
```

---

## 6. Test Senaryolari

### 6.1 Temel Testler

| Test | Aciklama | Beklenen Sonuc |
|------|----------|----------------|
| T1 | ConfirmationMode = false | Normal trading, mevcut davranis |
| T2 | ConfirmationMode = true, KarOnly, Kar >= Esik | Gercek sinyal uretilir |
| T3 | ConfirmationMode = true, ZararOnly, Zarar >= Esik | Gercek sinyal uretilir |
| T4 | ConfirmationMode = true, Both, Kar >= Esik | Gercek sinyal uretilir |
| T5 | ConfirmationMode = true, Both, Zarar >= Esik | Gercek sinyal uretilir |
| T6 | ConfirmationMode = true, Esiklere ulasilmadi | None sinyal, bekle |
| T7 | Sanal FLAT sinyali | Direkt gercek FLAT |

### 6.2 MultipleTrader Testleri

| Test | Aciklama | Beklenen Sonuc |
|------|----------|----------------|
| TM1 | N trader, hepsi ConfirmationMode = true | Her trader kendi konfirmasyonunu yapar |
| TM2 | N trader, karisik modlar | Sadece enabled olanlar konfirmasyon yapar |
| TM3 | Consensus of confirmed signals | Konfirme edilmis sinyallerin bilesimi |

---

## 7. Uygulama Adimlari (Checklist)

### Adim 1: Yeni Tablari Olustur (GUI)
- [ ] tabPageConfirmingSingleTrader - SingleTrader Tab'dan kopyala
- [ ] tabPageConfirmingMultipleTrader - MultipleTrader Tab'dan kopyala
- [ ] Kontrolleri yeniden isimlendir
- [ ] Event handler'lari bagla
- [ ] Test: Tablar gorunur ve calisir durumda

### Adim 2: ConfirmationTrigger Enum
- [ ] `src/Trading/Core/ConfirmationTrigger.cs` dosyasini olustur
- [ ] Enum degerlerini ekle (KarOnly, ZararOnly, Both)

### Adim 3: SingleTrader Degisiklikleri
- [ ] Public property'leri ekle
- [ ] Private field'lari ekle
- [ ] ProcessConfirmationMode() metodunu ekle
- [ ] UpdateSanalPozisyon() metodunu ekle
- [ ] UpdateSanalKarZarar() metodunu ekle
- [ ] CheckConfirmation() metodunu ekle
- [ ] ResetConfirmationMode() metodunu ekle
- [ ] Run() metodunu guncelle
- [ ] Reset() metodunda ResetConfirmationMode() cagir

### Adim 4: GUI Event Handler'lari
- [ ] btnStartConfirmingSingleTrader_Click
- [ ] btnStopConfirmingSingleTrader_Click
- [ ] btnPlotConfirmingSingleTraderData_Click
- [ ] Logger sinifi (ConfirmingSingleTraderLogger)

### Adim 5: MultipleTrader Entegrasyonu
- [ ] MultipleTrader icindeki SingleTrader'larin ConfirmationMode'u kullanmasini sagla
- [ ] buildConsensusSignal'in ConfirmedSignal'i kullanmasini sagla

### Adim 6: Test ve Debug
- [ ] Temel testleri calistir (T1-T7)
- [ ] MultipleTrader testlerini calistir (TM1-TM3)
- [ ] Log ciktilari kontrol et

---

## 8. Notlar

- SingleTrader sinifi EXTEND edilmeyecek, mevcut sinifa ozellik eklenecek
- MultipleTrader sinifi EXTEND edilmeyecek, mevcut sinifa ozellik eklenecek
- Yeni tablar bagimsiz calisacak, mevcut tablar degismeyecek
- Confirmation Mode default olarak KAPALI (false)
- FLAT sinyali her zaman direkt uygulanacak (konfirmasyon beklemeden)

---

*Son Guncelleme: 2026-01-21*
*Versiyon: 1.0*
