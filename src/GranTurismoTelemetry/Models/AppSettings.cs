using System.IO;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;

namespace GranTurismoTelemetry.Models;

public enum LayoutPreset
{
    Driving,
    Endurance,
    Minimal,
}

public static class LayoutPresetExtensions
{
    public static string Label(this LayoutPreset preset) => preset.ToString();

    public static LayoutPreset FromRaw(string? raw) =>
        raw switch
        {
            "Endurance" => LayoutPreset.Endurance,
            "Minimal" => LayoutPreset.Minimal,
            _ => LayoutPreset.Driving,
        };
}

/// <summary>
/// Persisted app settings (JSON in LocalApplicationData).
/// </summary>
public partial class AppSettings : ObservableObject
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    [ObservableProperty] private string _ps5IP = "";
    [ObservableProperty] private bool _useSimulator;
    [ObservableProperty] private LayoutPreset _preset = LayoutPreset.Driving;
    [ObservableProperty] private string _hudMode = "Simple";

    private bool _loading;

    public static string FilePath
    {
        get
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "GranTurismoTelemetry");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "settings.json");
        }
    }

    public static AppSettings Load()
    {
        var settings = new AppSettings { _loading = true };
        try
        {
            if (File.Exists(FilePath))
            {
                var dto = JsonSerializer.Deserialize<Dto>(File.ReadAllText(FilePath), JsonOpts);
                if (dto is not null)
                {
                    settings.Ps5IP = dto.Ps5IP?.Trim() ?? "";
                    settings.UseSimulator = dto.UseSimulator;
                    settings.Preset = LayoutPresetExtensions.FromRaw(dto.Preset);
                    settings.HudMode = NormalizeHudMode(dto.HudMode);
                }
            }
        }
        catch
        {
            // keep defaults
        }
        settings._loading = false;
        return settings;
    }

    public void Save()
    {
        if (_loading) return;
        try
        {
            var dto = new Dto
            {
                Ps5IP = Ps5IP,
                UseSimulator = UseSimulator,
                Preset = Preset.ToString(),
                HudMode = NormalizeHudMode(HudMode),
            };
            File.WriteAllText(FilePath, JsonSerializer.Serialize(dto, JsonOpts));
        }
        catch
        {
            // ignore IO errors
        }
    }

    partial void OnPs5IPChanged(string value) => Save();
    partial void OnUseSimulatorChanged(bool value) => Save();
    partial void OnPresetChanged(LayoutPreset value) => Save();
    partial void OnHudModeChanged(string value) => Save();

    public static string NormalizeHudMode(string? raw) => raw switch
    {
        "Driving" => "Driving",
        "PitWall" or "Pit wall" => "PitWall",
        _ => "Simple",
    };

    private sealed class Dto
    {
        public string Ps5IP { get; set; } = "";
        public bool UseSimulator { get; set; }
        public string Preset { get; set; } = "Driving";
        public string HudMode { get; set; } = "Simple";
    }
}
