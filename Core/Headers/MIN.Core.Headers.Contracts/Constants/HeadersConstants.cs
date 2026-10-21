using MIN.Core.Cryptography.Contracts.Constants;

namespace MIN.Core.Headers.Contracts.Constants;

/// <summary>
/// Конфигурация для Headers
/// </summary>
public static class HeadersConstants
{
    /// <summary>
    /// Размер заголовка потокового пакета (1 байт флагов + 16 байт StreamId + 4 байта индекса + 4 байта количества)
    /// </summary>
    public const int StreamHeaderSize = 25;

    /// <summary>
    /// Размер заголовка шифрования
    /// </summary>
    public const int EncryptionHeaderSize = 1 + CryptographyConstants.IvSize + CryptographyConstants.AuthTagSize;
}
