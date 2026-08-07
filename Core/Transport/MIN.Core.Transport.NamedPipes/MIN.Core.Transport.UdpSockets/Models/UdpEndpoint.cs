using System.Text;
using MIN.Core.Transport.Contracts.Enum;
using MIN.Core.Transport.Contracts.Interfaces;

namespace MIN.Core.Transport.UdpSockets.Models;

/// <summary>
/// Точка подключения к комнате на основе UDP Sockets
/// </summary>
public sealed class UdpEndpoint : IEndpoint
{
    /// <inheritdoc />
    public TransportType Type => TransportType.Udp;

    /// <inheritdoc />
    public AddressOrigin Origin { get; set; }

    /// <summary>
    /// IP Адрес
    /// </summary>
    public string IPAddress { get; set; } = string.Empty;

    /// <summary>
    /// Имя сети (если есть)
    /// </summary>
    public string? NetworkName { get; set; }

    /// <summary>
    /// Динамически созданный порт
    /// </summary>
    public int Port { get; set; }

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="UdpEndpoint"/>
    /// </summary>
    public UdpEndpoint() { }

    /// <inheritdoc />
    public override string ToString()
    {
        var tostringSb = new StringBuilder();
        tostringSb.Append(Origin);
        tostringSb.Append(' ');

        if (NetworkName != null)
        {
            tostringSb.Append(NetworkName);
            tostringSb.Append(' ');
        }

        tostringSb.Append(IPAddress);
        tostringSb.Append(':');
        tostringSb.Append(Port);

        return tostringSb.ToString();
    }

    string IEndpoint.GetOrigin()
    {
        var tostringSb = new StringBuilder();
        tostringSb.Append(Origin);
        tostringSb.Append(' ');

        if (NetworkName != null)
        {
            tostringSb.Append(NetworkName);
        }

        return tostringSb.ToString();
    }

    string IEndpoint.GetAddress()
    {
        var tostringSb = new StringBuilder();
        tostringSb.Append(IPAddress);
        tostringSb.Append(':');
        tostringSb.Append(Port);

        return tostringSb.ToString();
    }
}
