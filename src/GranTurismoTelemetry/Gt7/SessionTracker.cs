using GranTurismoTelemetry.Models;

namespace GranTurismoTelemetry.Gt7;

/// <summary>
/// In-memory this-app-stint lap table and derived fuel estimates.
/// Best/Last come only from locally recorded laps — never GT7 packet PB/last.
/// </summary>
public sealed class SessionTracker
{
    public const int MaxLaps = 100; // this-session keep-limit for the pit-wall table

    private readonly List<LapRow> _laps = [];
    private readonly List<double> _fuelPerLap = [];
    private int _lastCurrentLap;
    private bool _attached;
    private int _carCode;
    private bool _hasCarCode;
    private int _lastLapMsSeen;
    private double _fuelAtLapStart = -1;
    private long _lapStartTick = Environment.TickCount64;
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

    public void Reset()
    {
        _laps.Clear();
        _fuelPerLap.Clear();
        _lastCurrentLap = 0;
        _attached = false;
        _carCode = 0;
        _hasCarCode = false;
        _lastLapMsSeen = 0;
        _fuelAtLapStart = -1;
        _lapStartTick = Environment.TickCount64;
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
                _pauseStartedTick = Environment.TickCount64;
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

        if (_pauseStartedTick != 0)
        {
            _lapStartTick += Environment.TickCount64 - _pauseStartedTick;
            _pauseStartedTick = 0;
        }
        if (_offTrack)
        {
            _lapStartTick = Environment.TickCount64;
            _offTrack = false;
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
                _lapStartTick = Environment.TickCount64;
            }
            else if (pkt.CurrentLap > _lastCurrentLap && pkt.LastLapMs > 0)
            {
                RecordLap(pkt.CurrentLap - 1, pkt.LastLapMs, fuelPct);
                _lastCurrentLap = pkt.CurrentLap;
                _lastLapMsSeen = pkt.LastLapMs;
                _fuelAtLapStart = fuelPct;
                _lapStartTick = Environment.TickCount64;
            }
            else if (pkt.LastLapMs > 0 && pkt.LastLapMs != _lastLapMsSeen && pkt.CurrentLap == _lastCurrentLap)
            {
                // Same lap index but a new last-lap time (some packets report this way).
                RecordLap(pkt.CurrentLap, pkt.LastLapMs, fuelPct);
                _lastLapMsSeen = pkt.LastLapMs;
                _fuelAtLapStart = fuelPct;
                _lapStartTick = Environment.TickCount64;
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

        LiveDeltaSeconds = ComputeLiveDelta(pkt);
    }

    private double ComputeLiveDelta(TelemetryPacket pkt)
    {
        int best = SessionBestMs > 0 ? SessionBestMs : pkt.BestLapMs;
        if (best <= 0) return pkt.LiveDeltaSeconds;

        double bestSec = best / 1000.0;
        if (pkt.LapProgress > 0.02)
        {
            double elapsed = (Environment.TickCount64 - _lapStartTick) / 1000.0;
            return elapsed - bestSec * pkt.LapProgress;
        }

        if (LastLapMs > 0)
            return (LastLapMs - best) / 1000.0;

        return pkt.LiveDeltaSeconds;
    }

    private void RecordLap(int number, int timeMs, double fuelPct)
    {
        if (timeMs <= 0) return;

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
}
