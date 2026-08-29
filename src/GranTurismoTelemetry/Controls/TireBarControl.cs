using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using GranTurismoTelemetry.Theme;

namespace GranTurismoTelemetry.Controls;

public sealed class TireBarControl : Control
{
    public static readonly StyledProperty<float> TemperatureProperty =
        AvaloniaProperty.Register<TireBarControl, float>(nameof(Temperature));

    static TireBarControl()
    {
        AffectsRender<TireBarControl>(TemperatureProperty);
    }

    public float Temperature
    {
        get => GetValue(TemperatureProperty);
        set => SetValue(TemperatureProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var bg = new Rect(0, 0, Bounds.Width, Bounds.Height);
        context.DrawRectangle(GTTheme.InsetBrush, null, bg, Bounds.Height / 2, Bounds.Height / 2);
        float frac = Math.Clamp((Temperature - 60f) / 60f, 0f, 1f);
        var fill = new Rect(0, 0, Bounds.Width * frac, Bounds.Height);
        context.DrawRectangle(GTTheme.TireBrush(Temperature), null, fill, Bounds.Height / 2, Bounds.Height / 2);
    }
}
