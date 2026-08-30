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

public enum ViewMode
{
    Auto,
    Live,
    Sessions,
    Layouts,
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
    [ObservableProperty] private IReadOnlyList<double> _speedTrace = [];
    [ObservableProperty] private WindowLayout _currentLayout = WindowLayout.Simple;
    [ObservableProperty] private ViewMode _viewMode = ViewMode.Auto;
    [ObservableProperty] private WindowLayout? _forcedLayout;
    [ObservableProperty] private bool _showDebug;
    [ObservableProperty] private bool _showConnectIp;
    [ObservableProperty] private SessionRecord _selectedSession = SampleSessions.All[0];
    [ObservableProperty] private string _trackFilter = "Track";
    [ObservableProperty] private string _carFilter = "Car";
    [ObservableProperty] private string _tabletTab = "Sessions";
    [ObservableProperty] private bool _useMph;
    [ObservableProperty] private HudMode _hudMode = HudMode.Simple;
    [ObservableProperty] private bool _showManualIp = true;

    private double _windowWidth = 1440;
    private double _windowHeight = 900;

    public MainViewModel() : this(AppSettings.Load(), new TelemetryService()) { }

    public MainViewModel(AppSettings settings, TelemetryService telemetry)
    {
        Settings = settings;
        Telemetry = telemetry;
        Settings.PropertyChanged += (_, e) =>
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
            SpeedTrace = Telemetry.SpeedTrace.ToArray();
            OnPropertyChanged(string.Empty);
        };
        ApplyHudMode(Settings.HudMode, persist: false);
        RecalcLayout();
    }

    public void Start() => Telemetry.Start(Settings);

    public void Shutdown() => Telemetry.Disconnect();

    public void UpdateWindowSize(double width, double height)
    {
        _windowWidth = width;
        _windowHeight = height;
        RecalcLayout();
    }

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
    private void ToggleManualIp()
    {
        ShowManualIp = !ShowManualIp;
    }

    [RelayCommand]
    private void UseSimulator()
    {
        Settings.UseSimulator = true;
        Telemetry.ApplySource(Settings);
        OnPropertyChanged(string.Empty);
    }

    [RelayCommand]
    private void ToggleSimulator()
    {
        Settings.UseSimulator = !Settings.UseSimulator;
        Telemetry.ApplySource(Settings);
        OnPropertyChanged(string.Empty);
    }

    [RelayCommand]
    private void SetPreset(string name)
    {
        Settings.Preset = LayoutPresetExtensions.FromRaw(name);
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
        ForcedLayout = HudMode switch
        {
            HudMode.Simple => WindowLayout.Simple,
            HudMode.PitWall => WindowLayout.PitWall,
            _ => WindowLayout.SideMonitor,
        };
        ViewMode = ViewMode.Auto;
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
    private void SetViewMode(string mode)
    {
        // Auto keeps the current HudMode. v2 does not pick layout by window width.
        ViewMode = mode switch
        {
            "Live" => ViewMode.Live,
            "Sessions" => ViewMode.Sessions,
            "Layouts" => ViewMode.Layouts,
            _ => ViewMode.Auto,
        };
        if (ViewMode == ViewMode.Sessions) TabletTab = "Sessions";
        if (ViewMode == ViewMode.Layouts) TabletTab = "Layouts";
        if (ViewMode == ViewMode.Live) TabletTab = "Live";
        RecalcLayout();
    }

    [RelayCommand]
    private void ForceLayout(string name)
    {
        ForcedLayout = name switch
        {
            "SideMonitor" => WindowLayout.SideMonitor,
            "Simple" => WindowLayout.Simple,
            "PitWall" => WindowLayout.PitWall,
            _ => null,
        };
        HudMode = ForcedLayout switch
        {
            WindowLayout.Simple => HudMode.Simple,
            WindowLayout.PitWall => HudMode.PitWall,
            WindowLayout.SideMonitor => HudMode.Driving,
            _ => HudMode,
        };
        ViewMode = ViewMode.Auto;
        RecalcLayout();
    }

    [RelayCommand]
    private void SetUnits(string unit)
    {
        UseMph = string.Equals(unit, "mph", StringComparison.OrdinalIgnoreCase);
        OnPropertyChanged(string.Empty);
    }

    [RelayCommand]
    private void OpenDebug()
    {
        ShowConnectIp = false;
        ShowDebug = true;
    }

    [RelayCommand]
    private void CloseDebug() => ShowDebug = false;

    [RelayCommand]
    private void SelectSession(SessionRecord? session)
    {
        if (session is null) return;
        SelectedSession = session;
        OnPropertyChanged(string.Empty);
    }

    [RelayCommand]
    private void SetTabletTab(string tab)
    {
        TabletTab = tab;
        ViewMode = tab switch
        {
            "Live" => ViewMode.Live,
            "Sessions" => ViewMode.Sessions,
            "Layouts" => ViewMode.Layouts,
            _ => ViewMode,
        };
        RecalcLayout();
    }

    private void RecalcLayout()
    {
        // Modes are explicit chips (Simple default). Window size must not steal
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
        OnPropertyChanged(nameof(IsLiveTab));
        OnPropertyChanged(nameof(IsSessionsTab));
        OnPropertyChanged(nameof(IsLayoutsTab));
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

    public bool IsLiveTab => TabletTab == "Live";
    public bool IsSessionsTab => TabletTab == "Sessions";
    public bool IsLayoutsTab => TabletTab == "Layouts";

    public bool IsMinimal => false;
    public bool ShowTraces => true;
    public bool IsDrivingPreset => HudMode == HudMode.Driving;
    public bool IsEndurancePreset => HudMode == HudMode.Simple;
    public bool IsMinimalPreset => HudMode == HudMode.PitWall;
    public bool WidgetsAvailable => true;

    public bool IsKph => !UseMph;
    public bool IsMph => UseMph;

    public bool IsRacing => Settings.UseSimulator || Packet.IsRacing;
    public string GearDisplay => IsRacing ? Packet.GearDisplay : "N";
    public double DisplaySpeed => IsRacing ? (UseMph ? Packet.SpeedMph : Packet.SpeedKph) : 0;
    public string SpeedText => DisplaySpeed.ToString("0", CultureInfo.InvariantCulture);
    public string SpeedUnit => UseMph ? "mph" : "km/h";
    public string SpeedWithUnit => $"{SpeedText} {SpeedUnit}";
    public string FuelPercentText => $"{Packet.FuelPercent:0}%";
    public string FuelPerLapText => string.Format(CultureInfo.InvariantCulture, "{0:0.0}%/lap", Telemetry.Session.FuelPercentPerLap);
    public string FuelLapsText => string.Format(CultureInfo.InvariantCulture, "{0:0.0} laps", Telemetry.Session.FuelLapsRemaining);
    public string FuelLapsRemainingText => string.Format(CultureInfo.InvariantCulture, "{0:0.0} laps remaining", Telemetry.Session.FuelLapsRemaining);
    public string FuelCombined => string.Format(CultureInfo.InvariantCulture, "{0:0}%  ·  {1:0.0} laps", Packet.FuelPercent, Telemetry.Session.FuelLapsRemaining);
    public string PredictedStopsText
    {
        get
        {
            int n = Telemetry.Session.PredictedStops;
            return n == 1 ? "1 stop" : $"{n} stops";
        }
    }
    public string FuelAvgMixText
    {
        get
        {
            double tankLiters = Packet.FuelCapacity <= 100 ? 24 : Packet.FuelCapacity;
            double remainingLiters = tankLiters * Packet.FuelPercent / 100.0;
            double avg = remainingLiters / Math.Max(Telemetry.Session.FuelLapsRemaining, 0.1);
            return string.Format(CultureInfo.InvariantCulture, "Avg {0:0.00} L / lap · Mix 2", avg);
        }
    }
    public double FuelPercent => Packet.FuelPercent;
    public double RpmFraction => IsRacing ? Packet.RpmFraction : 0;
    public string RpmLine => string.Format(CultureInfo.InvariantCulture, "RPM {0:N0} · DRS n/a", Packet.EngineRpm);
    public string DeltaText => Formatters.Delta(IsRacing ? Packet.LiveDeltaSeconds : 0);
    public IBrush DeltaBrush => GTTheme.DeltaBrush(IsRacing ? Packet.LiveDeltaSeconds : 0);
    public string LastLapText => "LAST " + Formatters.LapTime(Packet.LastLapMs > 0 ? Packet.LastLapMs : Telemetry.Session.LastLapMs);
    public string BestLapText => "BEST " + Formatters.LapTime(SessionBestMs);
    public string LastLapValue => Formatters.LapTime(Packet.LastLapMs > 0 ? Packet.LastLapMs : Telemetry.Session.LastLapMs);
    public string BestLapValue => Formatters.LapTime(SessionBestMs);
    public int SessionBestMs => Telemetry.Session.SessionBestMs > 0 ? Telemetry.Session.SessionBestMs : Packet.BestLapMs;
    public string LapsInMemoryText => $"{Telemetry.Session.LapsInMemory}";
    public string CarClassChip => Packet.CarClass;
    public string LapChip => $"Lap {Math.Max(Packet.CurrentLap, 1)}";
    public string PresetChip => HudModeLabel;
    public string DrivingStatus => $"{HudModeLabel} · Live";
    public bool IsLive => Connection.IsLive;
    public IBrush LivePillBackground => Connection.IsLive ? GTTheme.GreenBrush : GTTheme.InsetBrush;
    public IBrush LivePillForeground => Connection.IsLive ? GTTheme.PageBrush : GTTheme.MutedBrush;
    public string LiveBadgeText => Connection.IsLive ? "LIVE" : Connection.Kind == ConnectionKind.Waiting ? "WAIT" : "IDLE";
    public string StatusLabel => Connection.Label;
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

    public IBrush StatusBrush => Connection.Kind switch
    {
        ConnectionKind.Live => GTTheme.GreenBrush,
        ConnectionKind.Waiting => GTTheme.AmberBrush,
        ConnectionKind.Error => GTTheme.RedBrush,
        _ => GTTheme.MutedBrush,
    };
    public string ConnectButtonText =>
        Connection.IsLive && !Settings.UseSimulator ? "Connected" : "Connect";
    public bool CanConnect => !(Connection.IsLive && !Settings.UseSimulator);
    public bool CanDisconnect => !Settings.UseSimulator && Connection.Kind is ConnectionKind.Waiting or ConnectionKind.Live;

    public bool IsUdpActive => !Settings.UseSimulator && Connection.Kind is ConnectionKind.Waiting or ConnectionKind.Live or ConnectionKind.Error;
    public bool IsPs5Connected => !Settings.UseSimulator && (Connection.IsLive || !string.IsNullOrEmpty(Telemetry.ConnectedHost) && Connection.Kind != ConnectionKind.Error);
    public string FindPs5Copy => "Sends a heartbeat on the LAN and connects when GT7 answers.";
    public string FindPs5StatusLine =>
        Settings.UseSimulator ? "Simulator is on until you Find PS5 or Connect."
        : Telemetry.IsDiscovering ? "Looking for GT7 on this network…"
        : Connection.Kind == ConnectionKind.Waiting ? "Looking for GT7 on this network…"
        : Connection.IsLive ? FindPs5Copy
        : FindPs5Copy;
    public string ConnectedIp => Telemetry.ConnectedHost ?? Settings.Ps5IP;
    public string PeerSummary => $"Using the first PlayStation answering GT7 · {ConnectedIp}";
    public bool ShowConnectedRow => IsPs5Connected || Connection.IsLive && !Settings.UseSimulator;

    public QualityRating Quality => Telemetry.Quality;
    public string QualityLabel => ConnectionQuality.Label(Telemetry.Quality);
    public IBrush QualityBrush => ConnectionQuality.Brush(Telemetry.Quality);
    public double QualityFraction => ConnectionQuality.Fraction(Telemetry.Quality);
    public string QualityMetricsText =>
        string.Format(CultureInfo.InvariantCulture, "rx {0:N0} · dec {1:N0} · err {2:N0}",
            Telemetry.RawPackets, Telemetry.DecodedPackets, Telemetry.DecodeFailures);
    public string QualityStatusText => "Status: " + Connection.Label;

    public string TireFL => $"{Packet.TireTempFL:0}°";
    public string TireFR => $"{Packet.TireTempFR:0}°";
    public string TireRL => $"{Packet.TireTempRL:0}°";
    public string TireRR => $"{Packet.TireTempRR:0}°";
    public string TireFLC => $"{Packet.TireTempFL:0}°C";
    public string TireFRC => $"{Packet.TireTempFR:0}°C";
    public string TireRLC => $"{Packet.TireTempRL:0}°C";
    public string TireRRC => $"{Packet.TireTempRR:0}°C";
    public IBrush TireFLBrush => GTTheme.TireBrush(Packet.TireTempFL);
    public IBrush TireFRBrush => GTTheme.TireBrush(Packet.TireTempFR);
    public IBrush TireRLBrush => GTTheme.TireBrush(Packet.TireTempRL);
    public IBrush TireRRBrush => GTTheme.TireBrush(Packet.TireTempRR);
    public float TireFLTemp => Packet.TireTempFL;
    public float TireFRTemp => Packet.TireTempFR;
    public float TireRLTemp => Packet.TireTempRL;
    public float TireRRTemp => Packet.TireTempRR;
    public string TireFLStatus => TireStatus(Packet.TireTempFL);
    public string TireFRStatus => TireStatus(Packet.TireTempFR);
    public string TireRLStatus => TireStatus(Packet.TireTempRL);
    public string TireRRStatus => TireStatus(Packet.TireTempRR);

    public string LastSessionTitle => $"{SelectedSession.ShortTrack} - {SelectedSession.CarClass}";
    public string LastSessionBest => SampleSessions.All[0].BestLapLabel;
    public string LastSessionLaps => SampleSessions.All[0].Laps.ToString();
    public string HistoryLine1 => HistoryLine(SampleSessions.All[0]);
    public string HistoryLine2 => HistoryLine(SampleSessions.All[1]);
    public string HistoryLine3 => HistoryLine(SampleSessions.All[2]);
    private static string HistoryLine(SessionRecord s) =>
        $"{s.ShortTrack} - {s.BestLapLabel} - {s.Laps} laps";

    public IReadOnlyList<SessionRecord> Sessions => FilterSessions();
    public IReadOnlyList<string> TrackOptions { get; } =
        ["Track", .. SampleSessions.All.Select(s => s.Track).Distinct().OrderBy(x => x)];
    public IReadOnlyList<string> CarOptions { get; } =
        ["Car", .. SampleSessions.All.Select(s => s.CarClass).Distinct().OrderBy(x => x)];

    public string CompareTitle => $"{SelectedSession.Track} — compare vs Best";
    public string CompareSubtitle =>
        $"Lap {SelectedSession.Laps} ({Formatters.LapTime(SelectedSession.LastLapMs)}) vs Best ({SelectedSession.BestLapLabel})";
    public string CompareDelta => Formatters.Delta((SelectedSession.LastLapMs - SelectedSession.BestLapMs) / 1000.0);
    public IBrush CompareDeltaBrush =>
        GTTheme.DeltaBrush((SelectedSession.LastLapMs - SelectedSession.BestLapMs) / 1000.0);
    public IReadOnlyList<SectorCompare> Sectors => SelectedSession.Sectors;
    public IReadOnlyList<double> SessionDeltaTrace => SelectedSession.DeltaTrace;
    public IReadOnlyList<LapRow> LapRows => Telemetry.Session.Laps;
    public IReadOnlyList<LapRow> RecentLapRows => LapRows.Take(8).ToList();
    public IReadOnlyList<double> MappedSessionDelta
    {
        get
        {
            var src = DeltaTrace.Count > 0 ? DeltaTrace : SelectedSession.DeltaTrace;
            if (src.Count == 0) return [0.5, 0.5];
            return src.Select(v => 0.5 - v).ToArray();
        }
    }

    public string DecodedText => Telemetry.DecodedPackets.ToString();
    public string RawUdpText => Telemetry.RawPackets.ToString();
    public string DecodeFailsText => Telemetry.DecodeFailures.ToString();
    public string? LastDecodeError => Telemetry.LastDecodeError;
    public bool HasDecodeError => !string.IsNullOrEmpty(Telemetry.LastDecodeError);
    public bool ShowWaitingHint => !Settings.UseSimulator && Connection.Kind == ConnectionKind.Waiting;
    public bool PitWindowOpen => Telemetry.Session.WindowOpen || Packet.PitWindowOpen;
    public string WindowOpenText => PitWindowOpen ? "WINDOW OPEN" : "WINDOW CLOSED";
    public string PitWindowOpenText => PitWindowOpen ? "WINDOW OPEN" : "WINDOW CLOSED";
    public IBrush WindowOpenBrush => PitWindowOpen ? GTTheme.AmberBrush : GTTheme.MutedBrush;
    public string PitWindowHint
    {
        get
        {
            int start = Math.Max(Packet.CurrentLap, 1);
            return $"Pit window laps {start}–{start + 3}";
        }
    }

    public string GearFontSize => "88";

    public int ThrottlePercent => IsRacing ? (int)Math.Round(Packet.ThrottleNorm * 100) : 0;
    public int BrakePercent => IsRacing ? (int)Math.Round(Packet.BrakeNorm * 100) : 0;
    public string ThrottlePercentText => $"{ThrottlePercent}%";
    public string BrakePercentText => $"{BrakePercent}%";

    public string LapCounterText
    {
        get
        {
            int lap = Math.Max(Packet.CurrentLap, 1);
            int total = Math.Max(Packet.TotalLaps, 1);
            return $"LAP {lap} / {total}";
        }
    }

    public string LapHeaderLine => $"{LapCounterText}  ·  {LastLapValue}";

    public int CurrentSectorNumber =>
        Packet.LapProgress < 1.0 / 3.0 ? 1 : Packet.LapProgress < 2.0 / 3.0 ? 2 : 3;

    public string MapProgressText =>
        string.Format(CultureInfo.InvariantCulture, "Sector {0} · {1:0}% complete",
            CurrentSectorNumber, Packet.LapProgress * 100.0);

    public string RacingLineText => $"this session only";

    public string DeltaCaption => "this session only";

    public (double S1, double S2, double S3) LiveSectorDeltas
    {
        get
        {
            double d = Packet.LiveDeltaSeconds;
            double s2 = d * 0.55;
            double s3 = d * 0.50;
            double s1 = d - s2 - s3;
            return (s1, s2, s3);
        }
    }

    public string SectorDeltasLine
    {
        get
        {
            var (s1, s2, s3) = LiveSectorDeltas;
            return $"S1 {Formatters.Delta(s1)}   S2 {Formatters.Delta(s2)}   S3 {Formatters.Delta(s3)}";
        }
    }

    public string MapSector1Text => MapSectorLabel(0);
    public string MapSector2Text => MapSectorLabel(1);
    public string MapSector3Text => MapSectorLabel(2);
    public IBrush MapSector1Brush => SectorBrush(0);
    public IBrush MapSector2Brush => SectorBrush(1);
    public IBrush MapSector3Brush => SectorBrush(2);

    public bool IsTcsOn => Packet.Flags.HasFlag(SimulatorFlags.TcsActive);
    public bool IsAsmOn => Packet.Flags.HasFlag(SimulatorFlags.AsmActive);
    public string TcsLabel => IsTcsOn ? "TCS 2" : "TCS";
    public IBrush TcsForeground => IsTcsOn ? GTTheme.CyanBrush : GTTheme.MutedBrush;
    public IBrush TcsBorder => IsTcsOn ? GTTheme.CyanBrush : GTTheme.TrackBrush;
    public IBrush TcsBackground => IsTcsOn
        ? new SolidColorBrush(Color.Parse("#0F2733"))
        : GTTheme.InsetBrush;
    public IBrush AsmForeground => IsAsmOn ? GTTheme.CyanBrush : GTTheme.MutedBrush;
    public IBrush AsmBorder => IsAsmOn ? GTTheme.CyanBrush : GTTheme.TrackBrush;
    public IBrush AsmBackground => IsAsmOn
        ? new SolidColorBrush(Color.Parse("#0F2733"))
        : GTTheme.InsetBrush;

    private string MapSectorLabel(int index)
    {
        if (index < 0 || index >= Sectors.Count) return $"S{index + 1}";
        var s = Sectors[index];
        return $"S{s.Number} {Formatters.Sector(s.TimeSeconds)}  {Formatters.Delta(s.DeltaSeconds)}";
    }

    private IBrush SectorBrush(int index)
    {
        if (index < 0 || index >= Sectors.Count) return GTTheme.MutedBrush;
        var d = Sectors[index].DeltaSeconds;
        if (Math.Abs(d) < 0.0005) return GTTheme.MutedBrush;
        if (d < 0) return GTTheme.GreenBrush;
        return d < 0.08 ? GTTheme.AmberBrush : GTTheme.RedBrush;
    }

    private static string TireStatus(float temp) => temp switch
    {
        < 70f => "cold",
        < 90f => "in window",
        < 105f => "hot",
        _ => "over",
    };

    private IReadOnlyList<SessionRecord> FilterSessions()
    {
        return SampleSessions.All.Where(s =>
            (TrackFilter == "Track" || s.Track == TrackFilter) &&
            (CarFilter == "Car" || s.CarClass == CarFilter)).ToList();
    }

    partial void OnTrackFilterChanged(string value) => OnPropertyChanged(nameof(Sessions));
    partial void OnCarFilterChanged(string value) => OnPropertyChanged(nameof(Sessions));
}

public sealed class SectorRow
{
    public SectorRow(SectorCompare s)
    {
        Label = $"SECTOR {s.Number}";
        Time = Formatters.Sector(s.TimeSeconds);
        Delta = Formatters.Delta(s.DeltaSeconds);
        Brush = GTTheme.DeltaBrush(s.DeltaSeconds);
        double extra = Math.Min(0.4, Math.Abs(s.DeltaSeconds) * 0.8);
        Bar = Math.Abs(s.DeltaSeconds) < 0.0005 ? 0.48
            : s.DeltaSeconds < 0 ? 0.62
            : Math.Min(0.92, 0.48 + extra + 0.3);
    }

    public string Label { get; }
    public string Time { get; }
    public string Delta { get; }
    public IBrush Brush { get; }
    public double Bar { get; }
}
