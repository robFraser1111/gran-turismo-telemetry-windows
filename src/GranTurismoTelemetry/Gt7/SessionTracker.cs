using GranTurismoTelemetry.Models;

namespace GranTurismoTelemetry.Gt7;

/// <summary>
/// In-memory this-app-stint lap table and derived fuel estimates.
/// Best/Last/delta come only from locally recorded laps — never GT7 packet PB/last.
/// Live delta is a sampled XYZ ghost of the fastest flying lap this stint, not polar LapProgress.
/// The first completed lap is never the ghost (out-lap / standing start).
/// </summary>
public sealed class SessionTracker
{
    public const int MaxLaps = 100; // this-session keep-limit for the pit-wall table

    /// <summary>Keep a sample when the car has moved this far in XZ from the last kept point.</summary>
    internal const double SampleSpacingM = 1.0;

    /// <summary>If the nearest ghost point is farther than this, hold the previous delta.</summary>
    internal const double MaxMatchDistanceM = 32.0;

    /// <summary>A later lap must cover at least this fraction of the longest completed path this stint.</summary>
    internal const double FlyerPathFraction = 0.85;

    private const int MaxSamplesPerLap = 4096;

    private readonly Func<long> _clock;
    private readonly List<LapRow> _laps = [];
    private readonly List<double> _fuelPerLap = [];
    private readonly List<GhostSample> _currentLap = [];
    private List<GhostSample> _ghost = [];
    private int _ghostBestMs;
    private int _ghostMatchIndex = -1;
    private double _heldDelta;
    private int _completedLapCount;
    private double _maxPathM;
    private int _lastCurrentLap;
    private bool _attached;
    private int _carCode;
    private bool _hasCarCode;
    private int _lastLapMsSeen;
    private double _fuelAtLapStart = -1;
    private long _lapStartTick;
    private long _pauseStartedTick;
    private bool _offTrack;

    public IReadOnlyList<LapRow> Laps => _laps;
    public int SessionBestMs { get; private set; }
    public int LastLapMs { get; private set; }
    public int LapsInMemory => _laps.Count;
    public double FuelPercentPerLap { get; private set; } = 2.1;
    public double FuelLapsRemaining { get; private set; }
    public int PredictedStops { get; private set; }
    public bool WindowOpen { get; private set; }
    public double LiveDeltaSeconds { get; private set; }

    /// <param name="clock">Optional millisecond clock (tests). Production uses <see cref="Environment.TickCount64"/>.</param>
    public SessionTracker(Func<long>? clock = null)
    {
        _clock = clock ?? (() => Environment.TickCount64);
        _lapStartTick = _clock();
    }

    public void Reset()
    {
        _laps.Clear();
        _fuelPerLap.Clear();
        _currentLap.Clear();
        _ghost = [];
        _ghostBestMs = 0;
        _ghostMatchIndex = -1;
        _heldDelta = 0;
        _completedLapCount = 0;
        _maxPathM = 0;
        _lastCurrentLap = 0;
        _attached = false;
        _carCode = 0;
        _hasCarCode = false;
        _lastLapMsSeen = 0;
        _fuelAtLapStart = -1;
        _lapStartTick = Now();
        SessionBestMs = 0;
        LastLapMs = 0;
        FuelPercentPerLap = 2.1;
        FuelLapsRemaining = 0;
        PredictedStops = 0;
        WindowOpen = false;
        LiveDeltaSeconds = 0;
        _pauseStartedTick = 0;
        _offTrack = false;
    }

    /// <summary>Seed completed laps (simulator demo) so the table matches a mid-session start.</summary>
    public void Seed(IEnumerable<LapRow> rows)
    {
        _laps.Clear();
        foreach (var row in rows.Take(MaxLaps))
            _laps.Add(row);
        Relabel();
        if (_laps.Count > 0)
        {
            LastLapMs = _laps[0].TimeMs;
            SessionBestMs = _laps.Where(l => l.TimeMs > 0).Select(l => l.TimeMs).DefaultIfEmpty(0).Min();
        }
    }

    public void Ingest(TelemetryPacket pkt)
    {
        if (!pkt.IsRacing)
        {
            LiveDeltaSeconds = 0;
            if (_pauseStartedTick == 0)
                _pauseStartedTick = Now();
            _offTrack = !pkt.Flags.HasFlag(SimulatorFlags.CarOnTrack)
                        || pkt.Flags.HasFlag(SimulatorFlags.LoadingOrProcessing);
            return;
        }

        // New stint: different car, or CurrentLap dropped (new race, typically 8->1).
        // After Reset this packet is a fresh attach. Do not store CurrentLap < 0.
        if (_hasCarCode && pkt.CarCode != _carCode)
            Reset();
        else if (_attached && pkt.CurrentLap >= 0 && pkt.CurrentLap < _lastCurrentLap)
            Reset();

        if (!_hasCarCode)
        {
            _carCode = pkt.CarCode;
            _hasCarCode = true;
        }

        long now = Now();
        if (_pauseStartedTick != 0)
        {
            _lapStartTick += now - _pauseStartedTick;
            _pauseStartedTick = 0;
        }
        if (_offTrack)
        {
            _lapStartTick = now;
            _offTrack = false;
            _currentLap.Clear();
            _ghostMatchIndex = _ghost.Count > 0 ? 0 : -1;
        }

        double fuelPct = pkt.FuelPercent;

        if (pkt.CurrentLap >= 0)
        {
            if (!_attached)
            {
                _attached = true;
                _lastCurrentLap = pkt.CurrentLap;
                _lastLapMsSeen = pkt.LastLapMs;
                _fuelAtLapStart = fuelPct;
                _lapStartTick = now;
                _currentLap.Clear();
            }
            else if (pkt.CurrentLap > _lastCurrentLap && pkt.LastLapMs > 0)
            {
                RecordLap(pkt.CurrentLap - 1, pkt.LastLapMs, fuelPct);
                _lastCurrentLap = pkt.CurrentLap;
                _lastLapMsSeen = pkt.LastLapMs;
                _fuelAtLapStart = fuelPct;
                _lapStartTick = now;
            }
            else if (pkt.LastLapMs > 0 && pkt.LastLapMs != _lastLapMsSeen && pkt.CurrentLap == _lastCurrentLap)
            {
                // Same lap index but a new last-lap time (some packets report this way).
                RecordLap(pkt.CurrentLap, pkt.LastLapMs, fuelPct);
                _lastLapMsSeen = pkt.LastLapMs;
                _fuelAtLapStart = fuelPct;
                _lapStartTick = now;
            }
        }

        if (_fuelPerLap.Count > 0)
            FuelPercentPerLap = _fuelPerLap.Average();
        else if (pkt.FuelLapsRemaining > 0.1 && fuelPct > 0)
            FuelPercentPerLap = Math.Clamp(fuelPct / pkt.FuelLapsRemaining, 0.4, 12);

        FuelLapsRemaining = FuelPercentPerLap > 0.05
            ? Math.Max(0, fuelPct / FuelPercentPerLap)
            : 0;

        int raceLeft = pkt.TotalLaps > 0 ? Math.Max(0, pkt.TotalLaps - Math.Max(pkt.CurrentLap, 0)) : 0;
        if (raceLeft <= 0)
        {
            PredictedStops = fuelPct < 50 && FuelLapsRemaining < 8 ? 1 : 0;
        }
        else
        {
            double need = raceLeft * FuelPercentPerLap;
            double extra = need - fuelPct;
            PredictedStops = extra <= 0.5 ? 0 : (int)Math.Ceiling(extra / 100.0);
        }

        WindowOpen = fuelPct is > 18 and < 50 || (FuelLapsRemaining is > 1.5 and < 8);

        double elapsed = (Now() - _lapStartTick) / 1000.0;
        AppendSample(pkt.PositionX, pkt.PositionZ, elapsed);
        LiveDeltaSeconds = ComputeLiveDelta(pkt, elapsed);
    }

    private double ComputeLiveDelta(TelemetryPacket pkt, double elapsed)
    {
        if (_ghost.Count < 2)
            return double.NaN;

        int idx = FindGhostMatch(pkt.PositionX, pkt.PositionZ, elapsed);
        if (idx < 0)
            return _heldDelta;

        double dx = _ghost[idx].X - pkt.PositionX;
        double dz = _ghost[idx].Z - pkt.PositionZ;
        if (Math.Sqrt(dx * dx + dz * dz) > MaxMatchDistanceM)
            return _heldDelta;

        _ghostMatchIndex = idx;
        _heldDelta = elapsed - _ghost[idx].ElapsedSec;
        return _heldDelta;
    }

    private void RecordLap(int number, int timeMs, double fuelPct)
    {
        if (timeMs <= 0) return;

        if (_currentLap.Count > 0)
            _currentLap[^1] = _currentLap[^1] with { ElapsedSec = timeMs / 1000.0 };

        double path = PathLengthM(_currentLap);
        if (path > _maxPathM)
            _maxPathM = path;
        _completedLapCount++;

        // Never promote the first completed lap (out-lap / standing start). Later laps
        // become the live ghost only if they look like a full-track flyer. Session best
        // time is independent — lap 1 can still be BEST in the table.
        bool eligibleFlyer = _completedLapCount > 1
            && _currentLap.Count >= 2
            && timeMs > 0
            && path >= FlyerPathFraction * _maxPathM;
        if (eligibleFlyer && (_ghost.Count == 0 || timeMs < _ghostBestMs))
        {
            _ghost = [.. _currentLap];
            _ghostBestMs = timeMs;
        }

        _currentLap.Clear();
        _ghostMatchIndex = _ghost.Count > 0 ? 0 : -1;

        if (_fuelAtLapStart > 0)
        {
            double used = _fuelAtLapStart - fuelPct;
            if (used is > 0.3 and < 25)
            {
                _fuelPerLap.Add(used);
                if (_fuelPerLap.Count > 12)
                    _fuelPerLap.RemoveAt(0);
            }
        }

        LastLapMs = timeMs;
        if (SessionBestMs <= 0 || timeMs < SessionBestMs)
            SessionBestMs = timeMs;

        bool isBest = timeMs == SessionBestMs;
        double? delta = isBest ? null : (timeMs - SessionBestMs) / 1000.0;
        var row = new LapRow(number, timeMs, delta, isBest);

        // Newest first. Drop duplicate lap numbers from a re-record.
        _laps.RemoveAll(l => l.Number == number);
        _laps.Insert(0, row);
        while (_laps.Count > MaxLaps)
            _laps.RemoveAt(_laps.Count - 1);

        Relabel();
    }

    private void Relabel()
    {
        if (_laps.Count == 0) return;
        int bestMs = _laps.Where(l => l.TimeMs > 0).Select(l => l.TimeMs).DefaultIfEmpty(0).Min();
        SessionBestMs = bestMs;
        for (int i = 0; i < _laps.Count; i++)
        {
            var src = _laps[i];
            bool isBest = bestMs > 0 && src.TimeMs == bestMs;
            double? delta = isBest ? null : (src.TimeMs - bestMs) / 1000.0;
            _laps[i] = src with { IsBest = isBest, DeltaSeconds = delta, IsLatest = i == 0 };
        }
    }

    private void AppendSample(float x, float z, double elapsedSec)
    {
        if (_currentLap.Count == 0)
        {
            _currentLap.Add(new GhostSample(x, z, elapsedSec));
            return;
        }

        var last = _currentLap[^1];
        double dx = x - last.X;
        double dz = z - last.Z;
        double distSq = dx * dx + dz * dz;
        if (distSq < SampleSpacingM * SampleSpacingM)
            return;

        if (_currentLap.Count >= MaxSamplesPerLap)
        {
            _currentLap[^1] = new GhostSample(x, z, elapsedSec);
            return;
        }

        _currentLap.Add(new GhostSample(x, z, elapsedSec));
    }

    private int FindGhostMatch(float x, float z, double elapsed)
    {
        int n = _ghost.Count;
        if (n == 0) return -1;

        int start;
        int count;
        if (_ghostMatchIndex < 0)
        {
            start = 0;
            count = n;
        }
        else
        {
            int fwd = Math.Min(n, Math.Max(32, n / 8));
            int back = Math.Min(n, Math.Max(8, n / 32));
            start = (_ghostMatchIndex - back + n) % n;
            count = Math.Min(n, back + fwd + 1);
        }

        int bestIdx = -1;
        double bestDistSq = double.MaxValue;
        for (int i = 0; i < count; i++)
        {
            int idx = (start + i) % n;
            var g = _ghost[idx];
            double dx = g.X - x;
            double dz = g.Z - z;
            double d = dx * dx + dz * dz;
            if (d < bestDistSq)
            {
                bestDistSq = d;
                bestIdx = idx;
            }
        }

        if (bestIdx < 0) return -1;

        // Start/finish is the same XZ: among similarly close points, prefer elapsed.
        double near = bestDistSq + 16.0;
        int chosen = bestIdx;
        double bestElapsedErr = Math.Abs(elapsed - _ghost[bestIdx].ElapsedSec);
        for (int i = 0; i < count; i++)
        {
            int idx = (start + i) % n;
            var g = _ghost[idx];
            double dx = g.X - x;
            double dz = g.Z - z;
            double d = dx * dx + dz * dz;
            if (d > near) continue;
            double err = Math.Abs(elapsed - g.ElapsedSec);
            if (err < bestElapsedErr)
            {
                bestElapsedErr = err;
                chosen = idx;
            }
        }

        return chosen;
    }

    private static double PathLengthM(List<GhostSample> samples)
    {
        double sum = 0;
        for (int i = 1; i < samples.Count; i++)
        {
            double dx = samples[i].X - samples[i - 1].X;
            double dz = samples[i].Z - samples[i - 1].Z;
            sum += Math.Sqrt(dx * dx + dz * dz);
        }
        return sum;
    }

    private long Now() => _clock();

    private readonly record struct GhostSample(float X, float Z, double ElapsedSec);
}
