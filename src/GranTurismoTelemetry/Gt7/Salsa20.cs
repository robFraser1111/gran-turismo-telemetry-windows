using System.Buffers.Binary;

namespace GranTurismoTelemetry.Gt7;

/// <summary>
/// Minimal Salsa20 stream cipher (20 rounds). Only supports what GT7 needs:
/// 256-bit key, 64-bit nonce, keystream-XOR decryption.
/// Ported from https://github.com/robFraser1111/gran-turismo-telemetry
/// </summary>
public static class Salsa20
{
    private static readonly byte[] Sigma = System.Text.Encoding.ASCII.GetBytes("expand 32-byte k");

    public static void XorInPlace(ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce, Span<byte> cipher)
    {
        if (key.Length != 32) throw new ArgumentException("Key must be 32 bytes", nameof(key));
        if (nonce.Length != 8) throw new ArgumentException("Nonce must be 8 bytes", nameof(nonce));

        Span<uint> state = stackalloc uint[16];
        state[0] = BinaryPrimitives.ReadUInt32LittleEndian(Sigma.AsSpan(0, 4));
        state[5] = BinaryPrimitives.ReadUInt32LittleEndian(Sigma.AsSpan(4, 4));
        state[10] = BinaryPrimitives.ReadUInt32LittleEndian(Sigma.AsSpan(8, 4));
        state[15] = BinaryPrimitives.ReadUInt32LittleEndian(Sigma.AsSpan(12, 4));
        state[1] = BinaryPrimitives.ReadUInt32LittleEndian(key.Slice(0, 4));
        state[2] = BinaryPrimitives.ReadUInt32LittleEndian(key.Slice(4, 4));
        state[3] = BinaryPrimitives.ReadUInt32LittleEndian(key.Slice(8, 4));
        state[4] = BinaryPrimitives.ReadUInt32LittleEndian(key.Slice(12, 4));
        state[11] = BinaryPrimitives.ReadUInt32LittleEndian(key.Slice(16, 4));
        state[12] = BinaryPrimitives.ReadUInt32LittleEndian(key.Slice(20, 4));
        state[13] = BinaryPrimitives.ReadUInt32LittleEndian(key.Slice(24, 4));
        state[14] = BinaryPrimitives.ReadUInt32LittleEndian(key.Slice(28, 4));
        state[6] = BinaryPrimitives.ReadUInt32LittleEndian(nonce.Slice(0, 4));
        state[7] = BinaryPrimitives.ReadUInt32LittleEndian(nonce.Slice(4, 4));

        Span<byte> block = stackalloc byte[64];
        int offset = 0;
        while (offset < cipher.Length)
        {
            GenerateBlock(state, block);
            int take = Math.Min(64, cipher.Length - offset);
            for (int i = 0; i < take; i++)
                cipher[offset + i] ^= block[i];
            offset += take;

            unchecked
            {
                state[8]++;
                if (state[8] == 0) state[9]++;
            }
        }
    }

    /// <summary>First 64 bytes of keystream for (key, nonce) with counter 0. Used by tests.</summary>
    public static byte[] FirstBlock(ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce)
    {
        var zeros = new byte[64];
        XorInPlace(key, nonce, zeros);
        return zeros;
    }

    private static void GenerateBlock(ReadOnlySpan<uint> input, Span<byte> output)
    {
        Span<uint> x = stackalloc uint[16];
        input.CopyTo(x);

        for (int i = 0; i < 10; i++)
        {
            x[4] ^= Rotl(x[0] + x[12], 7);
            x[8] ^= Rotl(x[4] + x[0], 9);
            x[12] ^= Rotl(x[8] + x[4], 13);
            x[0] ^= Rotl(x[12] + x[8], 18);

            x[9] ^= Rotl(x[5] + x[1], 7);
            x[13] ^= Rotl(x[9] + x[5], 9);
            x[1] ^= Rotl(x[13] + x[9], 13);
            x[5] ^= Rotl(x[1] + x[13], 18);

            x[14] ^= Rotl(x[10] + x[6], 7);
            x[2] ^= Rotl(x[14] + x[10], 9);
            x[6] ^= Rotl(x[2] + x[14], 13);
            x[10] ^= Rotl(x[6] + x[2], 18);

            x[3] ^= Rotl(x[15] + x[11], 7);
            x[7] ^= Rotl(x[3] + x[15], 9);
            x[11] ^= Rotl(x[7] + x[3], 13);
            x[15] ^= Rotl(x[11] + x[7], 18);

            x[1] ^= Rotl(x[0] + x[3], 7);
            x[2] ^= Rotl(x[1] + x[0], 9);
            x[3] ^= Rotl(x[2] + x[1], 13);
            x[0] ^= Rotl(x[3] + x[2], 18);

            x[6] ^= Rotl(x[5] + x[4], 7);
            x[7] ^= Rotl(x[6] + x[5], 9);
            x[4] ^= Rotl(x[7] + x[6], 13);
            x[5] ^= Rotl(x[4] + x[7], 18);

            x[11] ^= Rotl(x[10] + x[9], 7);
            x[8] ^= Rotl(x[11] + x[10], 9);
            x[9] ^= Rotl(x[8] + x[11], 13);
            x[10] ^= Rotl(x[9] + x[8], 18);

            x[12] ^= Rotl(x[15] + x[14], 7);
            x[13] ^= Rotl(x[12] + x[15], 9);
            x[14] ^= Rotl(x[13] + x[12], 13);
            x[15] ^= Rotl(x[14] + x[13], 18);
        }

        for (int i = 0; i < 16; i++)
        {
            uint v = x[i] + input[i];
            BinaryPrimitives.WriteUInt32LittleEndian(output.Slice(i * 4, 4), v);
        }
    }

    private static uint Rotl(uint v, int c) => (v << c) | (v >> (32 - c));
}
