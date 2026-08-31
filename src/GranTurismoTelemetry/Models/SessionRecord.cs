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
    public IBrush DeltaBrush => IsBest ? GTTheme.GreenBrush : GTTheme.TableDeltaBrush(DeltaSeconds ?? 0);
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
}

public static class SampleSessions
{
    // Newest-first demo times (old L12-L5). The rest fill out to 100 so debug mode can scroll.
    private static readonly (int TimeMs, double? S1, double? S2, double? S3)[] Newest =
    [
        (84_881, -0.031, -0.190, -0.121),
        (85_104, 0.102, 0.301, 0.162),
        (84_539, -0.010, -0.004, -0.002),
        (85_882, 0.402, 0.611, 0.330),
        (86_207, 0.512, 0.740, 0.416),
        (85_331, 0.210, 0.402, 0.180),
        (84_902, -0.020, 0.210, 0.173),
        (86_744, 0.702, 0.980, 0.523),
    ];

    public static IReadOnlyList<SessionRecord> All { get; } = [DeepForest()];

    private static SessionRecord DeepForest()
    {
        const int bestMs = 84_539;
        const int count = 100;
        var rows = new LapRow[count];
        for (int i = 0; i < count; i++)
        {
            int number = count - i;
            int timeMs;
            double? s1 = null, s2 = null, s3 = null;
            if (i < Newest.Length)
            {
                (timeMs, s1, s2, s3) = Newest[i];
            }
            else
            {
                // Deterministic scatter ~0.2s-2.8s off best so only the seeded BEST stays BEST.
                int wobble = ((number * 37) % 47) * 40 + ((number * 13) % 11) * 70;
                timeMs = bestMs + 180 + wobble;
            }

            bool isBest = timeMs == bestMs;
            double? delta = isBest ? null : (timeMs - bestMs) / 1000.0;
            rows[i] = new LapRow(number, timeMs, delta, isBest, s1, s2, s3);
        }

        return new(
            Track: "Deep Forest Raceway",
            CarClass: "Gr.3",
            BestLapMs: bestMs,
            Laps: count,
            WhenLabel: "Today 14:32",
            LastLapMs: rows[0].TimeMs,
            Sectors:
            [
                new(1, 27.902, -0.121),
                new(2, 31.902, 0.043),
                new(3, 24.323, 0.011),
            ],
            DeltaTrace: [0.05, -0.04, 0.08, -0.12, 0.04, -0.18, 0.02, -0.22, 0.06, -0.28, 0.10],
            LapRows: rows);
    }
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
}
