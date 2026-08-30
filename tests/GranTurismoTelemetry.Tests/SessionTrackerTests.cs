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
    public void CapsTableAtMaxLapsNewestFirst()
    {
        var s = new SessionTracker();
        s.Ingest(Racing(1, 90_000, 90_000, 80));
        int lastRecorded = SessionTracker.MaxLaps + 3;
        for (int lap = 2; lap <= lastRecorded + 1; lap++)
            s.Ingest(Racing(lap, 84_000 + lap, 84_000, 80));

        Assert.Equal(SessionTracker.MaxLaps, s.LapsInMemory);
        Assert.Equal(lastRecorded, s.Laps[0].Number);
        Assert.Equal(lastRecorded - SessionTracker.MaxLaps + 1, s.Laps[^1].Number);
    }

    [Fact]
    public void MaxLapsPlusOneEvictsOldestAndRelabelMinsRemaining()
    {
        var s = new SessionTracker();
        // Lap 1 is uniquely fast so it is session best; it will be the first evicted.
        s.Ingest(Racing(1, 90_000, 90_000, 80));
        s.Ingest(Racing(2, 70_000, 70_000, 79));
        Assert.Equal(1, s.LapsInMemory);
        Assert.Equal(70_000, s.SessionBestMs);

        for (int lap = 3; lap <= SessionTracker.MaxLaps + 1; lap++)
            s.Ingest(Racing(lap, 90_000, 70_000, 78));

        Assert.Equal(SessionTracker.MaxLaps, s.LapsInMemory);
        Assert.Equal(1, s.Laps[^1].Number);
        Assert.Equal(70_000, s.SessionBestMs);

        s.Ingest(Racing(SessionTracker.MaxLaps + 2, 88_000, 70_000, 77));

        Assert.Equal(SessionTracker.MaxLaps, s.LapsInMemory);
        Assert.Equal(SessionTracker.MaxLaps + 1, s.Laps[0].Number);
        Assert.Equal(2, s.Laps[^1].Number);
        Assert.DoesNotContain(s.Laps, l => l.Number == 1);
        Assert.Equal(88_000, s.SessionBestMs);
        Assert.True(s.Laps[0].IsBest);
        Assert.All(s.Laps.Skip(1), l => Assert.False(l.IsBest));
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
    public void PauseFreezesSessionWhileJunkPacketsKeepArriving()
    {
        var s = new SessionTracker();
        s.Ingest(Racing(5, 85_000, 84_000, 42));
        s.Ingest(Racing(6, 84_500, 84_000, 39.9));

        int laps = s.LapsInMemory;
        int best = s.SessionBestMs;
        int last = s.LastLapMs;
        double fuelPerLap = s.FuelPercentPerLap;
        double fuelLeft = s.FuelLapsRemaining;
        int stops = s.PredictedStops;
        bool window = s.WindowOpen;
        Assert.True(laps > 0);

        var paused = Racing(99, 12_000, 0, 0);
        paused.Flags = SimulatorFlags.CarOnTrack | SimulatorFlags.Paused;
        s.Ingest(paused);
        s.Ingest(paused);

        Assert.Equal(laps, s.LapsInMemory);
        Assert.Equal(best, s.SessionBestMs);
        Assert.Equal(last, s.LastLapMs);
        Assert.Equal(fuelPerLap, s.FuelPercentPerLap);
        Assert.Equal(fuelLeft, s.FuelLapsRemaining);
        Assert.Equal(stops, s.PredictedStops);
        Assert.Equal(window, s.WindowOpen);
        Assert.Equal(0, s.LiveDeltaSeconds);

        var resume = Racing(6, 84_500, 84_000, 39.9);
        resume.LapProgress = 0.4;
        s.Ingest(resume);
        Assert.Equal(laps, s.LapsInMemory);
        Assert.Equal(best, s.SessionBestMs);
        Assert.Equal(last, s.LastLapMs);
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

    private static TelemetryPacket Racing(int lap, int lastMs, int bestMs, double fuel) => new()
    {
        CurrentLap = lap,
        LastLapMs = lastMs,
        BestLapMs = bestMs,
        FuelLevel = (float)fuel,
        FuelCapacity = 100,
        TotalLaps = 20,
        LapProgress = 0.01,
        Flags = SimulatorFlags.CarOnTrack,
    };
}
