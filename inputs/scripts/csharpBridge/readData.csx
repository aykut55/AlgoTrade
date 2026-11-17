// Read Data asamasi - globals: FilePath (string), LastN (int, 0 = Tumu)
// return: object[] { data, metaSummary }
//   data        : List<StockData>
//   metaSummary : string (Record Time/Chart Symbol/Period/Bar Count/Start-End Date/Format)
//
// 01_RunSingleTraderWithProgressAsync.csx (Program.cs:64-92 OnReadMetaData) ile
// AYNI mantik: dosya var mi kontrolu, metadata ozeti, bos veri kontrolu.

if (!System.IO.File.Exists(FilePath))
    throw new Exception("Dosya bulunamadi: " + FilePath);

var reader = new StockDataReader();
reader.ReadMetaData(FilePath);
if (!reader.IsMetaDataRead)
    throw new Exception("MetaData okunamadi: " + FilePath);

var meta = reader.GetMetaData();
string Field(string key) => meta.ContainsKey(key) ? meta[key] : "N/A";

var metaSummary = string.Join("\n", new[]
{
    $"Record Time  : {Field("Kayit_Zamani")}",
    $"Chart Symbol : {Field("GrafikSembol")}",
    $"Chart Period : {Field("GrafikPeriyot")}",
    $"Bar Count    : {Field("BarCount")}",
    $"Start Date   : {Field("Baslangic_Tarihi")}",
    $"End Date     : {Field("Bitis_Tarihi")}",
    $"Format       : {Field("Format")}",
});

// =============================================================================
// Veri Filtreleme (ReadData) - Config_01_SingleTrader.csx ile AYNI kopya.
// FilterMode: All, LastN, FirstN, IndexRange, AfterDateTime, BeforeDateTime, DateTimeRange
// Dt1/Dt2 formati: "yyyy.MM.dd HH:mm:ss" (bos string = kullanilmiyor)
//
// readDataChoice: 0=All, 1=LastN, 2=FirstN, 3=IndexRange,
//                 4=AfterDateTime, 5=BeforeDateTime, 6=DateTimeRange
// GUI'deki "Bar" kutusu (LastN) > 0 ise readDataChoice OTOMATIK 1 (LastN) olur -
// baska bir mod kullanmak istiyorsan GUI'deki Bar'i 0 birak, asagidaki
// readDataChoice'u elle degistir.
// =============================================================================
int readDataChoice = LastN > 0 ? 1 : 0;

string readDataFilterMode;
int readDataN1 = 0;
int readDataN2 = 0;
string readDataDt1 = "";
string readDataDt2 = "";

if (readDataChoice == 0)
{
    readDataFilterMode = "All";
}
else if (readDataChoice == 1)
{
    readDataFilterMode = "LastN";
    readDataN1 = LastN > 0 ? LastN : 5000;
}
else if (readDataChoice == 2)
{
    readDataFilterMode = "FirstN";
    readDataN1 = 5000;
}
else if (readDataChoice == 3)
{
    readDataFilterMode = "IndexRange";
    readDataN1 = 0;
    readDataN2 = 5000;
}
else if (readDataChoice == 4)
{
    readDataFilterMode = "AfterDateTime";
    readDataDt1 = "2020.01.01 00:00:00";
}
else if (readDataChoice == 5)
{
    readDataFilterMode = "BeforeDateTime";
    readDataDt1 = "2020.01.01 00:00:00";
}
else if (readDataChoice == 6)
{
    readDataFilterMode = "DateTimeRange";
    readDataDt1 = "2020.01.01 00:00:00";
    readDataDt2 = "2024.01.01 00:00:00";
}
else
{
    throw new ArgumentOutOfRangeException(nameof(readDataChoice), $"Bilinmeyen readDataChoice: {readDataChoice}");
}

Enum.TryParse<StockDataReader.FilterMode>(readDataFilterMode, ignoreCase: true, out var filterMode);
DateTime? dt1 = string.IsNullOrWhiteSpace(readDataDt1) ? null : DateTime.Parse(readDataDt1);
DateTime? dt2 = string.IsNullOrWhiteSpace(readDataDt2) ? null : DateTime.Parse(readDataDt2);

reader.ReadDataFast(FilePath, filterMode, readDataN1, readDataN2, dt1, dt2);

var data = reader.GetData();
if (data.Count == 0)
    throw new Exception("Data bos.");

return new object[] { data, metaSummary };
