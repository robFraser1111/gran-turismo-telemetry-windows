using System.Collections;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using GranTurismoTelemetry.Theme;

namespace GranTurismoTelemetry.Controls;

public sealed class TraceLineControl : Control
{
    public static readonly StyledProperty<IEnumerable?> ValuesProperty =
        AvaloniaProperty.Register<TraceLineControl, IEnumerable?>(nameof(Values));

    public static readonly StyledProperty<IBrush> StrokeProperty =
        AvaloniaProperty.Register<TraceLineControl, IBrush>(nameof(Stroke), GTTheme.CyanBrush);

    public static readonly StyledProperty<double> ThicknessProperty =
        AvaloniaProperty.Register<TraceLineControl, double>(nameof(Thickness), 2);

    public static readonly StyledProperty<bool> ShowZeroLineProperty =
        AvaloniaProperty.Register<TraceLineControl, bool>(nameof(ShowZeroLine));

    public static readonly StyledProperty<bool> CenterZeroProperty =
        AvaloniaProperty.Register<TraceLineControl, bool>(nameof(CenterZero));

    public static readonly StyledProperty<bool> FilledProperty =
        AvaloniaProperty.Register<TraceLineControl, bool>(nameof(Filled));

    static TraceLineControl()
    {
        AffectsRender<TraceLineControl>(
            ValuesProperty, StrokeProperty, ThicknessProperty,
            ShowZeroLineProperty, CenterZeroProperty, FilledProperty);
    }

    public IEnumerable? Values
    {
        get => GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public IBrush Stroke
    {
        get => GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public double Thickness
    {
        get => GetValue(ThicknessProperty);
        set => SetValue(ThicknessProperty, value);
    }

    public bool ShowZeroLine
    {
        get => GetValue(ShowZeroLineProperty);
        set => SetValue(ShowZeroLineProperty, value);
    }

    public bool CenterZero
    {
        get => GetValue(CenterZeroProperty);
        set => SetValue(CenterZeroProperty, value);
    }

    public bool Filled
    {
        get => GetValue(FilledProperty);
        set => SetValue(FilledProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        if (size.Width <= 1 || size.Height <= 1) return;

        if (ShowZeroLine)
        {
            var zpen = new Pen(GTTheme.TrackBrush, 1);
            double y = size.Height / 2;
            context.DrawLine(zpen, new Point(0, y), new Point(size.Width, y));
        }

        var pts = ToList(Values);
        if (pts.Count < 2) pts = [0, 0];

        double lo = 0, hi = 1;
        if (CenterZero)
        {
            double maxAbs = 0.2;
            foreach (var v in pts) maxAbs = Math.Max(maxAbs, Math.Abs(v));
            lo = -maxAbs;
            hi = maxAbs;
        }

        double span = Math.Max(0.0001, hi - lo);
        var linePts = new List<Point>(pts.Count);
        for (int i = 0; i < pts.Count; i++)
        {
            double x = size.Width * i / Math.Max(1, pts.Count - 1);
            double n = Math.Clamp((pts[i] - lo) / span, 0, 1);
            double y = size.Height * (1 - n);
            linePts.Add(new Point(x, y));
        }

        if (Filled)
        {
            var fill = FillBrush(Stroke);
            var area = new StreamGeometry();
            using (var ctx = area.Open())
            {
                ctx.BeginFigure(new Point(linePts[0].X, size.Height), true);
                foreach (var p in linePts) ctx.LineTo(p);
                ctx.LineTo(new Point(linePts[^1].X, size.Height));
                ctx.EndFigure(true);
            }
            context.DrawGeometry(fill, null, area);
        }

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(linePts[0], false);
            for (int i = 1; i < linePts.Count; i++) ctx.LineTo(linePts[i]);
        }

        var pen = new Pen(Stroke, Thickness) { LineJoin = PenLineJoin.Round, LineCap = PenLineCap.Round };
        context.DrawGeometry(null, pen, geometry);
    }

    private static IBrush FillBrush(IBrush stroke)
    {
        if (stroke is SolidColorBrush scb)
            return new SolidColorBrush(Color.FromArgb(38, scb.Color.R, scb.Color.G, scb.Color.B));
        return new SolidColorBrush(Color.FromArgb(38, 34, 211, 238));
    }

    private static List<double> ToList(IEnumerable? values)
    {
        var list = new List<double>();
        if (values is null) return list;
        foreach (var item in values)
        {
            if (item is double d) list.Add(d);
            else if (item is float f) list.Add(f);
            else if (item is IConvertible c) list.Add(c.ToDouble(null));
        }
        return list;
    }
}
