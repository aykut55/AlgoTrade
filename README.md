# AlgoTrade
Algo Trade With C# (Included Optimization, Scripting and Charting)

## Proje Yapısı

```
AlgoTrade.sln
├── src/AlgoTrade.Core/        → Paylaşılan class library (net8.0)
├── AlgoTrade.Console/         → Console uygulaması (net8.0)
├── AlgoTrade.WinForms/        → WinForms uygulaması (net8.0-windows)
├── inputs/                    → Girdi dosyaları
├── outputs/                   → Çıktı dosyaları
```

- **AlgoTrade.Core** — Ortak sınıflar, modeller ve iş mantığı. Her iki uygulama bu projeyi referans alır.
- **AlgoTrade.Console** — Komut satırı arayüzü.
- **AlgoTrade.WinForms** — Grafiksel arayüz (Windows Forms).

## Geliştirme

| Araç | Kullanım |
|------|----------|
| **Visual Studio** | `AlgoTrade.sln` dosyasını aç. WinForms designer desteği için gerekli. |
| **VS Code** | Root klasörü aç. Hızlı kod düzenleme ve terminal işlemleri için. |

## Araçlar

### clean.bat
Tüm `bin/` ve `obj/` klasörlerini siler. Commit öncesi çalıştırılması önerilir.

```bash
.\clean.bat
```
