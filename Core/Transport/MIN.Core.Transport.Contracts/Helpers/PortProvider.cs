using System.Net;
using System.Net.Sockets;

namespace MIN.Core.Transport.Contracts.Helpers;

/// <summary>
/// Помошник в выделении порта
/// </summary>
public static class PortProvider
{
    private readonly static int minPort = 49152;
    private readonly static int maxPort = 65535;
    private readonly static int randomAttempts = 50;
    private readonly static HashSet<int> reserved = [];
    private readonly static Random random = new();

    /// <summary>
    /// Получить свободный порт
    /// </summary>
    public static int AllocatePort(short? preferredPort = 0, int? sequentialAttempts = 15)
    {
        if (preferredPort > 1024 && preferredPort < short.MaxValue && preferredPort != null && sequentialAttempts != null)
        {
            for (var i = 0; i < sequentialAttempts; i++)
            {
                var port = preferredPort.Value + i;
                if (!reserved.Contains(port) && IsPortFree(port))
                {
                    reserved.Add(port);
                    return port;
                }
            }
        }

        for (var i = 0; i < randomAttempts; i++)
        {
            var port = random.Next(minPort, maxPort + 1);
            if (!reserved.Contains(port) && IsPortFree(port))
            {
                reserved.Add(port);
                return port;
            }
        }

        throw new InvalidOperationException("No free port in dynamic range");
    }

    /// <summary>
    /// Отпустить порт
    /// </summary>
    public static void ReleasePort(int port) => reserved.Remove(port);

    private static bool IsPortFree(int port)
    {
        try
        {
            using var listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
            listener.Stop();
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }
}
