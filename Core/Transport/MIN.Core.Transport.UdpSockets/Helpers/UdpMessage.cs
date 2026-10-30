namespace MIN.Core.Transport.UdpSockets.Helpers;

/// <summary>
/// Форматтер данных для udp сообщений
/// Формат udp датаграммы: [16 байт: Guid соединения][payload]
/// </summary>
static internal class UdpMessage
{
    public const int ConnectionIdSize = 16;

    public static byte[] Wrap(Guid connectionId, byte[] payload)
    {
        var datagram = new byte[ConnectionIdSize + payload.Length];
        connectionId.TryWriteBytes(datagram);
        Buffer.BlockCopy(payload, 0, datagram, ConnectionIdSize, payload.Length);
        return datagram;
    }

    public static (Guid ConnectionId, byte[] Payload) Parse(byte[] datagram)
    {
        if (datagram.Length < ConnectionIdSize)
        {
            throw new ArgumentException($"Датаграмма короче заголовка ({ConnectionIdSize} байт)");
        }

        var connectionId = new Guid(datagram.AsSpan(0, ConnectionIdSize));
        var payload = datagram.AsSpan(ConnectionIdSize).ToArray();
        return (connectionId, payload);
    }
}
