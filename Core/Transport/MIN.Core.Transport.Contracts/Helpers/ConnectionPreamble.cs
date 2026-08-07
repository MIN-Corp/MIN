using System.Text;

namespace MIN.Core.Transport.Contracts.Helpers;

/// <summary>
/// Сервис преамбулы MIN-соединения: первый TCP-фрейм [3 байта "MIN"][16 байт Guid].
/// Клиент объявляет через него свой connectionId (единый для TCP/UDP ног),
/// сервер читает преамбулу до создания логического соединения.
/// нужен для синхронизации id соединения
/// </summary>
public static class ConnectionPreamble
{
    private readonly static byte[] magicBytes = Encoding.ASCII.GetBytes(Magic);

    /// <summary>
    /// Магия протокола. Единый источник истины — ProtocolConstants ссылается на неё
    /// </summary>
    public const string Magic = "MIN";

    /// <summary>
    /// Размер Guid соединения в байтах
    /// </summary>
    public const int ConnectionIdSize = 16;

    /// <summary>
    /// Размер ожидаемой прембулы
    /// </summary>
    public readonly static int Size = Magic.Length + ConnectionIdSize;

    /// <summary>
    /// Таймаут ожидания преамбулы от нового клиента
    /// </summary>
    public readonly static TimeSpan Timeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Создать сообщение преамбулы
    /// </summary>
    public static byte[] Create(Guid connectionId)
    {
        var frame = new byte[Size];
        magicBytes.CopyTo(frame, 0);
        connectionId.TryWriteBytes(frame.AsSpan(Magic.Length, ConnectionIdSize));
        return frame;
    }

    /// <summary>
    /// Попытаться распарсить пакет на полезные данные в виде преамбулы
    /// </summary>
    public static bool TryParse(ReadOnlySpan<byte> frame, out Guid connectionId)
    {
        connectionId = Guid.Empty;

        if (frame.Length != Size)
        {
            return false;
        }

        if (!frame.StartsWith(magicBytes))
        {
            return false;
        }

        connectionId = new Guid(frame.Slice(magicBytes.Length, ConnectionIdSize));
        return connectionId != Guid.Empty;
    }
}
