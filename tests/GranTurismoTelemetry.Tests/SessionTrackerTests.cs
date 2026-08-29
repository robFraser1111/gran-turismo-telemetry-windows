using GranTurismoTelemetry.Gt7;
using GranTurismoTelemetry.Models;

namespace GranTurismoTelemetry.Tests;

public class SessionTrackerTests
{
    [Fact]
    public void RecordsLapWhenCurrentLapIncrements()
    {
        var s = new SessionTracker();
        s.Ingest(Pkt(lap: 3, lastMs: 84_881, bestMs: 84_539, fuel: 50, capacity: 100));
        s.Ingest(Pkt(lap: 4, lastMs: 84_539, bestMs: 84_539, fuel: 47.9, capacity: 100));

        Assert.Equal(1, s.LapsInMemory);
        Assert.Equal(84_539, s.LastLapMs);
        Assert.Equal(84_539, s.SessionBestMs);
        Assert.True(s.Laps[0].IsBest);
        Assert.True(s.Laps[0].IsLatest);
        Assert.Equal("BEST", s.Laps[0].DeltaLabel);
    }

    [Fact]
    public void SessionBestAndDeltaAreThisSessionOnly()
    {
        var s = new SessionTracker();
        s.Ingest(Pkt(2, 90_000, 80_000, 60, 100));
        s.Ingest(Pkt(3, 85_000, 80_000, 57, 100));
        s.Ingest(Pkt(4, 84_000, 80_000, 54, 100));

        Assert.Equal(2, s.LapsInMemory);
        Assert.Equal(84_000, s.SessionBestMs);
        Assert.Equal(84_000, s.Laps[0].TimeMs);
        Assert.True(s.Laps[0].IsBest);
        Assert.False(s.Laps[1].IsBest);
        Assert.NotNull(s.Laps[1].DeltaSeconds);
        Assert.InRange(s.Laps[1].DeltaSeconds!.Value, 0.99, 1.01);
    }

    [Fact]
    public void CapsTableAtEightNewestFirst()
    {
        var s = new SessionTracker();
        s.Ingest(Pkt(1, 90_000, 90_000, 80, 100));
        for (int lap = 2; lap <= 12; lap++)
            s.Ingest(Pkt(lap, 84_000 + lap, 84_000, 80 - lap, 100));

        Assert.Equal(8, s.LapsInMemory);
        Assert.Equal(11, s.Laps[0].Number);
        Assert.Equal(4, s.Laps[^1].Number);
    }

    [Fact]
    public void FuelPerLapAndLapsRemainingAreDerived()
    {
        var s = new SessionTracker();
        s.Ingest(Pkt(5, 85_000, 84_000, 42, 100));
        s.Ingest(Pkt(6, 84_500, 84_000, 39.9, 100));

        Assert.InRange(s.FuelPercentPerLap, 2.0, 2.3);
        Assert.InRange(s.FuelLapsRemaining, 17.0, 21.0);
        Assert.True(s.WindowOpen);
    }

    [Fact]
    public void PredictedStopsUseRemainingRaceDistance()
    {
        var s = new SessionTracker();
        var first = Pkt(10, 85_000, 84_000, 12, 100);
        first.TotalLaps = 20;
        s.Ingest(first);
        var second = Pkt(11, 84_800, 84_000, 4, 100);
        second.TotalLaps = 20;
        s.Ingest(second);

        Assert.True(s.PredictedStops >= 1);
    }

    [Fact]
    public void SeedThenRelabelMarksLatest()
    {
        var s = new SessionTracker();
        s.Seed(SampleSessions.All[0].LapRows);
        Assert.Equal(8, s.LapsInMemory);
        Assert.True(s.Laps[0].IsLatest);
        Assert.Equal(84_539, s.SessionBestMs);
    }

    private static TelemetryPacket Pkt(int lap, int lastMs, int bestMs, double fuel, float capacity) => new()
    {
        CurrentLap = lap,
        LastLapMs = lastMs,
        BestLapMs = bestMs,
        FuelLevel = (float)fuel,
        FuelCapacity = capacity,
        TotalLaps = 20,
        LapProgress = 0.01,
    };
}
