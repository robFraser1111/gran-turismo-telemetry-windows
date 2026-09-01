using Avalonia.Threading;
using GranTurismoTelemetry.Models;

namespace GranTurismoTelemetry.Gt7;

public enum ConnectionKind
{
    Idle,
    Waiting,
    Live,
    Error,
}

public sealed class ConnectionState
{
    public ConnectionKind Kind { get; }
    public string Message { get; }

    public ConnectionState(ConnectionKind kind, string? message = null)
    {
        Kind = kind;
        Message = message ?? kind switch
        {
            ConnectionKind.Idle => "Idle",
            ConnectionKind.Waiting => "Waiting for telemetry",
            ConnectionKind.Live => "Receiving telemetry",
            ConnectionKind.Error => "Error",
            _ => kind.ToString(),
        };
    }

    public static ConnectionState Idle { get; } = new(ConnectionKind.Idle);
    public static ConnectionState Waiting { get; } = new(ConnectionKind.Waiting);
    public static ConnectionState Live { get; } = new(ConnectionKind.Live);
    public static ConnectionState Error(string message) => new(ConnectionKind.Error, message);

    public string Label => Message;
    public bool IsLive => Kind == ConnectionKind.Live;
}

/// <summary>
/// Owns the active telemetry source (simulator or UDP) and publishes UI-sampled
/// packets at ~30 Hz plus rolling traces.
/// </summary>
public sealed class TelemetryService
{
    public TelemetryPacket Packet { get; private set; } = TelemetryPacket.Idle;
    public ConnectionState State { get; private set; } = ConnectionState.Idle;
    public List<double> ThrottleTrace { get; } = [];
    public List<double> BrakeTrace { get; } = [];
    public List<double> DeltaTrace { get; } = [];
    public int RawPackets { get; private set; }
    public int DecodedPackets { get; private set; }
    public int DecodeFailures { get; private set; }
    public string? LastDecodeError { get; private set; }
    public SessionTracker Session { get; } = new();
    public string? ConnectedHost { get; private set; }
    public bool IsDiscovering { get; private set; }
    public bool UsingSimulator { get; private set; }
    public double PacketsPerSecond { get; private set; }
    public QualityRating Quality { get; private set; } = QualityRating.Poor;

    public event Action? Updated;

    private TelemetrySimulator? _simulator;
    private Gt7UdpClient? _udp;
    private long _lastPublishNs;
    private readonly Queue<long> _rxTicks = new();
    private const int MaxTrace = 120;

    public void Start(AppSettings settings) => ApplySource(settings);

    public void ApplySource(AppSettings settings)
    {
        if (settings.UseSimulator) StartSimulator();
        else if (!string.IsNullOrWhiteSpace(settings.Ps5IP)) StartUdp(settings.Ps5IP, discover: false);
        else Disconnect();
    }

    public void Connect(AppSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.Ps5IP))
        {
            _simulator?.Stop();
            _simulator = null;
            _udp?.Stop();
            _udp = null;
            UsingSimulator = false;
            State = ConnectionState.Error("Enter a PS5 IP address.");
            PostMain(() => Updated?.Invoke());
            return;
        }
        PostMain(() => State = ConnectionState.Waiting);
        StartUdp(settings.Ps5IP, discover: false);
    }

    public void FindPs5()
    {
        PostMain(() =>
        {
            State = ConnectionState.Waiting;
            IsDiscovering = true;
            ConnectedHost = null;
            Updated?.Invoke();
        });
        StartUdp(null, discover: true);
    }

    public void Disconnect()
    {
        _simulator?.Stop();
        _simulator = null;
        _udp?.Stop();
        _udp = null;
        PostMain(() =>
        {
            State = ConnectionState.Idle;
            IsDiscovering = false;
            UsingSimulator = false;
            Updated?.Invoke();
        });
    }

    private void StartSimulator()
    {
        _udp?.Stop();
        _udp = null;
        UsingSimulator = true;
        IsDiscovering = false;
        ConnectedHost = null;
        Session.Reset();
        Session.Seed(SampleSessions.All[0].LapRows);
        if (_simulator is null)
        {
            var sim = new TelemetrySimulator();
            sim.PacketGenerated += pkt => Ingest(pkt, fromSimulator: true);
            _simulator = sim;
        }
        PostMain(() =>
        {
            State = ConnectionState.Waiting;
            Quality = QualityRating.Good;
            PacketsPerSecond = 60;
            Updated?.Invoke();
        });
        _simulator.Start();
    }

    private void StartUdp(string? host, bool discover)
    {
        if (!discover && string.IsNullOrWhiteSpace(host))
        {
            _simulator?.Stop();
            _simulator = null;
            UsingSimulator = false;
            State = ConnectionState.Error("Enter a PS5 IP address.");
            PostMain(() => Updated?.Invoke());
            return;
        }

        _simulator?.Stop();
        _simulator = null;
        _udp?.Stop();
        UsingSimulator = false;
        Session.Reset();
        RawPackets = 0;
        DecodedPackets = 0;
        DecodeFailures = 0;
        LastDecodeError = null;
        lock (_rxTicks) { _rxTicks.Clear(); }

        var client = new Gt7UdpClient();
        client.PacketReceived += pkt => Ingest(pkt, fromSimulator: false);
        client.RawPacketReceived += _ => PostMain(() =>
        {
            RawPackets += 1;
            NoteRx();
            RecalcQuality();
            Updated?.Invoke();
        });
        client.DecodeFailed += reason => PostMain(() =>
        {
            DecodeFailures += 1;
            LastDecodeError = reason;
            RecalcQuality();
            Updated?.Invoke();
        });
        client.PeerLocked += ip => PostMain(() =>
        {
            ConnectedHost = ip;
            IsDiscovering = false;
            Updated?.Invoke();
        });
        client.Status += msg => PostMain(() =>
        {
            bool failed = msg.Contains("failed", StringComparison.OrdinalIgnoreCase)
                          || msg.Contains("Invalid", StringComparison.OrdinalIgnoreCase)
                          || msg.Contains("already in use", StringComparison.OrdinalIgnoreCase);
            if (failed) State = ConnectionState.Error(msg);
            else if (State.Kind is not ConnectionKind.Live and not ConnectionKind.Error)
                State = new ConnectionState(ConnectionKind.Waiting, msg);
            Updated?.Invoke();
        });
        _udp = client;
        PostMain(() =>
        {
            State = ConnectionState.Waiting;
            IsDiscovering = discover;
            ConnectedHost = discover ? null : host;
            Quality = QualityRating.Poor;
            PacketsPerSecond = 0;
            Updated?.Invoke();
        });
        if (discover) client.StartDiscover();
        else client.Start(host!);
    }

    private void Ingest(TelemetryPacket pkt, bool fromSimulator)
    {
        long now = System.Diagnostics.Stopwatch.GetTimestamp();
        if (_lastPublishNs != 0)
        {
            double elapsedMs = (now - _lastPublishNs) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            if (elapsedMs < 1000.0 / 30.0) return;
        }
        _lastPublishNs = now;

        TelemetryPacket next;
        if (fromSimulator)
        {
            next = pkt;
        }
        else
        {
            var prev = Packet;
            pkt.CarClass = prev.CarClass ?? "";
            double r = Math.Sqrt((double)pkt.PositionX * pkt.PositionX + (double)pkt.PositionZ * pkt.PositionZ);
            if (r > 1)
            {
                double ang = Math.Atan2(pkt.PositionZ, pkt.PositionX);
                if (ang < 0) ang += 2 * Math.PI;
                pkt.LapProgress = ang / (2 * Math.PI);
            }
            next = pkt;
        }

        PostMain(() =>
        {
            DecodedPackets += 1;
            Session.Ingest(next);
            next.LiveDeltaSeconds = Session.LiveDeltaSeconds;
            next.FuelLapsRemaining = Session.FuelLapsRemaining;
            next.PitWindowOpen = Session.WindowOpen;
            Packet = next;
            State = ConnectionState.Live;
            if (fromSimulator)
            {
                Quality = QualityRating.Good;
                PacketsPerSecond = 60;
            }
            bool racing = next.IsRacing || fromSimulator;
            Append(ThrottleTrace, racing ? next.ThrottleNorm : 0);
            Append(BrakeTrace, racing ? next.BrakeNorm : 0);
            Append(DeltaTrace, racing && !double.IsNaN(next.LiveDeltaSeconds) ? next.LiveDeltaSeconds : 0);
            Updated?.Invoke();
        });
    }

    private void NoteRx()
    {
        long t = Environment.TickCount64;
        lock (_rxTicks)
        {
            _rxTicks.Enqueue(t);
            while (_rxTicks.Count > 0 && t - _rxTicks.Peek() > 2000)
                _rxTicks.Dequeue();
            PacketsPerSecond = _rxTicks.Count / 2.0;
        }
    }

    private void RecalcQuality()
    {
        double errRatio = RawPackets <= 0 ? 1 : DecodeFailures / (double)RawPackets;
        Quality = ConnectionQuality.Classify(PacketsPerSecond, errRatio);
    }

    private static void Append(List<double> values, double value)
    {
        values.Add(value);
        if (values.Count > MaxTrace)
            values.RemoveAt(0);
    }

    private static void PostMain(Action block)
    {
        if (Dispatcher.UIThread.CheckAccess())
            block();
        else
            Dispatcher.UIThread.Post(block);
    }
}
