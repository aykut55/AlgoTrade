# Ileride Yapilacaklar

## Python Entegrasyonu

Proje yapisina `AlgoTrade.Python/` klasoru eklenebilir. C# ve Python arasinda veri paylasimi icin uc yaklasim mevcut:

### 1. Ortak Veri Dosyalari (Onerilen - Baslangic icin)
- `inputs/` ve `outputs/` klasorleri her iki taraftan okunup yazilabilir (JSON, CSV vb.)
- En basit ve bagimsiz yaklasim
- Ek kutuphane veya konfigurasyona gerek yok

### 2. pythonnet (Python.NET)
- `pip install pythonnet` ile C# DLL'leri Python'dan dogrudan cagirilabilir
- AlgoTrade.Core icindeki siniflar Python'dan kullanilabilir hale gelir
- Kurulumu ve debug'i zahmetli olabilir

### 3. REST API / gRPC
- C# tarafi bir servis olarak calistirilir, Python HTTP ile cagrir
- Daha kurumsal bir yaklasim
- Projeler tamamen bagimsiz calisabilir
