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
        Assert.Equal(100, s.LapsInMemory);
        Assert.True(s.Laps[0].IsLatest);
        Assert.Equal(84_539, s.SessionBestMs);
    }

    [Fact]
    public void GameBestOnFirstPacketsDoesNotSetSessionBestOrDelta()
    {
        var s = new SessionTracker();
        s.Ingest(Racing(1, 0, 153_399, 50));
        s.Ingest(Racing(1, 0, 153_399, 50));

        Assert.Equal(0, s.SessionBestMs);
        Assert.Equal(0, s.LastLapMs);
        Assert.Equal(0, s.LiveDeltaSeconds);
        Assert.Equal(0, s.LapsInMemory);
        Assert.Empty(s.Laps);
    }

    [Fact]
    public void PacketLastLapMsWithoutRecordLapDoesNotBecomeSessionLast()
    {
        var s = new SessionTracker();
        s.Ingest(Racing(3, 90_000, 80_000, 50));
        s.Ingest(Racing(3, 90_000, 80_000, 49));

        Assert.Equal(0, s.LastLapMs);
        Assert.Equal(0, s.SessionBestMs);
        Assert.Equal(0, s.LapsInMemory);
    }

    [Fact]
    public void LocalFlyerIsSessionBestEvenIfPacketBestIsFasterOtherCarPb()
    {
        var s = new SessionTracker();
        s.Ingest(Racing(1, 0, 80_000, 50));
        s.Ingest(Racing(2, 90_000, 80_000, 47));

        Assert.Equal(1, s.LapsInMemory);
        Assert.Equal(90_000, s.LastLapMs);
        Assert.Equal(90_000, s.SessionBestMs);
        Assert.True(s.Laps[0].IsBest);
    }

    [Fact]
    public void CurrentLapDropResetsTableAndSessionBest()
    {
        var s = new SessionTracker();
        s.Ingest(Racing(5, 0, 84_000, 50));
        s.Ingest(Racing(6, 85_000, 84_000, 47));
        Assert.Equal(1, s.LapsInMemory);
        Assert.Equal(85_000, s.SessionBestMs);

        s.Ingest(Racing(1, 85_000, 80_000, 80));
        Assert.Equal(0, s.LapsInMemory);
        Assert.Equal(0, s.SessionBestMs);
        Assert.Equal(0, s.LastLapMs);

        s.Ingest(Racing(2, 90_000, 80_000, 77));
        Assert.Equal(1, s.LapsInMemory);
        Assert.Equal(90_000, s.LastLapMs);
        Assert.Equal(90_000, s.SessionBestMs);
    }

    [Fact]
    public void CarCodeChangeResetsStint()
    {
        var s = new SessionTracker();
        s.Ingest(Racing(2, 0, 84_000, 50, carCode: 10));
        s.Ingest(Racing(3, 85_000, 84_000, 47, carCode: 10));
        Assert.Equal(1, s.LapsInMemory);
        Assert.Equal(85_000, s.SessionBestMs);

        s.Ingest(Racing(3, 85_000, 80_000, 47, carCode: 20));
        Assert.Equal(0, s.LapsInMemory);
        Assert.Equal(0, s.SessionBestMs);
        Assert.Equal(0, s.LastLapMs);

        s.Ingest(Racing(4, 91_000, 80_000, 44, carCode: 20));
        Assert.Equal(1, s.LapsInMemory);
        Assert.Equal(91_000, s.SessionBestMs);
        Assert.Equal(91_000, s.LastLapMs);
    }

    [Fact]
    public void CurrentLapMinusOneThenOneRecordsCompletedLap()
    {
        var s = new SessionTracker();
        s.Ingest(Racing(1, 0, 0, 50));
        Assert.Equal(0, s.LapsInMemory);

        s.Ingest(Racing(-1, 85_000, 85_000, 48));
        Assert.Equal(0, s.LapsInMemory);
        Assert.Equal(0, s.LastLapMs);

        s.Ingest(Racing(1, 85_000, 85_000, 48));
        Assert.Equal(1, s.LapsInMemory);
        Assert.Equal(85_000, s.LastLapMs);
        Assert.Equal(85_000, s.SessionBestMs);
        Assert.True(s.Laps[0].IsBest);
    }

    [Fact]
    public void MenuThenRacingWithNewLastLapMsRecordsIfLapDidNotDrop()
    {
        var s = new SessionTracker();
        s.Ingest(Racing(1, 0, 0, 50));

        var menu = Racing(-1, 86_500, 0, 48);
        menu.Flags = SimulatorFlags.Paused;
        s.Ingest(menu);
        Assert.Equal(0, s.LiveDeltaSeconds);
        Assert.Equal(0, s.LapsInMemory);

        s.Ingest(Racing(1, 86_500, 86_500, 48));
        Assert.Equal(1, s.LapsInMemory);
        Assert.Equal(86_500, s.LastLapMs);
    }

    [Fact]
    public void NoGhostYetLiveDeltaIsZeroUntilLastLapExists()
    {
        var clock = new Clock(1_000_000);
        var none = new SessionTracker(() => clock.Now);
        var empty = PathPkt(1, lastMs: 0, bestMs: 0, x: 80, z: 0);
        empty.LiveDeltaSeconds = 9.99;
        none.Ingest(empty);
        Assert.Equal(0, none.LiveDeltaSeconds);

        var withGameBest = new SessionTracker(() => clock.Now);
        var pkt = PathPkt(5, lastMs: 85_000, bestMs: 84_000, x: 80, z: 0);
        pkt.LiveDeltaSeconds = 9.99;
        withGameBest.Ingest(pkt);
        Assert.Equal(0, withGameBest.LiveDeltaSeconds);
        Assert.Equal(0, withGameBest.SessionBestMs);
        Assert.Equal(0, withGameBest.LastLapMs);
        Assert.Equal(0, withGameBest.LapsInMemory);
    }

    [Fact]
    public void SamePathSlowerIsPositiveFasterIsNegative()
    {
        var clock = new Clock(1_000_000);
        var s = new SessionTracker(() => clock.Now);

        DriveArc(s, clock, lap: 1, lastMs: 0, bestMs: 80_000, startTick: clock.Now, durationMs: 80_000, points: 16);
        CompleteLap(s, clock, newLap: 2, lastMs: 80_000, bestMs: 80_000, tick: 1_000_000 + 80_000);

        Assert.Equal(1, s.LapsInMemory);
        Assert.Equal(80_000, s.SessionBestMs);

        // Halfway around a slower lap: +8s vs the 80s ghost (96s pace).
        DriveArc(s, clock, lap: 2, lastMs: 80_000, bestMs: 80_000, startTick: clock.Now, durationMs: 96_000, points: 9);
        Assert.True(s.LiveDeltaSeconds > 1.0, $"expected +ve delta, got {s.LiveDeltaSeconds}");

        // Restart a faster lap from the start/finish after completing this one.
        CompleteLap(s, clock, newLap: 3, lastMs: 96_000, bestMs: 80_000, tick: clock.Now + 1);
        DriveArc(s, clock, lap: 3, lastMs: 96_000, bestMs: 80_000, startTick: clock.Now, durationMs: 64_000, points: 9);
        Assert.True(s.LiveDeltaSeconds < -1.0, $"expected -ve delta, got {s.LiveDeltaSeconds}");
    }

    [Fact]
    public void FasterLapReplacesGhost()
    {
        var clock = new Clock(1_000_000);
        var s = new SessionTracker(() => clock.Now);

        DriveArc(s, clock, lap: 1, lastMs: 0, bestMs: 80_000, startTick: clock.Now, durationMs: 80_000, points: 16);
        CompleteLap(s, clock, newLap: 2, lastMs: 80_000, bestMs: 80_000, tick: 1_000_000 + 80_000);

        DriveArc(s, clock, lap: 2, lastMs: 80_000, bestMs: 80_000, startTick: clock.Now, durationMs: 64_000, points: 16);
        CompleteLap(s, clock, newLap: 3, lastMs: 64_000, bestMs: 64_000, tick: clock.Now);

        Assert.Equal(64_000, s.SessionBestMs);

        // Same positions as the original 80s lap should now be late vs the 64s ghost.
        DriveArc(s, clock, lap: 3, lastMs: 64_000, bestMs: 64_000, startTick: clock.Now, durationMs: 80_000, points: 9);
        Assert.True(s.LiveDeltaSeconds > 1.0, $"expected +ve vs new ghost, got {s.LiveDeltaSeconds}");
    }

    [Fact]
    public void PauseZerosDeltaAndDoesNotOverwriteGhost()
    {
        var clock = new Clock(1_000_000);
        var s = new SessionTracker(() => clock.Now);

        DriveArc(s, clock, lap: 1, lastMs: 0, bestMs: 80_000, startTick: clock.Now, durationMs: 80_000, points: 16);
        CompleteLap(s, clock, newLap: 2, lastMs: 80_000, bestMs: 80_000, tick: 1_000_000 + 80_000);

        DriveArc(s, clock, lap: 2, lastMs: 80_000, bestMs: 80_000, startTick: 1_000_000 + 80_000, durationMs: 88_000, points: 9);
        double before = s.LiveDeltaSeconds;
        Assert.True(before > 0.5, $"expected +ve vs ghost before pause, got {before}");
        int laps = s.LapsInMemory;
        float x = (float)(80 * Math.Cos(8 / 9.0 * 2 * Math.PI));
        float z = (float)(80 * Math.Sin(8 / 9.0 * 2 * Math.PI));

        var paused = PathPkt(99, 12_000, 0, 0, 0);
        paused.Flags = SimulatorFlags.CarOnTrack | SimulatorFlags.Paused;
        paused.FuelLevel = 0;
        s.Ingest(paused);
        clock.Now += 5_000;
        s.Ingest(paused);
        Assert.Equal(0, s.LiveDeltaSeconds);
        Assert.Equal(laps, s.LapsInMemory);
        Assert.Equal(80_000, s.SessionBestMs);

        s.Ingest(PathPkt(2, 80_000, 80_000, x, z));
        Assert.Equal(laps, s.LapsInMemory);
        Assert.Equal(80_000, s.SessionBestMs);
        Assert.InRange(s.LiveDeltaSeconds, before - 1.0, before + 1.0);
        Assert.True(s.LiveDeltaSeconds > 0.4);
    }

    [Fact]
    public void AgedOutTableBestDoesNotReplaceGhost()
    {
        var clock = new Clock(1_000_000);
        var s = new SessionTracker(() => clock.Now);

        DriveArc(s, clock, lap: 1, lastMs: 0, bestMs: 70_000, startTick: clock.Now, durationMs: 70_000, points: 16);
        CompleteLap(s, clock, newLap: 2, lastMs: 70_000, bestMs: 70_000, tick: clock.Now);

        int lastDriveLap = SessionTracker.MaxLaps + 2;
        for (int lap = 2; lap <= lastDriveLap; lap++)
        {
            long start = clock.Now;
            DriveArc(s, clock, lap, lastMs: lap == 2 ? 70_000 : 80_000, bestMs: 70_000, startTick: start, durationMs: 80_000, points: 8);
            CompleteLap(s, clock, newLap: lap + 1, lastMs: 80_000, bestMs: 70_000, tick: start + 80_000);
        }

        Assert.Equal(SessionTracker.MaxLaps, s.LapsInMemory);
        Assert.True(s.SessionBestMs > 70_000);

        DriveArc(s, clock, lap: lastDriveLap + 1, lastMs: 80_000, bestMs: 80_000, startTick: clock.Now, durationMs: 70_000, points: 9);
        Assert.True(s.LiveDeltaSeconds < 2.0, $"ghost should still be the 70s flyer, got {s.LiveDeltaSeconds}");
        Assert.InRange(s.LiveDeltaSeconds, -2.0, 2.0);
    }

    [Fact]
    public void FarOffLineHoldsPreviousDelta()
    {
        var clock = new Clock(1_000_000);
        var s = new SessionTracker(() => clock.Now);

        DriveArc(s, clock, lap: 1, lastMs: 0, bestMs: 80_000, startTick: clock.Now, durationMs: 80_000, points: 16);
        CompleteLap(s, clock, newLap: 2, lastMs: 80_000, bestMs: 80_000, tick: 1_000_000 + 80_000);

        DriveArc(s, clock, lap: 2, lastMs: 80_000, bestMs: 80_000, startTick: clock.Now, durationMs: 88_000, points: 9);
        double held = s.LiveDeltaSeconds;
        Assert.True(held > 0.5);

        s.Ingest(PathPkt(2, 80_000, 80_000, x: 800, z: 800));
        Assert.Equal(held, s.LiveDeltaSeconds);
    }

    private sealed class Clock
    {
        public long Now;
        public Clock(long now) => Now = now;
    }

    private static void DriveArc(SessionTracker s, Clock clock, int lap, int lastMs, int bestMs,
        long startTick, int durationMs, int points, double radius = 80)
    {
        for (int i = 0; i < points; i++)
        {
            double t = i / (double)points;
            clock.Now = startTick + (long)(t * durationMs);
            double ang = t * 2 * Math.PI;
            float x = (float)(radius * Math.Cos(ang));
            float z = (float)(radius * Math.Sin(ang));
            s.Ingest(PathPkt(lap, lastMs, bestMs, x, z));
        }
    }

    private static void CompleteLap(SessionTracker s, Clock clock, int newLap, int lastMs, int bestMs,
        long tick, double radius = 80)
    {
        clock.Now = tick;
        s.Ingest(PathPkt(newLap, lastMs, bestMs, (float)radius, 0));
    }

    private static TelemetryPacket PathPkt(int lap, int lastMs, int bestMs, float x, float z) => new()
    {
        CurrentLap = lap,
        LastLapMs = lastMs,
        BestLapMs = bestMs,
        FuelLevel = 50,
        FuelCapacity = 100,
        TotalLaps = 20,
        PositionX = x,
        PositionZ = z,
        Flags = SimulatorFlags.CarOnTrack,
    };

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

    private static TelemetryPacket Racing(int lap, int lastMs, int bestMs, double fuel, int carCode = 0) => new()
    {
        CurrentLap = lap,
        LastLapMs = lastMs,
        BestLapMs = bestMs,
        FuelLevel = (float)fuel,
        FuelCapacity = 100,
        TotalLaps = 20,
        LapProgress = 0.01,
        CarCode = carCode,
        Flags = SimulatorFlags.CarOnTrack,
    };
}
