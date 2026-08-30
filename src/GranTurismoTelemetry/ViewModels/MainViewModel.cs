using System.Globalization;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GranTurismoTelemetry.Gt7;
using GranTurismoTelemetry.Models;
using GranTurismoTelemetry.Theme;

namespace GranTurismoTelemetry.ViewModels;

public enum WindowLayout
{
    SideMonitor,
    PitWall,
    Simple,
}

public enum HudMode
{
    Driving,
    Simple,
    PitWall,
}

public partial class MainViewModel : ViewModelBase
{
    public AppSettings Settings { get; }
    public TelemetryService Telemetry { get; }

    [ObservableProperty] private TelemetryPacket _packet = TelemetryPacket.Idle;
    [ObservableProperty] private ConnectionState _connection = ConnectionState.Idle;
    [ObservableProperty] private IReadOnlyList<double> _throttleTrace = [];
    [ObservableProperty] private IReadOnlyList<double> _brakeTrace = [];
    [ObservableProperty] private IReadOnlyList<double> _deltaTrace = [];
    [ObservableProperty] private WindowLayout _currentLayout = WindowLayout.Simple;
    [ObservableProperty] private bool _showDebug;
    [ObservableProperty] private bool _showConnectIp;
    [ObservableProperty] private HudMode _hudMode = HudMode.Simple;

    public MainViewModel() : this(AppSettings.Load(), new TelemetryService()) { }

    public MainViewModel(AppSettings settings, TelemetryService telemetry)
    {
        Settings = settings;
        Telemetry = telemetry;
        Settings.PropertyChanged += (_, _) =>
        {
            OnPropertyChanged(string.Empty);
        };
        Telemetry.Updated += () =>
        {
            Packet = Telemetry.Packet;
            Connection = Telemetry.State;
            ThrottleTrace = Telemetry.ThrottleTrace.ToArray();
            BrakeTrace = Telemetry.BrakeTrace.ToArray();
            DeltaTrace = Telemetry.DeltaTrace.ToArray();
            OnPropertyChanged(string.Empty);
        };
        ApplyHudMode(Settings.HudMode, persist: false);
        RecalcLayout();
    }

    public void Start() => Telemetry.Start(Settings);

    public void Shutdown() => Telemetry.Disconnect();

    public void UpdateWindowSize(double width, double height) => RecalcLayout();

    [RelayCommand]
    private void Connect()
    {
        Settings.UseSimulator = false;
        Telemetry.Connect(Settings);
        OnPropertyChanged(string.Empty);
    }

    [RelayCommand]
    private void OpenConnectIp()
    {
        ShowDebug = false;
        ShowConnectIp = true;
    }

    [RelayCommand]
    private void CloseConnectIp() => ShowConnectIp = false;

    [RelayCommand]
    private void ConnectFromIp()
    {
        ShowConnectIp = true;
        Connect();
    }

    [RelayCommand]
    private void Disconnect()
    {
        Telemetry.Disconnect();
        OnPropertyChanged(string.Empty);
    }

    [RelayCommand]
    private void FindPs5()
    {
        Settings.UseSimulator = false;
        Telemetry.FindPs5();
        OnPropertyChanged(string.Empty);
    }

    [RelayCommand]
    private void SetHudMode(string name)
    {
        ApplyHudMode(name, persist: true);
        RecalcLayout();
    }

    private void ApplyHudMode(string? name, bool persist)
    {
        var next = name switch
        {
            "Simple" => HudMode.Simple,
            "PitWall" or "Pit wall" => HudMode.PitWall,
            "Driving" => HudMode.Driving,
            _ => persist ? HudMode : HudMode.Simple,
        };
        HudMode = next;
        // Always refresh ticks so clicking the current view cannot leave it unticked.
        OnPropertyChanged(nameof(IsSimpleChip));
        OnPropertyChanged(nameof(IsDrivingChip));
        OnPropertyChanged(nameof(IsPitWallChip));
        if (persist)
            Settings.HudMode = HudModeToSettings(HudMode);
    }

    public static string HudModeToSettings(HudMode mode) => mode switch
    {
        HudMode.Simple => "Simple",
        HudMode.PitWall => "PitWall",
        _ => "Driving",
    };

    [RelayCommand]
    private void OpenDebug()
    {
        ShowConnectIp = false;
        ShowDebug = true;
    }

    [RelayCommand]
    private void CloseDebug() => ShowDebug = false;

    private void RecalcLayout()
    {
        // Modes are explicit View-menu items (Simple default). Window size must not steal
        // Simple on a fresh / maximized window.
        WindowLayout next = HudMode switch
        {
            HudMode.Simple => WindowLayout.Simple,
            HudMode.PitWall => WindowLayout.PitWall,
            _ => WindowLayout.SideMonitor,
        };

        if (CurrentLayout != next)
            CurrentLayout = next;
        OnPropertyChanged(nameof(IsSideMonitor));
        OnPropertyChanged(nameof(IsPitWall));
        OnPropertyChanged(nameof(IsSimple));
        OnPropertyChanged(nameof(IsDrivingChip));
        OnPropertyChanged(nameof(IsSimpleChip));
        OnPropertyChanged(nameof(IsPitWallChip));
        OnPropertyChanged(nameof(HudModeLabel));
        OnPropertyChanged(nameof(DrivingStatus));
    }

    public bool IsSideMonitor => CurrentLayout == WindowLayout.SideMonitor;
    public bool IsPitWall => CurrentLayout == WindowLayout.PitWall;
    public bool IsSimple => CurrentLayout == WindowLayout.Simple;

    public bool IsDrivingChip => HudMode == HudMode.Driving;
    public bool IsSimpleChip => HudMode == HudMode.Simple;
    public bool IsPitWallChip => HudMode == HudMode.PitWall;
    public string HudModeLabel => HudMode switch
    {
        HudMode.Simple => "Simple",
        HudMode.PitWall => "Pit wall",
        _ => "Driving",
    };

    public bool ShowTraces => true;
    public bool WidgetsAvailable => true;

    public bool IsRacing => Settings.UseSimulator || Packet.IsRacing;
    public string GearDisplay => IsRacing ? Packet.GearDisplay : "N";
    public double DisplaySpeed => IsRacing ? Packet.SpeedKph : 0;
    public string SpeedText => DisplaySpeed.ToString("0", CultureInfo.InvariantCulture);
    public string SpeedUnit => "km/h";
    public string FuelPercentText => $"{Packet.FuelPercent:0}%";
    public string FuelPerLapText => string.Format(CultureInfo.InvariantCulture, "{0:0.0}%/lap", Telemetry.Session.FuelPercentPerLap);
    public string FuelLapsRemainingText => string.Format(CultureInfo.InvariantCulture, "{0:0.0} laps remaining", Telemetry.Session.FuelLapsRemaining);
    public string PredictedStopsText
    {
        get
        {
            int n = Telemetry.Session.PredictedStops;
            return n == 1 ? "1 stop" : $"{n} stops";
        }
    }
    public double FuelPercent => Packet.FuelPercent;
    public double RpmFraction => IsRacing ? Packet.RpmFraction : 0;
    public string DeltaText => Formatters.Delta(IsRacing ? Packet.LiveDeltaSeconds : 0);
    public IBrush DeltaBrush => GTTheme.DeltaBrush(IsRacing ? Packet.LiveDeltaSeconds : 0);
    public string LastLapText => "LAST " + Formatters.LapTime(Packet.LastLapMs > 0 ? Packet.LastLapMs : Telemetry.Session.LastLapMs);
    public string BestLapText => "BEST " + Formatters.LapTime(SessionBestMs);
    public string LastLapValue => Formatters.LapTime(Packet.LastLapMs > 0 ? Packet.LastLapMs : Telemetry.Session.LastLapMs);
    public string BestLapValue => Formatters.LapTime(SessionBestMs);
    public int SessionBestMs => Telemetry.Session.SessionBestMs > 0 ? Telemetry.Session.SessionBestMs : Packet.BestLapMs;
    public string LapsInMemoryText => $"{Telemetry.Session.LapsInMemory}";
    public string DrivingStatus => $"{HudModeLabel} · Live";
    public IBrush LivePillBackground => Connection.IsLive ? GTTheme.GreenBrush : GTTheme.InsetBrush;
    public IBrush LivePillForeground => Connection.IsLive ? GTTheme.PageBrush : GTTheme.MutedBrush;
    public string LiveBadgeText => Connection.IsLive ? "LIVE" : Connection.Kind == ConnectionKind.Waiting ? "WAIT" : "IDLE";
    public string ConnectOverlayStatus =>
        Connection.Kind == ConnectionKind.Error ? "Failed"
        : Connection.IsLive && !Settings.UseSimulator ? "Connected"
        : Connection.Kind == ConnectionKind.Waiting && !Settings.UseSimulator ? "Connecting…"
        : "Ready";
    public IBrush ConnectOverlayBrush =>
        Connection.Kind == ConnectionKind.Error ? GTTheme.RedBrush
        : Connection.IsLive && !Settings.UseSimulator ? GTTheme.GreenBrush
        : GTTheme.MutedBrush;
    public string ConnectIpDetail =>
        Connection.Kind == ConnectionKind.Error ? Connection.Message
        : Connection.IsLive && !Settings.UseSimulator ? $"Receiving from {ConnectedIp}."
        : Connection.Kind == ConnectionKind.Waiting && !Settings.UseSimulator ? $"Listening at {Settings.Ps5IP}."
        : "Enter the PlayStation IP and tap Connect.";

    public string ConnectButtonText =>
        Connection.IsLive && !Settings.UseSimulator ? "Connected" : "Connect";
    public bool CanConnect => !(Connection.IsLive && !Settings.UseSimulator);
    public bool CanDisconnect => !Settings.UseSimulator && Connection.Kind is ConnectionKind.Waiting or ConnectionKind.Live;

    public string FindPs5Copy => "Sends a heartbeat on the LAN and connects when GT7 answers.";
    public string ConnectedIp => Telemetry.ConnectedHost ?? Settings.Ps5IP;

    public string QualityMetricsText =>
        string.Format(CultureInfo.InvariantCulture, "rx {0:N0} · dec {1:N0} · err {2:N0}",
            Telemetry.RawPackets, Telemetry.DecodedPackets, Telemetry.DecodeFailures);

    public string TireFLC => $"{Packet.TireTempFL:0}°C";
    public string TireFRC => $"{Packet.TireTempFR:0}°C";
    public string TireRLC => $"{Packet.TireTempRL:0}°C";
    public string TireRRC => $"{Packet.TireTempRR:0}°C";
    public IBrush TireFLBrush => GTTheme.TireBrush(Packet.TireTempFL);
    public IBrush TireFRBrush => GTTheme.TireBrush(Packet.TireTempFR);
    public IBrush TireRLBrush => GTTheme.TireBrush(Packet.TireTempRL);
    public IBrush TireRRBrush => GTTheme.TireBrush(Packet.TireTempRR);

    public IReadOnlyList<LapRow> LapRows => Telemetry.Session.Laps;
    public IReadOnlyList<LapRow> RecentLapRows => LapRows.Take(8).ToList();

    public string DecodedText => Telemetry.DecodedPackets.ToString();
    public string RawUdpText => Telemetry.RawPackets.ToString();
    public string DecodeFailsText => Telemetry.DecodeFailures.ToString();
    public string? LastDecodeError => Telemetry.LastDecodeError;
    public bool HasDecodeError => !string.IsNullOrEmpty(Telemetry.LastDecodeError);

    public int ThrottlePercent => IsRacing ? (int)Math.Round(Packet.ThrottleNorm * 100) : 0;
    public int BrakePercent => IsRacing ? (int)Math.Round(Packet.BrakeNorm * 100) : 0;
    public string ThrottlePercentText => $"{ThrottlePercent}%";
    public string BrakePercentText => $"{BrakePercent}%";
}
