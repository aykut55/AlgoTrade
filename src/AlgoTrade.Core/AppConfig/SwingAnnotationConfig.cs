using Newtonsoft.Json;

namespace AlgoTrade.Core.AppConfig;

public sealed class SwingAnnotationConfig
{
    public SwingAnnotationWindowConfig Window { get; set; } = new();
    public SwingAnnotationRenderingConfig Rendering { get; set; } = new();

    public static SwingAnnotationConfig Load(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("Swing annotation config bulunamadı.", path);

        var config = JsonConvert.DeserializeObject<SwingAnnotationConfig>(File.ReadAllText(path))
                     ?? throw new InvalidDataException("Swing annotation config okunamadı.");
        config.Validate();
        return config;
    }

    private void Validate()
    {
        if (string.IsNullOrWhiteSpace(Window.InitialViewMode))
            Window.InitialViewMode = "FitToScreen (Ultra)";
        Window.MinimumWidthBars = Math.Max(1, Window.MinimumWidthBars);
        Window.MaximumWidthBars = Math.Max(Window.MinimumWidthBars, Window.MaximumWidthBars);
        Window.InitialWidthBars = Math.Clamp(
            Window.InitialWidthBars, Window.MinimumWidthBars, Window.MaximumWidthBars);
        Window.NavigationStepBars = Math.Clamp(
            Window.NavigationStepBars, 1, Window.InitialWidthBars);
        Window.InitialOffsetBars = Math.Max(0, Window.InitialOffsetBars);
        Window.Presets = (Window.Presets ?? Array.Empty<int>())
            .Where(v => v >= Window.MinimumWidthBars && v <= Window.MaximumWidthBars)
            .Distinct()
            .OrderBy(v => v)
            .ToArray();
        Rendering.MaxCandlesWithoutAggregation = Math.Max(100, Rendering.MaxCandlesWithoutAggregation);
    }
}

public sealed class SwingAnnotationWindowConfig
{
    public string InitialViewMode { get; set; } = "FitToScreen (Ultra)";
    public int InitialWidthBars { get; set; } = 2000;
    public int NavigationStepBars { get; set; } = 500;
    public int InitialOffsetBars { get; set; } = 0;
    public int MinimumWidthBars { get; set; } = 100;
    public int MaximumWidthBars { get; set; } = 20000;
    public bool ClampLastWindowToData { get; set; } = true;
    public bool AutoSaveOnWindowChange { get; set; } = true;
    public int[] Presets { get; set; } = [250, 500, 1000, 2000, 5000, 10000];
}

public sealed class SwingAnnotationRenderingConfig
{
    public int MaxCandlesWithoutAggregation { get; set; } = 10000;
    public bool LoadVolume { get; set; } = true;
    public bool LoadSize { get; set; } = false;
}
