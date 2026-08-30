namespace GranTurismoTelemetry.Gt7;

/// <summary>
/// Emits synthetic telemetry resembling a lap of Deep Forest Raceway so the UI
/// works without a PS5. Values are plausible and live-looking (~60 Hz).
/// </summary>
public sealed class TelemetrySimulator
{
    public event Action<TelemetryPacket>? PacketGenerated;

    private volatile bool _running;
    private Thread? _thread;

    public void Start()
    {
        if (_running) return;
        _running = true;
        _thread = new Thread(RunLoop)
        {
            IsBackground = true,
            Name = "gt7-simulator",
        };
        _thread.Start();
    }

    public void Stop()
    {
        _running = false;
        _thread = null;
    }

    private void RunLoop()
    {
        int packetId = 0;
        int lap = 12;
        const int bestLapMs = 84_539;
        int lastLapMs = 84_881;
        long lapStart = Environment.TickCount64;
        const double lapSeconds = 84.539;
        double[] gearMax = [0, 60, 100, 140, 180, 220, 260, 300];
        var rng = Random.Shared;

        while (_running)
        {
            double elapsed = (Environment.TickCount64 - lapStart) / 1000.0;
            if (elapsed >= lapSeconds)
            {
                lastLapMs = (int)(elapsed * 1000);
                lapStart = Environment.TickCount64;
                elapsed = 0;
                lap += 1;
            }

            double t = elapsed / lapSeconds;
            double throttle, brake, clutch, targetSpeedKph;
            if (t < 0.35) { throttle = 1.0; brake = 0.0; clutch = 0.0; targetSpeedKph = 260.0; }
            else if (t < 0.45) { throttle = 0.0; brake = 0.9; clutch = 0.9; targetSpeedKph = 90.0; }
            else if (t < 0.65) { throttle = 0.75; brake = 0.0; clutch = 0.0; targetSpeedKph = 180.0; }
            else if (t < 0.75) { throttle = 0.1; brake = 0.6; clutch = 0.6; targetSpeedKph = 120.0; }
            else { throttle = 1.0; brake = 0.0; clutch = 0.0; targetSpeedKph = 280.0; }

            double jitter = rng.NextDouble() * 2.0;
            double speedKph = targetSpeedKph + Math.Sin(elapsed * 4) * 4 + jitter;
            double speedMps = speedKph / 3.6;

            double altitudeM = 140 + 70 * Math.Sin(t * 2 * Math.PI);
            double slopeRad = Math.Atan(0.10 * Math.Cos(t * 2 * Math.PI));
            double targetLatG = 1.4 * Math.Sin(t * 4 * Math.PI);
            double yawRate = targetLatG * 9.80665 / Math.Max(speedMps, 1.0);
            double rideHeightM = 0.078 + 0.006 * Math.Sin(elapsed * 3);
            const double tireRadiusM = 0.33;
            double lockSlip = brake > 0.5 ? -0.10 : 0.0;
            double spinSlip = throttle > 0.9 ? 0.08 : 0.0;
            double frontOmega = speedMps * (1 + lockSlip) / tireRadiusM;
            double rearOmega = speedMps * (1 + spinSlip) / tireRadiusM;

            int gear = 1;
            for (int g = 1; g < gearMax.Length; g++)
            {
                gear = g;
                if (speedKph <= gearMax[g]) break;
            }
            double rpm = 1500 + (speedKph / gearMax[gear]) * 7000 + rng.NextDouble() * 100;
            rpm = Math.Clamp(rpm, 900.0, 8800.0);

            double liveDelta = -0.342 + 0.22 * Math.Sin(t * 2 * Math.PI) + 0.05 * Math.Sin(t * 14 * Math.PI);
            double angle = t * 2 * Math.PI;
            float posX = (float)(420 * Math.Cos(angle));
            float posZ = (float)(260 * Math.Sin(angle));
            double fuelFrac = 0.42 - elapsed * 0.0004;
            float fuelLevel = (float)(Math.Max(0.08, fuelFrac) * 100);

            var pkt = new TelemetryPacket
            {
                PacketId = packetId,
                EngineRpm = (float)rpm,
                SpeedMps = (float)speedMps,
                PositionX = posX,
                PositionY = (float)altitudeM,
                PositionZ = posZ,
                VelocityX = (float)(speedMps * Math.Cos(slopeRad)),
                VelocityY = (float)(speedMps * Math.Sin(slopeRad)),
                AngularVelocityY = (float)yawRate,
                RideHeight = (float)rideHeightM,
                Throttle = Math.Clamp((int)(throttle * 255), 0, 255),
                Brake = Math.Clamp((int)(brake * 255), 0, 255),
                ClutchPedal = (float)Math.Clamp(clutch, 0.0, 1.0),
                CurrentGear = gear,
                SuggestedGear = 15,
                FuelCapacity = 100f,
                FuelLevel = fuelLevel,
                BoostKpa = (float)(100 + throttle * 60 + rng.NextDouble() * 3),
                OilPressure = (float)(4.5 + throttle * 0.8 + rng.NextDouble() * 0.1),
                WaterTemp = 88f,
                OilTemp = 108f,
                TireTempFL = (float)(81 + brake * 8 + rng.NextDouble() * 1.2),
                TireTempFR = (float)(83 + brake * 8 + rng.NextDouble() * 1.2),
                TireTempRL = (float)(101 + throttle * 6 + rng.NextDouble() * 1.2),
                TireTempRR = (float)(102 + throttle * 6 + rng.NextDouble() * 1.2),
                WheelSpeedFL = (float)frontOmega,
                WheelSpeedFR = (float)frontOmega,
                WheelSpeedRL = (float)rearOmega,
                WheelSpeedRR = (float)rearOmega,
                TireRadiusFL = (float)tireRadiusM,
                TireRadiusFR = (float)tireRadiusM,
                TireRadiusRL = (float)tireRadiusM,
                TireRadiusRR = (float)tireRadiusM,
                CurrentLap = lap,
                TotalLaps = 20,
                BestLapMs = bestLapMs,
                LastLapMs = lastLapMs,
                AlertMinRpm = 6800,
                AlertMaxRpm = 8500,
                CalcMaxSpeedKph = 320,
                Flags = SimulatorFlags.CarOnTrack | SimulatorFlags.InGear | SimulatorFlags.HasTurbo | SimulatorFlags.TcsActive,
                GearRatios = [3.5f, 2.4f, 1.8f, 1.4f, 1.1f, 0.9f, 0.7f, 0f],
                CarClass = "Gr.3",
                LapProgress = t,
                LiveDeltaSeconds = liveDelta,
                FuelLapsRemaining = Math.Max(1.5, fuelLevel / 6.77),
                PitWindowOpen = fuelFrac < 0.5 && fuelFrac > 0.18,
            };

            packetId += 1;
            PacketGenerated?.Invoke(pkt);

            try { Thread.Sleep(1000 / 60); }
            catch (ThreadInterruptedException) { break; }
        }
    }
}
