using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using GranTurismoTelemetry.Gt7;

namespace GranTurismoTelemetry.Tests;

public class UdpDiscoverTests
{
    [Fact]
    public async Task DiscoverLocksOntoFirstValidGt7PacketSource()
    {
        int recvPort = FreeUdpPort();
        int sendPort = FreeUdpPort();
        using var client = new Gt7UdpClient();
        var locked = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.PeerLocked += ip => locked.TrySetResult(ip);
        client.StartDiscover(sendPort, recvPort);
        await Task.Delay(250);

        var plaintext = new byte[0x140];
        BinaryPrimitives.WriteUInt32LittleEndian(plaintext.AsSpan(0, 4), TelemetryPacket.Magic);
        var cipher = Gt7Crypto.EncryptForTest(plaintext, 0xAABBCCDDu);

        using var sender = new UdpClient(AddressFamily.InterNetwork);
        await sender.SendAsync(cipher, cipher.Length, new IPEndPoint(IPAddress.Loopback, recvPort));

        var winner = await Task.WhenAny(locked.Task, Task.Delay(TimeSpan.FromSeconds(4)));
        Assert.True(locked.Task.IsCompleted, "client did not lock onto the first valid GT7 packet");
        Assert.Equal(IPAddress.Loopback.ToString(), await locked.Task);
        Assert.Equal(IPAddress.Loopback.ToString(), client.PeerHost);
        Assert.False(client.IsDiscovering);
        client.Stop();
    }

    [Fact]
    public async Task UnicastDoesNotRelockPeer()
    {
        int recvPort = FreeUdpPort();
        int sendPort = FreeUdpPort();
        using var client = new Gt7UdpClient();
        string? locked = null;
        client.PeerLocked += ip => locked = ip;
        client.Start("127.0.0.1", sendPort, recvPort);
        await Task.Delay(250);

        var plaintext = new byte[0x140];
        BinaryPrimitives.WriteUInt32LittleEndian(plaintext.AsSpan(0, 4), TelemetryPacket.Magic);
        var cipher = Gt7Crypto.EncryptForTest(plaintext, 0x11111111u);
        using var sender = new UdpClient(AddressFamily.InterNetwork);
        await sender.SendAsync(cipher, cipher.Length, new IPEndPoint(IPAddress.Loopback, recvPort));
        await Task.Delay(400);

        Assert.Equal("127.0.0.1", client.PeerHost);
        Assert.Null(locked);
        client.Stop();
    }

    [Fact]
    public void AddressAlreadyInUseReportsWebAppHint()
    {
        int port = FreeUdpPort();
        using var holder = CreateBound(port);
        using var client = new Gt7UdpClient();
        string? status = null;
        var gate = new ManualResetEventSlim();
        client.Status += msg =>
        {
            status = msg;
            gate.Set();
        };
        client.Start("127.0.0.1", port + 1, port);
        Assert.True(gate.Wait(TimeSpan.FromSeconds(3)));
        Assert.Contains("already in use", status, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("web app", status, StringComparison.OrdinalIgnoreCase);
        client.Stop();
    }

    private static int FreeUdpPort()
    {
        using var probe = new UdpClient(0);
        return ((IPEndPoint)probe.Client.LocalEndPoint!).Port;
    }

    private static UdpClient CreateBound(int port)
    {
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, false);
        socket.Bind(new IPEndPoint(IPAddress.Any, port));
        return new UdpClient { Client = socket };
    }

}
