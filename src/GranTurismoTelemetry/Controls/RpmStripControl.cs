using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using GranTurismoTelemetry.Theme;

namespace GranTurismoTelemetry.Controls;

public sealed class RpmStripControl : Control
{
    public static readonly StyledProperty<double> FractionProperty =
        AvaloniaProperty.Register<RpmStripControl, double>(nameof(Fraction));

    static RpmStripControl()
    {
        AffectsRender<RpmStripControl>(FractionProperty);
    }

    public double Fraction
    {
        get => GetValue(FractionProperty);
        set => SetValue(FractionProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        const int count = 10;
        double gap = 7;
        double w = Bounds.Width;
        double h = Math.Max(8, Bounds.Height);
        double cell = (w - gap * (count - 1)) / count;
        double frac = Fraction;

        for (int i = 0; i < count; i++)
        {
            bool on = frac > i / (double)count;
            // Figma: 4 green, 3 amber, 3 red
            var color = i >= count - 3 ? GTTheme.Red
                : i >= count - 6 ? GTTheme.Amber
                : GTTheme.Green;
            IBrush brush = on ? new SolidColorBrush(color) : new SolidColorBrush(Color.FromArgb(46, color.R, color.G, color.B));
            var rect = new Rect(i * (cell + gap), (Bounds.Height - h) / 2, Math.Max(1, cell), h);
            context.DrawRectangle(brush, null, rect, h / 2, h / 2);
        }
    }
}
