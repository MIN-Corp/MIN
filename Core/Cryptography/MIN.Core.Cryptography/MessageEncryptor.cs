using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using MIN.Core.Cryptography.Contracts.Constants;
using MIN.Core.Cryptography.Contracts.Interfaces;
using MIN.Core.Identity.Contracts.Interfaces;
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
        IIdentityDataPathProvider identityDataPathProvider)
    {
        this.logger = logger;
        keyProvider = new KeyProvider(dataProtection, identityDataPathProvider, logger);
    }

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
            logger.Log($"Не удалось вычислить сохранённый секрет для партнёра {partnerId}", LogLevel.Error);
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

        var result = new byte[CryptographyConstants.IvSize + plaintext.Length + CryptographyConstants.AuthTagSize];
        var iv = result.AsSpan(0, CryptographyConstants.IvSize);
        RandomNumberGenerator.Fill(iv);

        var ciphertext = result.AsSpan(CryptographyConstants.IvSize, plaintext.Length);
        var authTag = result.AsSpan(CryptographyConstants.IvSize + plaintext.Length, CryptographyConstants.AuthTagSize);

        using var aesGcm = new AesGcm(key, tagSizeInBytes: CryptographyConstants.AuthTagSize);
        aesGcm.Encrypt(iv, plaintext, ciphertext, authTag);

        return result;
    }

    void IMessageEncryptor.EncryptMessageInPlace(Span<byte> ivPlaintextTag, Guid partnerId)
    {
        var key = GetSessionKey(partnerId);

        var iv = ivPlaintextTag[..CryptographyConstants.IvSize];
        RandomNumberGenerator.Fill(iv);

        var plaintext = ivPlaintextTag[CryptographyConstants.IvSize..^CryptographyConstants.AuthTagSize];

        using var aesGcm = new AesGcm(key, tagSizeInBytes: CryptographyConstants.AuthTagSize);
        aesGcm.Encrypt(iv, plaintext, plaintext, ivPlaintextTag[^CryptographyConstants.AuthTagSize..]);
    }

    byte[] IMessageEncryptor.DecryptMessage(ReadOnlyMemory<byte> encryptedData, Guid partnerId)
    {
        var key = GetSessionKey(partnerId);

        if (encryptedData.Length < CryptographyConstants.IvSize + CryptographyConstants.AuthTagSize)
        {
            throw new InvalidDataException($"Invalid encrypted data: length {encryptedData.Length} < 28");
        }

        var data = encryptedData.Span;
        var ciphertextLength = encryptedData.Length - CryptographyConstants.IvSize - CryptographyConstants.AuthTagSize;
        var plaintext = new byte[ciphertextLength];

        Span<byte> iv = stackalloc byte[CryptographyConstants.IvSize];
        data[..CryptographyConstants.IvSize].CopyTo(iv);

        Span<byte> authTag = stackalloc byte[CryptographyConstants.AuthTagSize];
        data[^CryptographyConstants.AuthTagSize..].CopyTo(authTag);

        using var aesGcm = new AesGcm(key, tagSizeInBytes: CryptographyConstants.AuthTagSize);

        try
        {
            aesGcm.Decrypt(iv, data.Slice(CryptographyConstants.IvSize, ciphertextLength), authTag, plaintext);
            return plaintext;
        }
        catch (CryptographicException ex) when (ex is AuthenticationTagMismatchException)
        {
            logger.Log($"Ошибка аутентификации — ключи не совпадают", LogLevel.Error);
            throw;
        }
        catch (CryptographicException ex)
        {
            logger.Log($"Возникла криптографическая ошибка: {ex.Message}", LogLevel.Error);
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
