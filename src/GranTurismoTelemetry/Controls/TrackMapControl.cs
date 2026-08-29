using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using GranTurismoTelemetry.Theme;

namespace GranTurismoTelemetry.Controls;

public sealed class TrackMapControl : Control
{
    public static readonly StyledProperty<double> ProgressProperty =
        AvaloniaProperty.Register<TrackMapControl, double>(nameof(Progress));

    public static readonly StyledProperty<bool> LockedProperty =
        AvaloniaProperty.Register<TrackMapControl, bool>(nameof(Locked));

    public static readonly StyledProperty<bool> ThickStrokeProperty =
        AvaloniaProperty.Register<TrackMapControl, bool>(nameof(ThickStroke));

    public static readonly StyledProperty<bool> ColorBySectorProperty =
        AvaloniaProperty.Register<TrackMapControl, bool>(nameof(ColorBySector));

    static TrackMapControl()
    {
        AffectsRender<TrackMapControl>(ProgressProperty, LockedProperty, ThickStrokeProperty, ColorBySectorProperty);
    }

    public double Progress
    {
        get => GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    public bool Locked
    {
        get => GetValue(LockedProperty);
        set => SetValue(LockedProperty, value);
    }

    public bool ThickStroke
    {
        get => GetValue(ThickStrokeProperty);
        set => SetValue(ThickStrokeProperty, value);
    }

    public bool ColorBySector
    {
        get => GetValue(ColorBySectorProperty);
        set => SetValue(ColorBySectorProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        if (size.Width <= 1 || size.Height <= 1) return;

        var pts = Samples(size.Width, size.Height, 240);
        double stroke = ThickStroke ? 22 : 8;
        DrawPath(context, pts, 0, pts.Count, new Pen(GTTheme.TrackBrush, stroke)
        {
            LineJoin = PenLineJoin.Round,
            LineCap = PenLineCap.Round,
        });

        if (Locked) return;

        if (ColorBySector)
        {
            int a = pts.Count / 3;
            int b = pts.Count * 2 / 3;
            double slim = Math.Max(4, stroke * 0.22);
            DrawPath(context, pts, 0, a + 1, new Pen(GTTheme.GreenBrush, slim) { LineJoin = PenLineJoin.Round, LineCap = PenLineCap.Round });
            DrawPath(context, pts, a, b + 1, new Pen(GTTheme.AmberBrush, slim) { LineJoin = PenLineJoin.Round, LineCap = PenLineCap.Round });
            DrawPath(context, pts, b, pts.Count, new Pen(GTTheme.RedBrush, slim) { LineJoin = PenLineJoin.Round, LineCap = PenLineCap.Round });
        }

        var dash = new Pen(GTTheme.CyanBrush, ThickStroke ? 2 : 2.5)
        {
            LineJoin = PenLineJoin.Round,
            LineCap = PenLineCap.Round,
            DashStyle = new DashStyle([5, 6], 0),
        };
        DrawPath(context, pts, 0, pts.Count, dash);

        double p = Math.Clamp(Progress, 0, 1);
        int idx = (int)Math.Clamp(p * (pts.Count - 1), 0, pts.Count - 1);
        double r = ThickStroke ? 10 : 8;
        context.DrawEllipse(new SolidColorBrush(Color.FromArgb(56, 34, 211, 238)), null, pts[idx], r + 6, r + 6);
        context.DrawEllipse(GTTheme.CyanBrush, null, pts[idx], r, r);
    }

    private static void DrawPath(DrawingContext context, List<Point> pts, int start, int end, Pen pen)
    {
        if (end - start < 2) return;
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(pts[start], false);
            for (int i = start + 1; i < end && i < pts.Count; i++) ctx.LineTo(pts[i]);
        }
        context.DrawGeometry(null, pen, geo);
    }

    private static List<Point> Samples(double width, double height, int count)
    {
        var pts = new List<Point>(count);
        for (int i = 0; i < count; i++)
        {
            double t = i / (double)count * 2 * Math.PI;
            double x = 0.50 + 0.36 * Math.Cos(t) + 0.06 * Math.Cos(2 * t) - 0.04 * Math.Sin(3 * t);
            double y = 0.54 + 0.32 * Math.Sin(t) - 0.08 * Math.Sin(2 * t);
            pts.Add(new Point(x * width, y * height));
        }
        return pts;
    }
}
