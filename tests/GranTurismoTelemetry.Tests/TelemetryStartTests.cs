using GranTurismoTelemetry.Gt7;
using GranTurismoTelemetry.Models;

namespace GranTurismoTelemetry.Tests;

public class TelemetryStartTests
{
    [Fact]
    public void StartWithoutSimulatorBeginsLanDiscover()
    {
        var telemetry = new TelemetryService();
        try
        {
            telemetry.Start(new AppSettings { UseSimulator = false, Ps5IP = "192.168.1.50" });
            Assert.True(telemetry.IsDiscovering);
            Assert.False(telemetry.UsingSimulator);
        }
        finally
        {
            telemetry.Disconnect();
        }
    }

    [Fact]
    public void StartWithSimulatorDoesNotDiscover()
    {
        var telemetry = new TelemetryService();
        try
        {
            telemetry.Start(new AppSettings { UseSimulator = true });
            Assert.True(telemetry.UsingSimulator);
            Assert.False(telemetry.IsDiscovering);
        }
        finally
        {
            telemetry.Disconnect();
        }
    }
}
