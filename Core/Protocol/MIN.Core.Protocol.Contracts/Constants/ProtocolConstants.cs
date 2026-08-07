using MIN.Core.Transport.Contracts.Helpers;

namespace MIN.Core.Protocol.Contracts.Constants;

/// <summary>
/// Константы для протокола
/// </summary>
public class ProtocolConstants
{
    /// <summary>
    /// Кодовое слово для любого подключения
    /// </summary>
    public const string ResponseStarter = ConnectionPreamble.Magic;

    /// <summary>
    /// Таймаут ожидания подключения со стороны клиента
    /// </summary>
    public const int ClientSideTimeout = 5;

    /// <summary>
    /// Таймаут ожидания подключения со стороны хоста
    /// </summary>
    public const int ServerSideTimuout = 10;
}
