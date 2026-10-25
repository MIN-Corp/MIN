using System.Net;
using System.Net.Sockets;
using MIN.Core.Transport.Contracts.Enum;

namespace MIN.Core.Transport.Contracts.Helpers;

/// <summary>
/// Помошник в выделении порта
/// </summary>
public static class PortProvider
{
    private const int MinPort = 49152;
    private const int MaxPort = 65535;
    private const int RandomAttempts = 50;
    private readonly static Random random = new();
    private readonly static Dictionary<TransportType, HashSet<int>> reserved = new()
    {
        { TransportType.Tcp, [] },
        { TransportType.Udp, [] },
    };

    /// <summary>
    /// Получить свободный порт
    /// </summary>
    public static int AllocatePort(TransportType type, ushort? preferredPort = 0, int? sequentialAttempts = 15)
    {
        if (preferredPort > 1024 && preferredPort < ushort.MaxValue && sequentialAttempts != null)
        {
            for (var i = 0; i < sequentialAttempts; i++)
            {
                var port = preferredPort.Value + i;
                if (reserved[type].Contains(port) || !IsPortFree(port, type))
                {
                    continue;
                }

                reserved[type].Add(port);
                return port;
            }
        }

        for (var i = 0; i < RandomAttempts; i++)
        {
            var port = random.Next(MinPort, MaxPort + 1);
            if (reserved[type].Contains(port) || !IsPortFree(port, type))
            {
                continue;
            }

            reserved[type].Add(port);
            return port;
        }

        throw new InvalidOperationException("No free port in dynamic range");
    }

    /// <summary>
    /// Отпустить порт
    /// </summary>
    public static void ReleasePort(int port, TransportType type)
        => reserved[type].Remove(port);

    private static bool IsPortFree(int port, TransportType type)
    {
        try
        {
            switch (type)
            {
                case TransportType.Tcp:
                {
                    using var listener = new TcpListener(IPAddress.Any, port);
                    listener.Start();
                    listener.Stop();
                    return true;
                }
                case TransportType.Udp:
                {
                    using var listener = new UdpClient(new IPEndPoint(IPAddress.Any, port));
                    return true;
                }
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }
        catch (SocketException)
        {
            return false;
        }
    }
}
