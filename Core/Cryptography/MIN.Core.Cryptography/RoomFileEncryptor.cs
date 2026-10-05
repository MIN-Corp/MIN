using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using MIN.Core.Cryptography.Contracts.Constants;
using MIN.Core.Cryptography.Contracts.Interfaces;
using MIN.Core.Identity.Contracts.Interfaces;
using MIN.Helpers.Contracts.Interfaces;
using MIN.Helpers.Contracts.Models.Enums;

namespace MIN.Core.Cryptography;

/// <inheritdoc cref="IRoomFileEncryptor"/>
public class RoomFileEncryptor : IRoomFileEncryptor, IDisposable
{
    private const string FileProtectorKey = "MIN.Core.Cryptography.RoomFileEncryption";
    private const int KeySize = 32; // AES-256

    private readonly IDataProtector protector;
    private readonly ILoggerProvider logger;
    private readonly SemaphoreSlim keyLock = new(1, 1);
    private readonly string masterKeyPath;
    private byte[]? masterKey;
    private bool disposed;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="RoomFileEncryptor"/>
    /// </summary>
    public RoomFileEncryptor(ILoggerProvider logger,
        IDataProtectionProvider dataProtection,
        IIdentityDataPathProvider identityDataPath)
    {
        var directory = Directory.CreateDirectory(identityDataPath.CryptographyDirectory).FullName;
        masterKeyPath = Path.Combine(directory, "rooms-master.key");
        protector = dataProtection.CreateProtector(FileProtectorKey);
        this.logger = logger;
    }


    byte[] IRoomFileEncryptor.Protect(byte[] data)
    {
        var key = GetMasterKey();
        var iv = RandomNumberGenerator.GetBytes(CryptographyConstants.IVSize);

        using var aes = new AesGcm(key, tagSizeInBytes: CryptographyConstants.AuthTagSize); // 16
        var ciphertext = new byte[data.Length];
        var tag = new byte[CryptographyConstants.AuthTagSize];
        aes.Encrypt(iv, data, ciphertext, tag);

        var payload = new byte[iv.Length + ciphertext.Length + tag.Length];
        Span<byte> span = payload;
        iv.CopyTo(span);
        ciphertext.CopyTo(span[iv.Length..]);
        tag.CopyTo(span[(iv.Length + ciphertext.Length)..]);
        return payload;
    }

    byte[] IRoomFileEncryptor.DecryptMessage(byte[] encryptedData)
    {
        var key = GetMasterKey();

        if (encryptedData.Length < CryptographyConstants.IVSize + CryptographyConstants.AuthTagSize)
        {
            logger.Log($"Invalid encrypted payload: length {encryptedData.Length}", LogLevel.Error);
            throw new InvalidDataException($"Invalid encrypted payload: length {encryptedData.Length}");
        }

        var iv = encryptedData.AsSpan(0, CryptographyConstants.IVSize).ToArray();
        var tag = encryptedData[^CryptographyConstants.AuthTagSize..].ToArray();
        var ciphertext = encryptedData.AsSpan(CryptographyConstants.IVSize,
            encryptedData.Length - CryptographyConstants.IVSize - CryptographyConstants.AuthTagSize).ToArray();

        using var aes = new AesGcm(key, tagSizeInBytes: CryptographyConstants.AuthTagSize);
        var plaintext = new byte[ciphertext.Length];
        try
        {
            aes.Decrypt(iv, ciphertext, tag, plaintext);
        }
        catch (AuthenticationTagMismatchException ex)
        {
            logger.Log($"Corrupted data: {ex.Message}", LogLevel.Error);
        }
        return plaintext;
    }

    private byte[] GetMasterKey()
    {
        if (masterKey != null)
        {
            return masterKey;
        }

        keyLock.Wait();
        try
        {
            if (masterKey != null)
            {
                return masterKey;
            }

            if (File.Exists(masterKeyPath))
            {
                var protectedBlob = File.ReadAllText(masterKeyPath);
                masterKey = protector.Unprotect(Convert.FromBase64String(protectedBlob));
            }
            else
            {
                masterKey = RandomNumberGenerator.GetBytes(KeySize);
                File.WriteAllText(masterKeyPath, Convert.ToBase64String(protector.Protect(masterKey)));
            }

            return masterKey;
        }
        finally
        {
            keyLock.Release();
        }
    }

    void IDisposable.Dispose()
    {
        if (disposed)
        {
            return;
        }

        keyLock.Dispose();
        disposed = true;
    }
}
