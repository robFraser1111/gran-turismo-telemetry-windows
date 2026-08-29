using Avalonia.Media;
using GranTurismoTelemetry.Theme;

namespace GranTurismoTelemetry.Gt7;

public enum QualityRating
{
    Poor,
    Fair,
    Good,
}

/// <summary>
/// Connection quality from packet rate and decode-error ratio. Shown on Connect UI only.
/// </summary>
public static class ConnectionQuality
{
    public static QualityRating Classify(double packetsPerSecond, double errorRatio)
    {
        errorRatio = Math.Clamp(errorRatio, 0, 1);
        if (packetsPerSecond >= 40 && errorRatio < 0.08) return QualityRating.Good;
        if (packetsPerSecond >= 12 && errorRatio < 0.25) return QualityRating.Fair;
        return QualityRating.Poor;
    }

    public static double Fraction(QualityRating rating) => rating switch
    {
        QualityRating.Good => 0.88,
        QualityRating.Fair => 0.55,
        _ => 0.28,
    };

    public static string Label(QualityRating rating) => rating.ToString();

    public static IBrush Brush(QualityRating rating) => rating switch
    {
        QualityRating.Good => GTTheme.GreenBrush,
        QualityRating.Fair => GTTheme.AmberBrush,
        _ => GTTheme.RedBrush,
    };
}
