using System.Net;
using System.Net.Sockets;

namespace MIN.Tests.Common;

/// <summary>
/// Выделение свободных портов для интеграционных тестов
/// </summary>
public static class TestPorts
{
    /// <summary>
    /// Получить свободный TCP-порт на loopback
    /// </summary>
    public static int GetFreeTcpPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    /// <summary>
    /// Получить свободный UDP-порт на loopback
    /// </summary>
    public static int GetFreeUdpPort()
    {
        using var client = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)client.Client.LocalEndPoint!).Port;
    }
}
