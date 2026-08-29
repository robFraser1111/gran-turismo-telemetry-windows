using System.Buffers.Binary;

namespace GranTurismoTelemetry.Gt7;

[Flags]
public enum SimulatorFlags : ushort
{
    None = 0,
    CarOnTrack = 1 << 0,
    Paused = 1 << 1,
    LoadingOrProcessing = 1 << 2,
    InGear = 1 << 3,
    HasTurbo = 1 << 4,
    RevLimiter = 1 << 5,
    HandBrakeActive = 1 << 6,
    LightsActive = 1 << 7,
    HighBeamActive = 1 << 8,
    LowBeamActive = 1 << 9,
    AsmActive = 1 << 10,
    TcsActive = 1 << 11,
}

public sealed class PacketParseException : Exception
{
    public PacketParseException(string message) : base(message) { }
}

/// <summary>
/// Decoded Gran Turismo 7 telemetry packet (296-byte "A" packet).
/// Field offsets match https://github.com/robFraser1111/gran-turismo-telemetry
/// plus UI-only fields (track, delta, fuel laps) filled by the simulator or ingest.
/// </summary>
public sealed class TelemetryPacket
{
    public const int MinimumSize = 0x128;
    public const uint Magic = 0x47375330u;

    public int PacketId { get; set; }
    public float PositionX { get; set; }
    public float PositionY { get; set; }
    public float PositionZ { get; set; }
    public float VelocityX { get; set; }
    public float VelocityY { get; set; }
    public float VelocityZ { get; set; }
    public float RotationPitch { get; set; }
    public float RotationYaw { get; set; }
    public float RotationRoll { get; set; }
    public float Heading { get; set; }
    public float AngularVelocityX { get; set; }
    public float AngularVelocityY { get; set; }
    public float AngularVelocityZ { get; set; }
    public float RideHeight { get; set; }
    public float EngineRpm { get; set; }
    public float FuelLevel { get; set; }
    public float FuelCapacity { get; set; }
    public float SpeedMps { get; set; }
    public float BoostKpa { get; set; }
    public float OilPressure { get; set; }
    public float WaterTemp { get; set; }
    public float OilTemp { get; set; }
    public float TireTempFL { get; set; }
    public float TireTempFR { get; set; }
    public float TireTempRL { get; set; }
    public float TireTempRR { get; set; }
    public int CurrentLap { get; set; }
    public int TotalLaps { get; set; }
    public int BestLapMs { get; set; }
    public int LastLapMs { get; set; }
    public int DayProgressionMs { get; set; }
    public int PreRaceStartPos { get; set; }
    public int NumCarsPreRace { get; set; }
    public int AlertMinRpm { get; set; }
    public int AlertMaxRpm { get; set; }
    public int CalcMaxSpeedKph { get; set; }
    public SimulatorFlags Flags { get; set; }
    /// <summary>0 = reverse, 15 = neutral (raw 0x0F).</summary>
    public int CurrentGear { get; set; } = 15;
    /// <summary>15 = none.</summary>
    public int SuggestedGear { get; set; } = 15;
    public int Throttle { get; set; }
    public int Brake { get; set; }
    public float WheelSpeedFL { get; set; }
    public float WheelSpeedFR { get; set; }
    public float WheelSpeedRL { get; set; }
    public float WheelSpeedRR { get; set; }
    public float TireRadiusFL { get; set; }
    public float TireRadiusFR { get; set; }
    public float TireRadiusRL { get; set; }
    public float TireRadiusRR { get; set; }
    public float SuspensionFL { get; set; }
    public float SuspensionFR { get; set; }
    public float SuspensionRL { get; set; }
    public float SuspensionRR { get; set; }
    public float ClutchPedal { get; set; }
    public float ClutchEngagement { get; set; }
    public float RpmAfterClutch { get; set; }
    public float TransmissionTopSpeedRatio { get; set; }
    public float[] GearRatios { get; set; } = new float[8];
    public int CarCode { get; set; }

    // UI-only (not on the wire)
    public string TrackName { get; set; } = "";
    public string CarClass { get; set; } = "Gr.3";
    public double LapProgress { get; set; }
    public double LiveDeltaSeconds { get; set; }
    public double FuelLapsRemaining { get; set; }
    public bool PitWindowOpen { get; set; }

    public double SpeedKph => SpeedMps * 3.6;
    public double SpeedMph => SpeedMps * 2.2369362920544;
    public double BoostBar => (BoostKpa - 100.0) / 100.0;

    public double FuelPercent =>
        FuelCapacity > 0f ? (FuelLevel / FuelCapacity) * 100.0 : FuelLevel;

    public double ThrottleNorm => Throttle / 255.0;
    public double BrakeNorm => Brake / 255.0;

    public double RpmFraction
    {
        get
        {
            int maxRpm = Math.Max(AlertMaxRpm, 1);
            return Math.Clamp(EngineRpm / maxRpm, 0.0, 1.0);
        }
    }

    public string GearDisplay => CurrentGear switch
    {
        0 => "R",
        15 => "N",
        _ => CurrentGear.ToString(),
    };

    /// <summary>True when the packet says we are in a car, on track, and not paused/loading.</summary>
    public bool IsRacing =>
        Flags.HasFlag(SimulatorFlags.CarOnTrack)
        && !Flags.HasFlag(SimulatorFlags.Paused)
        && !Flags.HasFlag(SimulatorFlags.LoadingOrProcessing);

    public static TelemetryPacket Idle => new();

    public static TelemetryPacket Parse(ReadOnlySpan<byte> p)
    {
        if (p.Length < MinimumSize)
            throw new PacketParseException($"Packet too small: {p.Length} bytes (need >= {MinimumSize})");

        var pkt = new TelemetryPacket
        {
            PositionX = ReadFloat(p, 0x04),
            PositionY = ReadFloat(p, 0x08),
            PositionZ = ReadFloat(p, 0x0C),
            VelocityX = ReadFloat(p, 0x10),
            VelocityY = ReadFloat(p, 0x14),
            VelocityZ = ReadFloat(p, 0x18),
            RotationPitch = ReadFloat(p, 0x1C),
            RotationYaw = ReadFloat(p, 0x20),
            RotationRoll = ReadFloat(p, 0x24),
            Heading = ReadFloat(p, 0x28),
            AngularVelocityX = ReadFloat(p, 0x2C),
            AngularVelocityY = ReadFloat(p, 0x30),
            AngularVelocityZ = ReadFloat(p, 0x34),
            RideHeight = ReadFloat(p, 0x38),
            EngineRpm = ReadFloat(p, 0x3C),
            FuelLevel = ReadFloat(p, 0x44),
            FuelCapacity = ReadFloat(p, 0x48),
            SpeedMps = ReadFloat(p, 0x4C),
            BoostKpa = ReadFloat(p, 0x50),
            OilPressure = ReadFloat(p, 0x54),
            WaterTemp = ReadFloat(p, 0x58),
            OilTemp = ReadFloat(p, 0x5C),
            TireTempFL = ReadFloat(p, 0x60),
            TireTempFR = ReadFloat(p, 0x64),
            TireTempRL = ReadFloat(p, 0x68),
            TireTempRR = ReadFloat(p, 0x6C),
            PacketId = BinaryPrimitives.ReadInt32LittleEndian(p.Slice(0x70, 4)),
            CurrentLap = BinaryPrimitives.ReadInt16LittleEndian(p.Slice(0x74, 2)),
            TotalLaps = BinaryPrimitives.ReadInt16LittleEndian(p.Slice(0x76, 2)),
            BestLapMs = BinaryPrimitives.ReadInt32LittleEndian(p.Slice(0x78, 4)),
            LastLapMs = BinaryPrimitives.ReadInt32LittleEndian(p.Slice(0x7C, 4)),
            DayProgressionMs = BinaryPrimitives.ReadInt32LittleEndian(p.Slice(0x80, 4)),
            PreRaceStartPos = BinaryPrimitives.ReadInt16LittleEndian(p.Slice(0x84, 2)),
            NumCarsPreRace = BinaryPrimitives.ReadInt16LittleEndian(p.Slice(0x86, 2)),
            AlertMinRpm = BinaryPrimitives.ReadInt16LittleEndian(p.Slice(0x88, 2)),
            AlertMaxRpm = BinaryPrimitives.ReadInt16LittleEndian(p.Slice(0x8A, 2)),
            CalcMaxSpeedKph = BinaryPrimitives.ReadInt16LittleEndian(p.Slice(0x8C, 2)),
            Flags = (SimulatorFlags)BinaryPrimitives.ReadUInt16LittleEndian(p.Slice(0x8E, 2)),
        };

        byte gears = p[0x90];
        pkt.CurrentGear = gears & 0x0F;
        pkt.SuggestedGear = (gears >> 4) & 0x0F;
        pkt.Throttle = p[0x91];
        pkt.Brake = p[0x92];
        pkt.WheelSpeedFL = ReadFloat(p, 0xB4);
        pkt.WheelSpeedFR = ReadFloat(p, 0xB8);
        pkt.WheelSpeedRL = ReadFloat(p, 0xBC);
        pkt.WheelSpeedRR = ReadFloat(p, 0xC0);
        pkt.TireRadiusFL = ReadFloat(p, 0xC4);
        pkt.TireRadiusFR = ReadFloat(p, 0xC8);
        pkt.TireRadiusRL = ReadFloat(p, 0xCC);
        pkt.TireRadiusRR = ReadFloat(p, 0xD0);
        pkt.SuspensionFL = ReadFloat(p, 0xD4);
        pkt.SuspensionFR = ReadFloat(p, 0xD8);
        pkt.SuspensionRL = ReadFloat(p, 0xDC);
        pkt.SuspensionRR = ReadFloat(p, 0xE0);
        pkt.ClutchPedal = ReadFloat(p, 0xF4);
        pkt.ClutchEngagement = ReadFloat(p, 0xF8);
        pkt.RpmAfterClutch = ReadFloat(p, 0xFC);
        pkt.TransmissionTopSpeedRatio = ReadFloat(p, 0x100);
        pkt.GearRatios =
        [
            ReadFloat(p, 0x104),
            ReadFloat(p, 0x108),
            ReadFloat(p, 0x10C),
            ReadFloat(p, 0x110),
            ReadFloat(p, 0x114),
            ReadFloat(p, 0x118),
            ReadFloat(p, 0x11C),
            ReadFloat(p, 0x120),
        ];
        pkt.CarCode = BinaryPrimitives.ReadInt32LittleEndian(p.Slice(0x124, 4));
        return pkt;
    }

    public byte[] Serialize(int size = 0x140)
    {
        var p = new byte[Math.Max(size, MinimumSize)];
        BinaryPrimitives.WriteUInt32LittleEndian(p.AsSpan(0, 4), Magic);
        WriteFloat(p, 0x04, PositionX);
        WriteFloat(p, 0x08, PositionY);
        WriteFloat(p, 0x0C, PositionZ);
        WriteFloat(p, 0x10, VelocityX);
        WriteFloat(p, 0x14, VelocityY);
        WriteFloat(p, 0x18, VelocityZ);
        WriteFloat(p, 0x1C, RotationPitch);
        WriteFloat(p, 0x20, RotationYaw);
        WriteFloat(p, 0x24, RotationRoll);
        WriteFloat(p, 0x28, Heading);
        WriteFloat(p, 0x2C, AngularVelocityX);
        WriteFloat(p, 0x30, AngularVelocityY);
        WriteFloat(p, 0x34, AngularVelocityZ);
        WriteFloat(p, 0x38, RideHeight);
        WriteFloat(p, 0x3C, EngineRpm);
        WriteFloat(p, 0x44, FuelLevel);
        WriteFloat(p, 0x48, FuelCapacity);
        WriteFloat(p, 0x4C, SpeedMps);
        WriteFloat(p, 0x50, BoostKpa);
        WriteFloat(p, 0x54, OilPressure);
        WriteFloat(p, 0x58, WaterTemp);
        WriteFloat(p, 0x5C, OilTemp);
        WriteFloat(p, 0x60, TireTempFL);
        WriteFloat(p, 0x64, TireTempFR);
        WriteFloat(p, 0x68, TireTempRL);
        WriteFloat(p, 0x6C, TireTempRR);
        BinaryPrimitives.WriteInt32LittleEndian(p.AsSpan(0x70, 4), PacketId);
        BinaryPrimitives.WriteInt16LittleEndian(p.AsSpan(0x74, 2), (short)CurrentLap);
        BinaryPrimitives.WriteInt16LittleEndian(p.AsSpan(0x76, 2), (short)TotalLaps);
        BinaryPrimitives.WriteInt32LittleEndian(p.AsSpan(0x78, 4), BestLapMs);
        BinaryPrimitives.WriteInt32LittleEndian(p.AsSpan(0x7C, 4), LastLapMs);
        BinaryPrimitives.WriteInt32LittleEndian(p.AsSpan(0x80, 4), DayProgressionMs);
        BinaryPrimitives.WriteInt16LittleEndian(p.AsSpan(0x84, 2), (short)PreRaceStartPos);
        BinaryPrimitives.WriteInt16LittleEndian(p.AsSpan(0x86, 2), (short)NumCarsPreRace);
        BinaryPrimitives.WriteInt16LittleEndian(p.AsSpan(0x88, 2), (short)AlertMinRpm);
        BinaryPrimitives.WriteInt16LittleEndian(p.AsSpan(0x8A, 2), (short)AlertMaxRpm);
        BinaryPrimitives.WriteInt16LittleEndian(p.AsSpan(0x8C, 2), (short)CalcMaxSpeedKph);
        BinaryPrimitives.WriteInt16LittleEndian(p.AsSpan(0x8E, 2), (short)Flags);
        p[0x90] = (byte)((CurrentGear & 0x0F) | ((SuggestedGear & 0x0F) << 4));
        p[0x91] = (byte)(Throttle & 0xFF);
        p[0x92] = (byte)(Brake & 0xFF);
        WriteFloat(p, 0xB4, WheelSpeedFL);
        WriteFloat(p, 0xB8, WheelSpeedFR);
        WriteFloat(p, 0xBC, WheelSpeedRL);
        WriteFloat(p, 0xC0, WheelSpeedRR);
        WriteFloat(p, 0xC4, TireRadiusFL);
        WriteFloat(p, 0xC8, TireRadiusFR);
        WriteFloat(p, 0xCC, TireRadiusRL);
        WriteFloat(p, 0xD0, TireRadiusRR);
        WriteFloat(p, 0xD4, SuspensionFL);
        WriteFloat(p, 0xD8, SuspensionFR);
        WriteFloat(p, 0xDC, SuspensionRL);
        WriteFloat(p, 0xE0, SuspensionRR);
        WriteFloat(p, 0xF4, ClutchPedal);
        WriteFloat(p, 0xF8, ClutchEngagement);
        WriteFloat(p, 0xFC, RpmAfterClutch);
        WriteFloat(p, 0x100, TransmissionTopSpeedRatio);
        var ratios = new float[8];
        Array.Copy(GearRatios, ratios, Math.Min(8, GearRatios.Length));
        for (int i = 0; i < 8; i++)
            WriteFloat(p, 0x104 + i * 4, ratios[i]);
        BinaryPrimitives.WriteInt32LittleEndian(p.AsSpan(0x124, 4), CarCode);
        return p;
    }

    private static float ReadFloat(ReadOnlySpan<byte> b, int offset) =>
        BinaryPrimitives.ReadSingleLittleEndian(b.Slice(offset, 4));

    private static void WriteFloat(byte[] b, int offset, float value) =>
        BinaryPrimitives.WriteSingleLittleEndian(b.AsSpan(offset, 4), value);
}
