using System.Text;
using MIN.Core.Transport.Contracts.Enum;
using MIN.Core.Transport.Contracts.Interfaces;

namespace MIN.Core.Transport.TcpSockets.Models;

/// <summary>
/// Точка подключения к комнате на основе TCP Sockets
/// </summary>
public sealed class TcpEndpoint : IEndpoint
{
    /// <inheritdoc />
    public TransportType Type => TransportType.Tcp;

    /// <inheritdoc />
    public AddressOrigin Origin { get; set; }

    /// <summary>
    /// IP Адрес
    /// </summary>
    public string IpAddress { get; set; } = string.Empty;

    /// <summary>
    /// Имя сети (если есть)
    /// </summary>
    public string? NetworkName { get; set; }

    /// <summary>
    /// Динамически созданный порт
    /// </summary>
    public int Port { get; set; }

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="TcpEndpoint"/>
    /// </summary>
    public TcpEndpoint() { }

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

        tostringSb.Append(IpAddress);
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
        tostringSb.Append(IpAddress);
        tostringSb.Append(':');
        tostringSb.Append(Port);

        return tostringSb.ToString();
    }
}
