using Avalonia.Media;

namespace GranTurismoTelemetry.Theme;

public static class GTTheme
{
    public static readonly Color Page = Color.Parse("#0B1220");
    public static readonly Color Inset = Color.Parse("#1B2436");
    public static readonly Color Cyan = Color.Parse("#22D3EE");
    public static readonly Color Green = Color.Parse("#22C55E");
    public static readonly Color Red = Color.Parse("#EF4444");
    public static readonly Color Amber = Color.Parse("#F59E0B");
    public static readonly Color Muted = Color.Parse("#8B97AB");
    public static readonly Color Track = Color.Parse("#2A3550");

    public static readonly IBrush PageBrush = new SolidColorBrush(Page);
    public static readonly IBrush InsetBrush = new SolidColorBrush(Inset);
    public static readonly IBrush CyanBrush = new SolidColorBrush(Cyan);
    public static readonly IBrush GreenBrush = new SolidColorBrush(Green);
    public static readonly IBrush RedBrush = new SolidColorBrush(Red);
    public static readonly IBrush AmberBrush = new SolidColorBrush(Amber);
    public static readonly IBrush MutedBrush = new SolidColorBrush(Muted);
    public static readonly IBrush TrackBrush = new SolidColorBrush(Track);

    public static Color TireColor(float temp) => temp switch
    {
        < 70f => Cyan,
        < 90f => Green,
        < 105f => Amber,
        _ => Red,
    };

    public static IBrush TireBrush(float temp) => new SolidColorBrush(TireColor(temp));

    public static Color DeltaColor(double seconds)
    {
        if (Math.Abs(seconds) < 0.0005) return Muted;
        return seconds < 0 ? Green : Red;
    }

    public static IBrush DeltaBrush(double seconds) => new SolidColorBrush(DeltaColor(seconds));

    /// <summary>Lap-table deltas: green when faster, amber when slower (Figma v2).</summary>
    public static IBrush TableDeltaBrush(double seconds)
    {
        if (Math.Abs(seconds) < 0.0005) return MutedBrush;
        if (seconds < 0) return GreenBrush;
        return seconds < 0.5 ? AmberBrush : RedBrush;
    }
}
