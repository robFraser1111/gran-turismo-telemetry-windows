using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace GranTurismoTelemetry.Gt7;

/// <summary>
/// Same shape as the working ASP.NET receiver: one IPv4 socket bound to 33740,
/// heartbeat ASCII 'A' sent FROM that socket to PS5:33739, ReceiveAsync without
/// overlapping reads. Windows SIO_UDP_CONNRESET so ICMP port-unreachable does
/// not kill the loop.
///
/// Find PS5: broadcast heartbeat to 255.255.255.255:33739 (and subnet broadcasts)
/// and lock onto the first source that decrypts as a valid GT7 packet.
/// </summary>
public sealed class Gt7UdpClient : IDisposable
{
    public const int DefaultSendPort = 33739;
    public const int DefaultReceivePort = 33740;

    private volatile bool _running;
    private UdpClient? _udp;
    private CancellationTokenSource? _cts;
    private volatile IPEndPoint[] _heartbeatTargets = [];
    private readonly object _peerLock = new();

    public event Action<TelemetryPacket>? PacketReceived;
    public event Action<int>? RawPacketReceived;
    public event Action<string>? DecodeFailed;
    public event Action<string>? Status;
    public event Action<string>? PeerLocked;

    public string? PeerHost { get; private set; }
    public bool IsDiscovering { get; private set; }

    public void Start(string ps5Host, int sendPort = DefaultSendPort, int receivePort = DefaultReceivePort)
    {
        Stop();
        _running = true;
        IsDiscovering = false;
        PeerHost = null;
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        _ = Task.Run(() => RunAsync(ps5Host, sendPort, receivePort, discover: false, ct), ct);
    }

    public void StartDiscover(int sendPort = DefaultSendPort, int receivePort = DefaultReceivePort)
    {
        Stop();
        _running = true;
        IsDiscovering = true;
        PeerHost = null;
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        _ = Task.Run(() => RunAsync("255.255.255.255", sendPort, receivePort, discover: true, ct), ct);
    }

    public void Stop()
    {
        _running = false;
        IsDiscovering = false;
        try { _cts?.Cancel(); } catch { /* ignore */ }
        try { _udp?.Close(); } catch { /* ignore */ }
        _udp = null;
        _cts?.Dispose();
        _cts = null;
        _heartbeatTargets = [];
    }

    public void Dispose() => Stop();

    private async Task RunAsync(string host, int sendPort, int receivePort, bool discover, CancellationToken ct)
    {
        UdpClient udp;
        try
        {
            udp = CreateIpv4Client(receivePort);
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressAlreadyInUse)
        {
            Status?.Invoke($"UDP {receivePort} is already in use. Close the original web app (or anything else bound to that port) and try Connect again.");
            return;
        }
        catch (Exception ex)
        {
            Status?.Invoke($"bind(:{receivePort}) failed: {ex.Message}");
            return;
        }

        _udp = udp;

        try
        {
            if (discover)
            {
                _heartbeatTargets = BuildDiscoverTargets(sendPort);
                Status?.Invoke("Looking for GT7 on this network…");
            }
            else
            {
                IPEndPoint endpoint;
                try
                {
                    if (!IPAddress.TryParse(host, out var ip) || ip.AddressFamily != AddressFamily.InterNetwork)
                    {
                        var addrs = await Dns.GetHostAddressesAsync(host, ct);
                        ip = addrs.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)
                             ?? throw new InvalidOperationException("No IPv4 address");
                    }
                    endpoint = new IPEndPoint(ip, sendPort);
                }
                catch (Exception ex)
                {
                    Status?.Invoke($"Invalid IPv4 address: {host} ({ex.Message})");
                    udp.Dispose();
                    return;
                }

                lock (_peerLock)
                {
                    PeerHost = endpoint.Address.ToString();
                    _heartbeatTargets = [endpoint];
                }
                Status?.Invoke($"Listening UDP :{receivePort}, heartbeat → {endpoint}");
            }

            foreach (var ep in _heartbeatTargets)
                await SendHeartbeatAsync(udp, ep);
            _ = Task.Run(() => HeartbeatLoop(udp, ct), ct);

            while (_running && !ct.IsCancellationRequested)
            {
                try
                {
                    // One receive at a time — do not race ReceiveAsync with a timeout
                    // (that was stacking overlapping reads and dropping packets).
                    var result = await udp.ReceiveAsync(ct);
                    int n = result.Buffer.Length;
                    if (n <= 0) continue;
                    RawPacketReceived?.Invoke(n);
                    var decoded = Gt7Crypto.TryDecode(result.Buffer);
                    if (decoded.Packet is not null)
                    {
                        TryLockPeer(result.RemoteEndPoint, sendPort, receivePort);
                        PacketReceived?.Invoke(decoded.Packet);
                    }
                    else
                    {
                        DecodeFailed?.Invoke(decoded.Reason ?? "unknown");
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (ObjectDisposedException) { break; }
                catch (Exception ex)
                {
                    if (_running)
                    {
                        Status?.Invoke($"recv failed: {ex.Message}");
                        try { await Task.Delay(500, ct); } catch { break; }
                    }
                }
            }
        }
        finally
        {
            try { udp.Dispose(); } catch { /* ignore */ }
        }
    }

    private void TryLockPeer(IPEndPoint remote, int sendPort, int receivePort)
    {
        if (remote.Address.AddressFamily != AddressFamily.InterNetwork)
            return;

        string ip = remote.Address.ToString();
        bool first = false;
        lock (_peerLock)
        {
            if (PeerHost is not null)
                return;
            PeerHost = ip;
            _heartbeatTargets = [new IPEndPoint(remote.Address, sendPort)];
            IsDiscovering = false;
            first = true;
        }

        if (first)
        {
            PeerLocked?.Invoke(ip);
            Status?.Invoke($"Listening UDP :{receivePort}, heartbeat → {ip}:{sendPort}");
        }
    }

    private static IPEndPoint[] BuildDiscoverTargets(int sendPort)
    {
        var list = new List<IPEndPoint>
        {
            new(IPAddress.Broadcast, sendPort),
        };
        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up) continue;
                if (nic.NetworkInterfaceType is NetworkInterfaceType.Loopback) continue;
                foreach (var ua in nic.GetIPProperties().UnicastAddresses)
                {
                    if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    if (IPAddress.IsLoopback(ua.Address)) continue;
                    var mask = ua.IPv4Mask;
                    if (mask is null) continue;
                    byte[] ip = ua.Address.GetAddressBytes();
                    byte[] m = mask.GetAddressBytes();
                    if (ip.Length != 4 || m.Length != 4) continue;
                    var bcast = new byte[4];
                    for (int i = 0; i < 4; i++)
                        bcast[i] = (byte)(ip[i] | ~m[i]);
                    var ep = new IPEndPoint(new IPAddress(bcast), sendPort);
                    if (!list.Any(x => x.Equals(ep)))
                        list.Add(ep);
                }
            }
        }
        catch
        {
            // broadcast-only is enough
        }
        return list.ToArray();
    }

    private static UdpClient CreateIpv4Client(int receivePort)
    {
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, false);
        socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, true);
        socket.ReceiveBufferSize = 1 << 16;
        socket.Bind(new IPEndPoint(IPAddress.Any, receivePort));
        var udp = new UdpClient { Client = socket, EnableBroadcast = true };
        DisableUdpConnReset(udp);
        return udp;
    }

    private async Task HeartbeatLoop(UdpClient udp, CancellationToken ct)
    {
        try
        {
            // 2s is safer than 10s — GT7 drops the stream if heartbeats go quiet.
            while (_running && !ct.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(2), ct);
                var targets = _heartbeatTargets;
                foreach (var ep in targets)
                    await SendHeartbeatAsync(udp, ep);
            }
        }
        catch (OperationCanceledException) { }
    }

    private async Task SendHeartbeatAsync(UdpClient udp, IPEndPoint ep)
    {
        try
        {
            await udp.SendAsync(new byte[] { (byte)'A' }, 1, ep);
        }
        catch (Exception ex)
        {
            Status?.Invoke($"heartbeat failed: {ex.Message}");
        }
    }

    private static void DisableUdpConnReset(UdpClient udp)
    {
        if (!OperatingSystem.IsWindows()) return;
        const int SioUdpConnreset = unchecked((int)0x9800000C);
        try
        {
            udp.Client.IOControl((IOControlCode)SioUdpConnreset, new byte[] { 0 }, null);
        }
        catch (PlatformNotSupportedException) { }
        catch (SocketException) { }
    }
}
