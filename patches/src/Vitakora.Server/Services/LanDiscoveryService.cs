using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Vitakora.Server.Services;

public sealed class LanDiscoveryService : BackgroundService
{
    private const int Port = 50505;
    private const string Request = "VITAKORA_DISCOVER_V1";
    private const string ResponsePrefix = "VITAKORA_SERVER_V1|";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var udp = new UdpClient(new IPEndPoint(IPAddress.Any, Port));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var received = await udp.ReceiveAsync(stoppingToken);
                var text = Encoding.UTF8.GetString(received.Buffer);
                if (!string.Equals(text, Request, StringComparison.Ordinal)) continue;

                var localAddress = ResolveLocalAddress(received.RemoteEndPoint.Address);
                var response = Encoding.UTF8.GetBytes($"{ResponsePrefix}http://{localAddress}:5000");
                await udp.SendAsync(response, response.Length, received.RemoteEndPoint);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch
            {
                await Task.Delay(250, stoppingToken);
            }
        }
    }

    private static IPAddress ResolveLocalAddress(IPAddress remoteAddress)
    {
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Connect(new IPEndPoint(remoteAddress, 9));
            return ((IPEndPoint)socket.LocalEndPoint!).Address;
        }
        catch
        {
            return Dns.GetHostAddresses(Dns.GetHostName())
                .FirstOrDefault(x => x.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(x))
                ?? IPAddress.Loopback;
        }
    }
}
