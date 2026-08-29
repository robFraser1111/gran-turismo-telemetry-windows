using GranTurismoTelemetry.Gt7;
using GranTurismoTelemetry.Models;
using GranTurismoTelemetry.ViewModels;

namespace GranTurismoTelemetry.Tests;

public class QualityAndModeTests
{
    [Theory]
    [InlineData(60, 0.0, QualityRating.Good)]
    [InlineData(45, 0.05, QualityRating.Good)]
    [InlineData(20, 0.1, QualityRating.Fair)]
    [InlineData(12, 0.2, QualityRating.Fair)]
    [InlineData(5, 0.0, QualityRating.Poor)]
    [InlineData(50, 0.5, QualityRating.Poor)]
    [InlineData(0, 1, QualityRating.Poor)]
    public void ClassifiesPacketRateAndErrors(double pps, double err, QualityRating expected)
    {
        Assert.Equal(expected, ConnectionQuality.Classify(pps, err));
    }

    [Fact]
    public void DefaultHudModeIsSimpleEvenWhenMaximized()
    {
        var vm = new MainViewModel(new AppSettings(), new TelemetryService());
        Assert.Equal(HudMode.Simple, vm.HudMode);
        Assert.Equal(WindowLayout.Simple, vm.CurrentLayout);
        Assert.True(vm.IsSimple);
        Assert.True(vm.IsSimpleChip);
        Assert.False(vm.IsDrivingChip);
        Assert.False(vm.IsPitWallChip);

        vm.UpdateWindowSize(1440, 900);
        Assert.Equal(HudMode.Simple, vm.HudMode);
        Assert.True(vm.IsSimple);
        Assert.False(vm.IsPitWall);

        vm.UpdateWindowSize(1920, 1080);
        Assert.Equal(HudMode.Simple, vm.HudMode);
        Assert.True(vm.IsSimple);
    }

    [Fact]
    public void ChipsSwitchAllThreeModesRegardlessOfWidth()
    {
        var vm = new MainViewModel(new AppSettings(), new TelemetryService());
        vm.UpdateWindowSize(1440, 900);

        vm.SetHudModeCommand.Execute("Driving");
        Assert.Equal(HudMode.Driving, vm.HudMode);
        Assert.Equal(WindowLayout.SideMonitor, vm.CurrentLayout);
        Assert.True(vm.IsSideMonitor);
        Assert.True(vm.IsDrivingChip);
        Assert.False(vm.IsSimpleChip);
        Assert.False(vm.IsPitWallChip);

        vm.SetHudModeCommand.Execute("Pit wall");
        Assert.Equal(HudMode.PitWall, vm.HudMode);
        Assert.Equal(WindowLayout.PitWall, vm.CurrentLayout);
        Assert.True(vm.IsPitWall);
        Assert.True(vm.IsPitWallChip);

        vm.SetHudModeCommand.Execute("Simple");
        Assert.Equal(HudMode.Simple, vm.HudMode);
        Assert.Equal(WindowLayout.Simple, vm.CurrentLayout);
        Assert.True(vm.IsSimple);
        Assert.True(vm.IsSimpleChip);
    }

    [Fact]
    public void ResizeDoesNotStealForcedMode()
    {
        var vm = new MainViewModel(new AppSettings(), new TelemetryService());
        vm.SetHudModeCommand.Execute("Driving");
        vm.UpdateWindowSize(1440, 900);
        Assert.Equal(HudMode.Driving, vm.HudMode);
        Assert.True(vm.IsSideMonitor);

        vm.SetHudModeCommand.Execute("PitWall");
        vm.UpdateWindowSize(1280, 720);
        Assert.Equal(HudMode.PitWall, vm.HudMode);
        Assert.True(vm.IsPitWall);
    }

    [Fact]
    public void ReselectingCurrentHudModeStaysOnThatView()
    {
        var vm = new MainViewModel(new AppSettings(), new TelemetryService());
        vm.SetHudModeCommand.Execute("Simple");
        Assert.Equal(HudMode.Simple, vm.HudMode);
        Assert.True(vm.IsSimple);

        vm.SetHudModeCommand.Execute("Driving");
        vm.SetHudModeCommand.Execute("Driving");
        Assert.Equal(HudMode.Driving, vm.HudMode);
        Assert.True(vm.IsDrivingChip);
        Assert.True(vm.IsSideMonitor);
    }

    [Fact]
    public void HudModePersistsThroughAppSettings()
    {
        var settings = new AppSettings();
        var vm = new MainViewModel(settings, new TelemetryService());
        Assert.Equal("Simple", settings.HudMode);

        vm.SetHudModeCommand.Execute("PitWall");
        Assert.Equal("PitWall", settings.HudMode);
        Assert.Equal("PitWall", MainViewModel.HudModeToSettings(vm.HudMode));

        var restored = new MainViewModel(settings, new TelemetryService());
        Assert.Equal(HudMode.PitWall, restored.HudMode);
        Assert.Equal(WindowLayout.PitWall, restored.CurrentLayout);
        Assert.True(restored.IsPitWallChip);
        Assert.False(restored.IsSimpleChip);

        restored.SetHudModeCommand.Execute("Driving");
        var again = new MainViewModel(settings, new TelemetryService());
        Assert.Equal(HudMode.Driving, again.HudMode);
        Assert.True(again.IsDrivingChip);
    }

    [Fact]
    public void FindPs5CopyIsExact()
    {
        var vm = new MainViewModel(new AppSettings(), new TelemetryService());
        Assert.Equal("Sends a heartbeat on the LAN and connects when GT7 answers.", vm.FindPs5Copy);
    }

    [Fact]
    public void FindPs5TurnsSimulatorOff()
    {
        var settings = new AppSettings { UseSimulator = true };
        var vm = new MainViewModel(settings, new TelemetryService());
        try
        {
            vm.FindPs5Command.Execute(null);
            Assert.False(settings.UseSimulator);
            Assert.False(vm.Telemetry.UsingSimulator);
        }
        finally
        {
            vm.Shutdown();
        }
    }

    [Fact]
    public void ConnectTurnsSimulatorOff()
    {
        var settings = new AppSettings { UseSimulator = true, Ps5IP = "192.168.1.42" };
        var vm = new MainViewModel(settings, new TelemetryService());
        try
        {
            vm.ConnectCommand.Execute(null);
            Assert.False(settings.UseSimulator);
            Assert.False(vm.Telemetry.UsingSimulator);
        }
        finally
        {
            vm.Shutdown();
        }
    }

    [Fact]
    public void WidgetsStayAvailable()
    {
        var vm = new MainViewModel(new AppSettings(), new TelemetryService());
        Assert.True(vm.WidgetsAvailable);
        Assert.True(vm.ShowTraces);
        Assert.DoesNotContain("Pro", vm.DrivingStatus, StringComparison.Ordinal);
        Assert.DoesNotContain("lock", vm.HudModeLabel, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SimulatorAndIpAreOffUntilUserConnects()
    {
        var settings = new AppSettings();
        Assert.False(settings.UseSimulator);
        Assert.True(string.IsNullOrEmpty(settings.Ps5IP));
        var telemetry = new TelemetryService();
        var vm = new MainViewModel(settings, telemetry);
        try
        {
            vm.Start();
            Assert.False(telemetry.UsingSimulator);
            Assert.Equal(ConnectionKind.Idle, telemetry.State.Kind);

            vm.ConnectCommand.Execute(null);
            Assert.False(settings.UseSimulator);
            Assert.False(telemetry.UsingSimulator);
            Assert.Equal(ConnectionKind.Error, telemetry.State.Kind);
            Assert.Contains("IP", telemetry.State.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            vm.Shutdown();
        }
    }

    [Fact]
    public void SessionLapsDoNotFallBackToSampleHistory()
    {
        var vm = new MainViewModel(new AppSettings(), new TelemetryService());
        Assert.Empty(vm.LapRows);
        Assert.Empty(vm.RecentLapRows);
    }
}
