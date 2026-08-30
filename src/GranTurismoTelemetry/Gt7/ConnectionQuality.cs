namespace GranTurismoTelemetry.Gt7;

public enum QualityRating
{
    Poor,
    Fair,
    Good,
}

/// <summary>
/// Connection quality from packet rate and decode-error ratio.
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
}
