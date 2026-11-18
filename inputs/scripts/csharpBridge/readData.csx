// Read Data asamasi - globals: FilePath (string), LastN (int, 0 = Tumu)
// return: object[] { data, metaSummary }
//   data        : List<StockData>
//   metaSummary : string (Record Time/Chart Symbol/Period/Bar Count/Start-End Date/Format)

// AppConfig: Menu 5 | SingleTraderConfig: Menu 8 -> 1 | Manual: GUI ve asagidaki ayarlar.
string readSettingsSource = "Manual";

object[] ReadFromAppConfig()
{
    string configPath = System.IO.Path.Combine(AppSettings.ConfigsDir, "AppConfig", "AppConfig.json");
    var config = AppConfigLoader.Load(configPath);
    string filePath = AppConfigApplier.ApplyAppSettings(config.AppSettings);
    var settings = config.ReadData;

    if (settings == null)
        return ReadData(filePath, StockDataReader.FilterMode.All, 0, 0, null, null);

    Enum.TryParse<StockDataReader.FilterMode>(settings.FilterMode, ignoreCase: true, out var filterMode);
    DateTime? dt1 = string.IsNullOrWhiteSpace(settings.Dt1) ? null : DateTime.Parse(settings.Dt1);
    DateTime? dt2 = string.IsNullOrWhiteSpace(settings.Dt2) ? null : DateTime.Parse(settings.Dt2);
    return ReadData(filePath, filterMode, settings.N1, settings.N2, dt1, dt2);
}

async Task<object[]> ReadFromSingleTraderConfig()
{
    string configPath = System.IO.Path.Combine(AppSettings.ScriptsDir, "Config_01_SingleTrader.csx");
    string code = System.IO.File.ReadAllText(configPath) + "\n"
        + "return new object[] { stockDataFullFileName, readDataFilterMode, readDataN1, readDataN2, readDataDt1, readDataDt2 };";
    var executor = new ScriptExecutor();
    var result = await executor.ExecuteAsync(code, new ReadDataScriptGlobals(FilePath, LastN),
        sourceDirectory: System.IO.Path.GetDirectoryName(configPath));
    if (!result.Success)
    {
        string error = result.CompilationErrors != null
            ? string.Join("\n", result.CompilationErrors)
            : result.Error ?? "Bilinmeyen hata.";
        throw new InvalidOperationException($"Config_01_SingleTrader.csx okunamadi: {error}");
    }

    var settings = (object[])result.ReturnValue!;
    Enum.TryParse<StockDataReader.FilterMode>((string)settings[1], ignoreCase: true, out var filterMode);
    string start = (string)settings[4];
    string end = (string)settings[5];
    DateTime? dt1 = string.IsNullOrWhiteSpace(start) ? null : DateTime.Parse(start);
    DateTime? dt2 = string.IsNullOrWhiteSpace(end) ? null : DateTime.Parse(end);
    return ReadData((string)settings[0], filterMode, (int)settings[2], (int)settings[3], dt1, dt2);
}

object[] ReadFromManualSettings()
{
    // =============================================================================
    // Manuel veri filtreleme (Menu 8 -> 1 ile ayni ayar deseni).
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
    return ReadData(FilePath, filterMode, readDataN1, readDataN2, dt1, dt2);
}

object[] ReadData(string filePath, StockDataReader.FilterMode filterMode, int n1, int n2, DateTime? dt1, DateTime? dt2)
{
    if (!System.IO.File.Exists(filePath))
        throw new Exception("Dosya bulunamadi: " + filePath);

    var reader = new StockDataReader();
    reader.ReadMetaData(filePath);
    if (!reader.IsMetaDataRead)
        throw new Exception("MetaData okunamadi: " + filePath);

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

    reader.ReadDataFast(filePath, filterMode, n1, n2, dt1, dt2);

    var data = reader.GetData();
    if (data.Count == 0)
        throw new Exception("Data bos.");

    return new object[] { data, metaSummary };
}

return readSettingsSource switch
{
    "AppConfig" => ReadFromAppConfig(),
    "SingleTraderConfig" => await ReadFromSingleTraderConfig(),
    "Manual" => ReadFromManualSettings(),
    _ => throw new ArgumentOutOfRangeException(nameof(readSettingsSource), readSettingsSource, "Bilinmeyen ayar kaynagi.")
};
