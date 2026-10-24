using MIN.Sessions.Core.Messaging.Contracts.Enums;

namespace MIN.Sessions.Core.Services.Tunnels;

/// <summary>
/// Протокол фреймов туннеля: [frameType: 1 байт][payload]
/// </summary>
public static class TunnelProtocol
{
    /// <summary>
    /// Максимальный размер полезной нагрузки одного фрейма
    /// </summary>
    public const int ChunkSize = 32 * 1024;

    /// <summary>
    /// Кодировать фрейм
    /// </summary>
    public static byte[] Encode(TunnelFrameType type, ReadOnlySpan<byte> payload = default)
    {
        var frame = new byte[1 + payload.Length];
        frame[0] = (byte)type;
        payload.CopyTo(frame.AsSpan(1));
        return frame;
    }

    /// <summary>
    /// Декодировать фрейм
    /// </summary>
    public static (TunnelFrameType Type, byte[] Payload) Decode(ReadOnlySpan<byte> frame)
    {
        if (frame.Length == 0)
        {
            throw new InvalidDataException("Пустой фрейм туннеля");
        }

        return ((TunnelFrameType)frame[0], frame.Slice(1).ToArray());
    }
}
