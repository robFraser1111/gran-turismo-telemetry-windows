using Avalonia.Media;

namespace GranTurismoTelemetry.Theme;

public static class GTTheme
{
    public static readonly Color Page = Color.Parse("#0B1220");
    public static readonly Color Header = Color.Parse("#0E1626");
    public static readonly Color Card = Color.Parse("#111A2B");
    public static readonly Color Well = Color.Parse("#0F172A");
    public static readonly Color Inset = Color.Parse("#1B2436");
    public static readonly Color Cyan = Color.Parse("#22D3EE");
    public static readonly Color Green = Color.Parse("#22C55E");
    public static readonly Color Red = Color.Parse("#EF4444");
    public static readonly Color Amber = Color.Parse("#F59E0B");
    public static readonly Color Text = Color.Parse("#F4F7FB");
    public static readonly Color Muted = Color.Parse("#8B97AB");
    public static readonly Color Track = Color.Parse("#2A3550");
    public static readonly Color Chip = Color.Parse("#16243A");
    public static readonly Color ProFill = Color.Parse("#3A2A0E");
    public static readonly Color ConnectInk = Color.Parse("#052330");
    public static readonly Color LiveInk = Color.Parse("#06210F");
    public static readonly Color WindowFill = Color.Parse("#0E3A25");
    public static readonly Color FieldBorder = Color.Parse("#243149");
    public static readonly Color PresetOff = Color.Parse("#151D2E");

    public static readonly IBrush PageBrush = new SolidColorBrush(Page);
    public static readonly IBrush HeaderBrush = new SolidColorBrush(Header);
    public static readonly IBrush CardBrush = new SolidColorBrush(Card);
    public static readonly IBrush WellBrush = new SolidColorBrush(Well);
    public static readonly IBrush InsetBrush = new SolidColorBrush(Inset);
    public static readonly IBrush CyanBrush = new SolidColorBrush(Cyan);
    public static readonly IBrush GreenBrush = new SolidColorBrush(Green);
    public static readonly IBrush RedBrush = new SolidColorBrush(Red);
    public static readonly IBrush AmberBrush = new SolidColorBrush(Amber);
    public static readonly IBrush TextBrush = new SolidColorBrush(Text);
    public static readonly IBrush MutedBrush = new SolidColorBrush(Muted);
    public static readonly IBrush TrackBrush = new SolidColorBrush(Track);
    public static readonly IBrush ChipBrush = new SolidColorBrush(Chip);
    public static readonly IBrush ProFillBrush = new SolidColorBrush(ProFill);
    public static readonly IBrush ConnectInkBrush = new SolidColorBrush(ConnectInk);
    public static readonly IBrush LiveInkBrush = new SolidColorBrush(LiveInk);
    public static readonly IBrush WindowFillBrush = new SolidColorBrush(WindowFill);
    public static readonly IBrush OverlayBrush = new SolidColorBrush(Color.FromArgb(140, 11, 18, 32));

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
