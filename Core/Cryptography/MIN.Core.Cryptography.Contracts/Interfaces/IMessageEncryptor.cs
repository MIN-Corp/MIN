namespace MIN.Core.Cryptography.Contracts.Interfaces;

/// <summary>
/// Помощник в шифровании
/// </summary>
public interface IMessageEncryptor
{
    /// <summary>
    /// Проверить, инициализирована ли сессия с собеседником
    /// </summary>
    bool IsSessionInitialized(Guid partnerId);

    /// <summary>
    /// Вызывается при первом контакте с собеседником
    /// </summary>
    /// <param name="partnerId">Идентификатор партнёра (участника)</param>
    /// <param name="partnerPublicKey">Публичный ключ партнёра</param>
    Task InitializeSessionWithPartnerAsync(Guid partnerId, byte[] partnerPublicKey);

    /// <summary>
    /// Получить свой публичный ключ
    /// </summary>
    Task<byte[]> GetLocalPublicKey();

    /// <summary>
    /// Инициализировать сессию из сохранённого ключа партнёра (без перезаписи хранилища)
    /// </summary>
    /// <returns>false, если сохранённого ключа нет или он повреждён</returns>
    Task<bool> TryInitializeSessionFromStoredAsync(Guid partnerId);

    /// <summary>
    /// Получить отпечаток сохранённого публичного ключа партнёра (null, если ключ не сохранён)
    /// </summary>
    Task<byte[]?> TryGetPartnerKeyFingerprintAsync(Guid partnerId);

    /// <summary>
    /// Вычислить отпечаток произвольного (входящего) публичного ключа
    /// </summary>
    byte[] ComputeKeyFingerprint(byte[] publicKey);

    /// <summary>
    /// Закодировать сообщение
    /// </summary>
    /// <param name="data">Информация для зашифровки</param>
    /// <param name="partnerId">Идентификатор партнёра (участника)</param>
    byte[] EncryptMessage(byte[] data, Guid partnerId);

    /// <summary>
    /// Раскодировать сообщение
    /// </summary>
    /// <param name="encryptedData">Информация для расшифровки</param>
    /// <param name="partnerId">Идентификатор партнёра (участника)</param>
    byte[] DecryptMessage(byte[] encryptedData, Guid partnerId);
}
