using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using MIN.Core.Cryptography.Contracts.Constants;
using MIN.Core.Cryptography.Contracts.Interfaces;
using MIN.Helpers.Contracts.Interfaces;
using MIN.Helpers.Contracts.Models.Enums;

namespace MIN.Core.Cryptography;

/// <inheritdoc cref="IMessageEncryptor"/>
public class MessageEncryptor : IMessageEncryptor, IDisposable
{
    private readonly ILoggerProvider logger;
    private readonly KeyProvider keyProvider;
    private readonly ConcurrentDictionary<Guid, byte[]> sharedSecrets = new();
    private bool disposed;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="MessageEncryptor"/>
    /// </summary>
    public MessageEncryptor(ILoggerProvider logger,
        IDataProtectionProvider dataProtection,
        IAppDataProvider appDataProvider)
    {
        this.logger = logger;
        keyProvider = new KeyProvider(dataProtection, appDataProvider, logger);
    }

    bool IMessageEncryptor.IsSessionInitialized(Guid partnerId)
        => sharedSecrets.ContainsKey(partnerId);

    async Task IMessageEncryptor.InitializeSessionWithPartnerAsync(Guid partnerId, byte[] partnerPublicKey)
    {
        var sharedSecret = await keyProvider.ComputeSharedSecretAsync(partnerPublicKey);
        sharedSecrets[partnerId] = sharedSecret;

        await keyProvider.SavePartnerPublicKeyAsync(partnerId, partnerPublicKey);
    }

    async Task<bool> IMessageEncryptor.TryInitializeSessionFromStoredAsync(Guid partnerId)
    {
        if (sharedSecrets.ContainsKey(partnerId))
        {
            return true;
        }

        var sharedSecret = await keyProvider.TryComputeStoredSharedSecretAsync(partnerId);

        if (sharedSecret == null)
        {
            logger.Log($"Не удалось ", LogLevel.Error);
            return false;
        }

        sharedSecrets[partnerId] = sharedSecret;
        return true;
    }

    async Task<byte[]?> IMessageEncryptor.TryGetPartnerKeyFingerprintAsync(Guid partnerId)
    {
        var storedKey = await keyProvider.GetPartnerPublicKeyAsync(partnerId);

        return storedKey == null ? null : KeyProvider.ComputeKeyFingerprint(storedKey);
    }

    byte[] IMessageEncryptor.ComputeKeyFingerprint(byte[] publicKey)
        => KeyProvider.ComputeKeyFingerprint(publicKey);

    async Task<byte[]> IMessageEncryptor.GetLocalPublicKey()
    {
        var keys = await keyProvider.GetLocalKeysAsync();
        return keys.EcdhPublicKeyBytes;
    }

    byte[] IMessageEncryptor.EncryptMessage(byte[] plaintext, Guid partnerId)
    {
        var key = GetSessionKey(partnerId);
        var iv = RandomNumberGenerator.GetBytes(CryptographyConstants.IVSize);

        using var aesGcm = new AesGcm(key, tagSizeInBytes: CryptographyConstants.AuthTagSize);
        var ciphertext = new byte[plaintext.Length];
        var authTag = new byte[CryptographyConstants.AuthTagSize];
        aesGcm.Encrypt(iv, plaintext, ciphertext, authTag);

        using var ms = new MemoryStream();
        ms.Write(iv, 0, CryptographyConstants.IVSize);
        ms.Write(ciphertext, 0, ciphertext.Length);
        ms.Write(authTag, 0, CryptographyConstants.AuthTagSize);

        return ms.ToArray();
    }

    byte[] IMessageEncryptor.DecryptMessage(byte[] encryptedData, Guid partnerId)
    {
        var key = GetSessionKey(partnerId);

        if (encryptedData.Length < CryptographyConstants.IVSize + CryptographyConstants.AuthTagSize)
        {
            throw new InvalidDataException($"Invalid encrypted data: length {encryptedData.Length} < 28");
        }

        var iv = encryptedData.AsSpan(0, CryptographyConstants.IVSize).ToArray();
        var authTag = encryptedData.AsSpan(encryptedData.Length - 16, 16).ToArray();
        var ciphertextLength = encryptedData.Length - CryptographyConstants.IVSize - CryptographyConstants.AuthTagSize;
        var ciphertext = encryptedData.AsSpan(CryptographyConstants.IVSize, ciphertextLength).ToArray();

        using var aesGcm = new AesGcm(key, tagSizeInBytes: CryptographyConstants.AuthTagSize);
        var plaintext = new byte[ciphertext.Length];

        try
        {
            aesGcm.Decrypt(iv, ciphertext, authTag, plaintext);
            return plaintext;
        }
        catch (CryptographicException ex) when (ex is AuthenticationTagMismatchException)
        {
            logger.Log($"Ошибка аутентификации — ключи не совпадают", LogLevel.Error);
            throw;
        }
        catch (CryptographicException ex)
        {
            logger.Log($"Возникла крипто-ошибка: {ex.Message}", LogLevel.Error);
            throw;
        }
    }

    private byte[] GetSessionKey(Guid partnerId)
    {
        if (sharedSecrets.TryGetValue(partnerId, out var key))
        {
            return key;
        }

        throw new InvalidOperationException($"Сессия не инициализирована для партнёра с id: {partnerId}");
    }

    void IDisposable.Dispose()
    {
        if (disposed)
        {
            return;
        }

        keyProvider.Dispose();
        disposed = true;
    }
}
