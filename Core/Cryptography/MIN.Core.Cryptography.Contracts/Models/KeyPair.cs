namespace MIN.Core.Cryptography.Contracts.Models;

/// <summary>
/// Пара ключей
/// </summary>
public class KeyPair
{
    /// <summary>
    /// Зашифрованный приватный ключ для шифрования (ECDH)
    /// </summary>
    public string EncryptedEcdhPrivateKeyPem { get; init; } = string.Empty;

    /// <summary>
    /// Для сетевого обмена
    /// </summary>
    public byte[] EcdhPublicKeyBytes { get; init; } = null!;
}
