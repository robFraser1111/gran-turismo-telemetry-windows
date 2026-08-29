using System.Globalization;
using Avalonia;
using Avalonia.Media;
using GranTurismoTelemetry.Theme;

namespace GranTurismoTelemetry.Models;

public sealed record SectorCompare(int Number, double TimeSeconds, double DeltaSeconds);

public sealed record LapRow(int Number, int TimeMs, double? DeltaSeconds, bool IsBest,
    double? S1Delta = null, double? S2Delta = null, double? S3Delta = null, bool IsLatest = false)
{
    public string TimeLabel => Formatters.LapTime(TimeMs);
    public string NumberLabel => $"L{Number}";
    public string DeltaLabel => IsBest ? "BEST" : DeltaSeconds is double d ? Formatters.Delta(d) : "";
    public bool ShowDelta => !IsBest && DeltaSeconds is not null;
    public IBrush DeltaBrush => IsBest ? GTTheme.GreenBrush : GTTheme.TableDeltaBrush(DeltaSeconds ?? 0);
    public string S1Label => Formatters.Delta(S1Delta ?? 0);
    public string S2Label => Formatters.Delta(S2Delta ?? 0);
    public string S3Label => Formatters.Delta(S3Delta ?? 0);
    public IBrush S1Brush => GTTheme.DeltaBrush(S1Delta ?? 0);
    public IBrush S2Brush => GTTheme.DeltaBrush(S2Delta ?? 0);
    public IBrush S3Brush => GTTheme.DeltaBrush(S3Delta ?? 0);
    public IBrush RowBackground => IsBest
        ? new SolidColorBrush(Color.Parse("#0F2733"))
        : Brushes.Transparent;
    public IBrush RowBorderBrush => IsLatest ? GTTheme.CyanBrush : IsBest ? GTTheme.GreenBrush : Brushes.Transparent;
    public Thickness RowBorderThickness => IsLatest || IsBest ? new Thickness(1) : new Thickness(0);
}

public sealed record SessionRecord(
    string Track,
    string CarClass,
    int BestLapMs,
    int Laps,
    string WhenLabel,
    int LastLapMs,
    IReadOnlyList<SectorCompare> Sectors,
    IReadOnlyList<double> DeltaTrace,
    IReadOnlyList<LapRow> LapRows)
{
    public string Id => $"{Track}-{WhenLabel}";
    public string BestLapLabel => Formatters.LapTime(BestLapMs);
    public string ShortTrack => Formatters.ShortTrack(Track);
}

public static class SampleSessions
{
    public static IReadOnlyList<SessionRecord> All { get; } =
    [
        new(
            Track: "Deep Forest Raceway",
            CarClass: "Gr.3",
            BestLapMs: 84_539,
            Laps: 12,
            WhenLabel: "Today 14:32",
            LastLapMs: 84_881,
            Sectors:
            [
                new(1, 27.902, -0.121),
                new(2, 31.902, 0.043),
                new(3, 24.323, 0.011),
            ],
            DeltaTrace: [0.05, -0.04, 0.08, -0.12, 0.04, -0.18, 0.02, -0.22, 0.06, -0.28, 0.10],
            LapRows:
            [
                new(12, 84_881, -0.342, false, -0.031, -0.190, -0.121),
                new(11, 85_104, 0.565, false, 0.102, 0.301, 0.162),
                new(10, 84_539, null, true, -0.010, -0.004, -0.002),
                new(9, 85_882, 1.343, false, 0.402, 0.611, 0.330),
                new(8, 86_207, 1.668, false, 0.512, 0.740, 0.416),
                new(7, 85_331, 0.792, false, 0.210, 0.402, 0.180),
                new(6, 84_902, 0.363, false, -0.020, 0.210, 0.173),
                new(5, 86_744, 2.205, false, 0.702, 0.980, 0.523),
            ]),
        new(
            Track: "Trial Mountain",
            CarClass: "Gr.4",
            BestLapMs: 118_204,
            Laps: 8,
            WhenLabel: "Yesterday 20:17",
            LastLapMs: 118_540,
            Sectors:
            [
                new(1, 38.2, 0.08),
                new(2, 42.1, -0.04),
                new(3, 37.904, 0.12),
            ],
            DeltaTrace: [0.1, 0.05, -0.02, 0.08, 0.14, 0.09, 0.16],
            LapRows: []),
        new(
            Track: "Suzuka Circuit",
            CarClass: "Gr.2",
            BestLapMs: 125_771,
            Laps: 15,
            WhenLabel: "Mon 18:41",
            LastLapMs: 126_102,
            Sectors:
            [
                new(1, 36.4, -0.05),
                new(2, 48.9, 0.21),
                new(3, 40.471, 0.04),
            ],
            DeltaTrace: [0.0, 0.04, 0.12, 0.08, 0.18, 0.22, 0.15],
            LapRows: []),
        new(
            Track: "Nürburgring GP",
            CarClass: "Gr.3",
            BestLapMs: 112_318,
            Laps: 9,
            WhenLabel: "Sun 11:26",
            LastLapMs: 112_890,
            Sectors:
            [
                new(1, 32.1, 0.11),
                new(2, 41.2, -0.08),
                new(3, 39.018, 0.05),
            ],
            DeltaTrace: [0.04, -0.02, 0.07, 0.12, 0.03, 0.09],
            LapRows: []),
    ];
}

public static class Formatters
{
    public static string LapTime(int ms)
    {
        if (ms <= 0) return "–.–––";
        double total = ms / 1000.0;
        int minutes = (int)total / 60;
        double seconds = total % 60.0;
        return string.Format(CultureInfo.InvariantCulture, "{0}:{1:00.000}", minutes, seconds);
    }

    public static string Delta(double seconds, bool showPlus = true)
    {
        if (Math.Abs(seconds) < 0.0005) return "0.000";
        string sign = seconds < 0 ? "−" : showPlus ? "+" : "";
        return string.Format(CultureInfo.InvariantCulture, "{0}{1:0.000}", sign, Math.Abs(seconds));
    }

    public static string Sector(double seconds) =>
        string.Format(CultureInfo.InvariantCulture, "{0:0.000}", seconds);

    public static string ShortTrack(string name) =>
        name.Replace(" Raceway", "").Replace(" Circuit", "");
}
