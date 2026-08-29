using System.Buffers.Binary;
using System.Text;

namespace GranTurismoTelemetry.Gt7;

/// <summary>
/// GT7 Salsa20 packet encryption helpers.
/// Key: first 32 bytes of UTF-8 "Simulator Interface Packet GT7 ver 0.0".
/// Nonce: ciphertext bytes 0x40..0x44 as LE uint32 XOR 0xDEADBEAF for the
/// first 4 nonce bytes; original 4 bytes are the second half. Salsa20 20 rounds.
/// After decrypt, magic at offset 0 must be "G7S0" (0x47375330).
/// </summary>
public static class Gt7Crypto
{
    public const string KeySeed = "Simulator Interface Packet GT7 ver 0.0";
    public const uint DeadBeaf = 0xDEADBEAFu;
    public const uint Magic = 0x47375330u;

    public static readonly byte[] Key = InitKey();

    public static byte[] Decrypt(byte[] raw)
    {
        var buffer = (byte[])raw.Clone();
        DecryptInPlace(buffer);
        return buffer;
    }

    public static void DecryptInPlace(byte[] buffer)
    {
        if (buffer.Length < 0x44)
            throw new ArgumentException("Buffer too small for IV", nameof(buffer));
        uint oiv = BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(0x40, 4));
        Span<byte> nonce = stackalloc byte[8];
        BuildNonce(oiv, nonce);
        Salsa20.XorInPlace(Key, nonce, buffer);
    }

    /// <summary>
    /// Builds a ciphertext that will decrypt back to <paramref name="plaintext"/> when
    /// passed to <see cref="TryDecode"/>. The four IV bytes at offset 0x40 in the
    /// returned ciphertext are literally <paramref name="ciphertextIv"/>.
    /// </summary>
    public static byte[] EncryptForTest(byte[] plaintext, uint ciphertextIv)
    {
        Span<byte> nonce = stackalloc byte[8];
        BuildNonce(ciphertextIv, nonce);
        var cipher = (byte[])plaintext.Clone();
        Salsa20.XorInPlace(Key, nonce, cipher);
        BinaryPrimitives.WriteUInt32LittleEndian(cipher.AsSpan(0x40, 4), ciphertextIv);
        return cipher;
    }

    public readonly record struct DecodeResult(TelemetryPacket? Packet, string? Reason);

    public static DecodeResult TryDecode(byte[] raw)
    {
        if (raw.Length < TelemetryPacket.MinimumSize)
        {
            return new DecodeResult(null,
                $"short packet ({raw.Length} bytes, need >= {TelemetryPacket.MinimumSize})");
        }

        try
        {
            var buffer = (byte[])raw.Clone();
            DecryptInPlace(buffer);
            uint magic = BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(0, 4));
            if (magic != Magic)
            {
                return new DecodeResult(null,
                    $"bad magic 0x{magic:X8} after decrypt (expected 0x47375330 'G7S0')");
            }
            return new DecodeResult(TelemetryPacket.Parse(buffer), null);
        }
        catch (Exception ex)
        {
            return new DecodeResult(null, ex.Message);
        }
    }

    /// <summary>
    /// Nonce layout matches the C# / Python community receivers:
    /// bytes 0..4 = (IV XOR 0xDEADBEAF) LE, bytes 4..8 = original IV LE.
    /// </summary>
    public static void BuildNonce(uint oivInt, Span<byte> nonce)
    {
        uint xored = oivInt ^ DeadBeaf;
        BinaryPrimitives.WriteUInt32LittleEndian(nonce.Slice(0, 4), xored);
        BinaryPrimitives.WriteUInt32LittleEndian(nonce.Slice(4, 4), oivInt);
    }

    private static byte[] InitKey()
    {
        var raw = Encoding.UTF8.GetBytes(KeySeed);
        var key = new byte[32];
        Array.Copy(raw, key, Math.Min(raw.Length, 32));
        return key;
    }
}
