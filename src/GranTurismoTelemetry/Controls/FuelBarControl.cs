using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using GranTurismoTelemetry.Theme;

namespace GranTurismoTelemetry.Controls;

public sealed class FuelBarControl : Control
{
    public static readonly StyledProperty<double> PercentProperty =
        AvaloniaProperty.Register<FuelBarControl, double>(nameof(Percent));

    public static readonly StyledProperty<IBrush> FillProperty =
        AvaloniaProperty.Register<FuelBarControl, IBrush>(nameof(Fill), GTTheme.AmberBrush);

    static FuelBarControl()
    {
        AffectsRender<FuelBarControl>(PercentProperty, FillProperty);
    }

    public double Percent
    {
        get => GetValue(PercentProperty);
        set => SetValue(PercentProperty, value);
    }

    public IBrush Fill
    {
        get => GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var bg = new Rect(0, 0, Bounds.Width, Bounds.Height);
        context.DrawRectangle(GTTheme.InsetBrush, null, bg, Bounds.Height / 2, Bounds.Height / 2);
        double frac = Math.Clamp(Percent / 100.0, 0.04, 1.0);
        var fill = new Rect(0, 0, Bounds.Width * frac, Bounds.Height);
        context.DrawRectangle(Fill, null, fill, Bounds.Height / 2, Bounds.Height / 2);
    }
}
