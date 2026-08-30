using System.Buffers.Binary;
using GranTurismoTelemetry.Gt7;

namespace GranTurismoTelemetry.Tests;

public class Salsa20ParserTests
{
    [Fact]
    public void Salsa20IsInvolution()
    {
        var key = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
        var nonce = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        var original = Enumerable.Range(0, 200).Select(i => (byte)i).ToArray();
        var buf = (byte[])original.Clone();
        Salsa20.XorInPlace(key, nonce, buf);
        Assert.False(buf.AsSpan().SequenceEqual(original));
        Salsa20.XorInPlace(key, nonce, buf);
        Assert.True(buf.AsSpan().SequenceEqual(original));
    }

    [Fact]
    public void Salsa20FirstBlockKnownVector()
    {
        var key = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
        var nonce = new byte[] { 9, 8, 7, 6, 5, 4, 3, 2 };
        var block = Salsa20.FirstBlock(key, nonce);
        const string expectedHex = "9a550e1ba1528b269e3473558c4aeea28ace1c870be826aedebc789f60a9da410eb808928b2d8c81e6dd680f1a4e2e4f231333cf6d08efe20ff470279ccad55a";
        Assert.Equal(64, block.Length);
        var actual = Convert.ToHexString(block).ToLowerInvariant();
        Assert.Equal(expectedHex, actual);
    }

    [Fact]
    public void PacketRoundTripSmokeTest()
    {
        var plaintext = new byte[0x140];
        BinaryPrimitives.WriteUInt32LittleEndian(plaintext.AsSpan(0, 4), TelemetryPacket.Magic);
        WriteF32(plaintext, 0x3C, 7250.5f);
        WriteF32(plaintext, 0x4C, 55.55f);
        WriteF32(plaintext, 0x50, 160.0f);
        WriteF32(plaintext, 0x44, 87.5f);
        WriteF32(plaintext, 0x48, 100f);
        WriteF32(plaintext, 0x60, 90.0f);
        WriteF32(plaintext, 0x64, 91.5f);
        WriteF32(plaintext, 0x68, 92.0f);
        WriteF32(plaintext, 0x6C, 93.0f);
        BinaryPrimitives.WriteInt32LittleEndian(plaintext.AsSpan(0x70, 4), 12345);
        BinaryPrimitives.WriteInt16LittleEndian(plaintext.AsSpan(0x74, 2), 3);
        BinaryPrimitives.WriteInt16LittleEndian(plaintext.AsSpan(0x76, 2), 10);
        BinaryPrimitives.WriteInt16LittleEndian(plaintext.AsSpan(0x8A, 2), 8500);
        plaintext[0x90] = 0x04;
        plaintext[0x91] = 200;
        plaintext[0x92] = 40;
        WriteF32(plaintext, 0xF4, 0.85f);
        WriteF32(plaintext, 0x08, 512.5f);
        WriteF32(plaintext, 0x14, 3.25f);
        WriteF32(plaintext, 0x30, 0.42f);
        WriteF32(plaintext, 0x38, 0.085f);
        WriteF32(plaintext, 0x54, 4.75f);
        WriteF32(plaintext, 0x58, 88.0f);
        WriteF32(plaintext, 0x5C, 108.0f);
        WriteF32(plaintext, 0xB4, 168.3f);
        WriteF32(plaintext, 0xC4, 0.33f);

        var cipher = Gt7Crypto.EncryptForTest(plaintext, 0x12345678u);
        Assert.Equal(0x12345678u, BinaryPrimitives.ReadUInt32LittleEndian(cipher.AsSpan(0x40, 4)));

        var decoded = Gt7Crypto.TryDecode(cipher);
        Assert.Null(decoded.Reason);
        Assert.NotNull(decoded.Packet);
        var packet = decoded.Packet!;
        Assert.Equal(7250.5f, packet.EngineRpm, 2);
        Assert.Equal(55.55f, packet.SpeedMps, 2);
        Assert.Equal(160.0f, packet.BoostKpa, 2);
        Assert.Equal(87.5f, packet.FuelLevel, 2);
        Assert.Equal(90.0f, packet.TireTempFL, 2);
        Assert.Equal(4, packet.CurrentGear);
        Assert.Equal(200, packet.Throttle);
        Assert.Equal(40, packet.Brake);
        Assert.Equal(0.85f, packet.ClutchPedal, 2);
        Assert.Equal(512.5f, packet.PositionY, 2);
        Assert.Equal(3.25f, packet.VelocityY, 2);
        Assert.Equal(0.42f, packet.AngularVelocityY, 2);
        Assert.Equal(0.085f, packet.RideHeight, 3);
        Assert.Equal(4.75f, packet.OilPressure, 2);
        Assert.Equal(88.0f, packet.WaterTemp, 2);
        Assert.Equal(108.0f, packet.OilTemp, 2);
        Assert.Equal(168.3f, packet.WheelSpeedFL, 2);
        Assert.Equal(0.33f, packet.TireRadiusFL, 3);
        Assert.Equal(12345, packet.PacketId);
        Assert.Equal(3, packet.CurrentLap);
        Assert.Equal(10, packet.TotalLaps);
    }

    [Fact]
    public void BadMagicIsDropped()
    {
        var plaintext = new byte[0x140];
        BinaryPrimitives.WriteUInt32LittleEndian(plaintext.AsSpan(0, 4), 0xDEADBEEFu);
        WriteF32(plaintext, 0x3C, 1000f);
        var cipher = Gt7Crypto.EncryptForTest(plaintext, 0x11111111u);
        var decoded = Gt7Crypto.TryDecode(cipher);
        Assert.Null(decoded.Packet);
        Assert.NotNull(decoded.Reason);
        Assert.Contains("bad magic", decoded.Reason!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ShortPacketIsDropped()
    {
        var decoded = Gt7Crypto.TryDecode(Enumerable.Repeat((byte)1, 20).ToArray());
        Assert.Null(decoded.Packet);
        Assert.Contains("short packet", decoded.Reason!);
    }

    [Fact]
    public void GearDisplay()
    {
        Assert.Equal("N", new TelemetryPacket { CurrentGear = 15 }.GearDisplay);
        Assert.Equal("R", new TelemetryPacket { CurrentGear = 0 }.GearDisplay);
        Assert.Equal("4", new TelemetryPacket { CurrentGear = 4 }.GearDisplay);
    }

    [Fact]
    public void KeyIsFirst32BytesOfSeed()
    {
        var seed = System.Text.Encoding.UTF8.GetBytes("Simulator Interface Packet GT7 ver 0.0");
        Assert.True(Gt7Crypto.Key.AsSpan().SequenceEqual(seed.AsSpan(0, 32)));
        Assert.Equal(32, Gt7Crypto.Key.Length);
    }

    [Fact]
    public void NonceLayout()
    {
        Span<byte> nonce = stackalloc byte[8];
        uint oiv = 0x12345678u;
        Gt7Crypto.BuildNonce(oiv, nonce);
        Assert.Equal(oiv ^ 0xDEADBEAFu, BinaryPrimitives.ReadUInt32LittleEndian(nonce.Slice(0, 4)));
        Assert.Equal(oiv, BinaryPrimitives.ReadUInt32LittleEndian(nonce.Slice(4, 4)));
    }

    [Fact]
    public void ParseConstructedPacketAReadsLapFlagsAndCarCode()
    {
        var src = new TelemetryPacket
        {
            CurrentLap = 4,
            LastLapMs = 85_000,
            BestLapMs = 84_000,
            Flags = SimulatorFlags.CarOnTrack,
            CarCode = 42,
            PacketId = 99,
        };
        var bytes = src.Serialize(296);
        Assert.Equal(296, bytes.Length);
        var parsed = TelemetryPacket.Parse(bytes);
        Assert.Equal(4, parsed.CurrentLap);
        Assert.Equal(85_000, parsed.LastLapMs);
        Assert.Equal(84_000, parsed.BestLapMs);
        Assert.Equal(SimulatorFlags.CarOnTrack, parsed.Flags);
        Assert.Equal(42, parsed.CarCode);
        Assert.Equal(99, parsed.PacketId);
    }

    private static void WriteF32(byte[] buf, int offset, float value) =>
        BinaryPrimitives.WriteSingleLittleEndian(buf.AsSpan(offset, 4), value);
}
