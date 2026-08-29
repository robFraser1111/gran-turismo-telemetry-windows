using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using GranTurismoTelemetry.Theme;

namespace GranTurismoTelemetry.Controls;

public sealed class QualityBarControl : Control
{
    public static readonly StyledProperty<double> FractionProperty =
        AvaloniaProperty.Register<QualityBarControl, double>(nameof(Fraction), 0.5);

    public static readonly StyledProperty<IBrush> FillProperty =
        AvaloniaProperty.Register<QualityBarControl, IBrush>(nameof(Fill), GTTheme.GreenBrush);

    static QualityBarControl()
    {
        AffectsRender<QualityBarControl>(FractionProperty, FillProperty);
    }

    public double Fraction
    {
        get => GetValue(FractionProperty);
        set => SetValue(FractionProperty, value);
    }

    public IBrush Fill
    {
        get => GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        double h = Math.Max(8, Bounds.Height);
        var bg = new Rect(0, 0, Bounds.Width, h);
        context.DrawRectangle(GTTheme.InsetBrush, null, bg, h / 2, h / 2);
        double frac = Math.Clamp(Fraction, 0.08, 1.0);
        var fill = new Rect(0, 0, Bounds.Width * frac, h);
        context.DrawRectangle(Fill, null, fill, h / 2, h / 2);
    }
}
