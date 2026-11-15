namespace MIN.Core.Cryptography.Contracts.Interfaces;

/// <summary>
/// Помощник в шифровании сообщений
/// </summary>
public interface IMessageEncryptor
{
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
    /// <returns>False, если сохранённого ключа нет или он повреждён</returns>
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
    /// Зашифровать сообщение
    /// </summary>
    /// <param name="data">Информация для зашифровки</param>
    /// <param name="partnerId">Идентификатор партнёра (участника)</param>
    byte[] EncryptMessage(byte[] data, Guid partnerId);

    /// <summary>
    /// Зашифровать на месте: буфер формата [IV(12)][plaintext][tag(16)]
    /// </summary>
    /// <remarks>IV и tag заполняются методом, plaintext перезаписывается шифротекстом (in-place)</remarks>
    void EncryptMessageInPlace(Span<byte> ivPlaintextTag, Guid partnerId);

    /// <summary>
    /// Расшифровать сообщение: [IV(12)][ciphertext][authTag(16)] -> plaintext
    /// </summary>
    /// <param name="encryptedData">Информация для расшифровки</param>
    /// <param name="partnerId">Идентификатор партнёра (участника)</param>
    byte[] DecryptMessage(ReadOnlyMemory<byte> encryptedData, Guid partnerId);
}
