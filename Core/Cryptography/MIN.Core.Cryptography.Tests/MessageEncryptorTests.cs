using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using MIN.Core.Cryptography.Contracts.Constants;
using MIN.Core.Cryptography.Contracts.Interfaces;
using MIN.Tests.Common;
using Xunit;

namespace MIN.Core.Cryptography.Tests;

/// <summary>
/// Тесты на <see cref="MessageEncryptor"/>
/// </summary>
public class MessageEncryptorTests
{
    /// <summary>
    /// Зашифрованное сообщение расшифровывается в исходные байты
    /// </summary>
    [Fact]
    public async Task EncryptDecryptShouldRoundTrip()
    {
        // Arrange
        using var pair = await EncryptorPair.CreateAsync();
        var plaintext = "Привет, MIN!"u8.ToArray();

        // Act
        var encrypted = pair.Alice.EncryptMessage(plaintext, pair.BobId);
        var decrypted = pair.Bob.DecryptMessage(encrypted, pair.AliceId);

        // Assert
        decrypted.Should().Equal(plaintext);
    }

    /// <summary>
    /// Шифртекст не равен открытому тексту и имеет размер plaintext + IV + tag
    /// </summary>
    [Fact]
    public async Task EncryptedShouldNotEqualPlain()
    {
        // Arrange
        using var pair = await EncryptorPair.CreateAsync();
        var plaintext = new byte[256];
        RandomNumberGenerator.Fill(plaintext);

        // Act
        var encrypted = pair.Alice.EncryptMessage(plaintext, pair.BobId);

        // Assert
        encrypted.Should().NotEqual(plaintext);
        encrypted.Length.Should().Be(plaintext.Length + CryptographyConstants.IvSize + CryptographyConstants.AuthTagSize);
        encrypted[CryptographyConstants.IvSize..^CryptographyConstants.AuthTagSize].Should().NotEqual(plaintext);
    }

    /// <summary>
    /// Каждый акт шифрования использует случайный IV
    /// </summary>
    [Fact]
    public async Task EncryptShouldUseRandomIv()
    {
        // Arrange
        using var pair = await EncryptorPair.CreateAsync();
        var plaintext = "одинаковый текст"u8.ToArray();

        // Act
        var first = pair.Alice.EncryptMessage(plaintext, pair.BobId);
        var second = pair.Alice.EncryptMessage(plaintext, pair.BobId);

        // Assert
        first.Should().NotEqual(second);
        first[..CryptographyConstants.IvSize].Should().NotEqual(second[..CryptographyConstants.IvSize]);
    }

    /// <summary>
    /// Пустое сообщение шифруется и расшифровывается
    /// </summary>
    [Fact]
    public async Task EmptyPayloadShouldRoundTrip()
    {
        // Arrange
        using var pair = await EncryptorPair.CreateAsync();

        // Act
        var encrypted = pair.Alice.EncryptMessage([], pair.BobId);
        var decrypted = pair.Bob.DecryptMessage(encrypted, pair.AliceId);

        // Assert
        encrypted.Length.Should().Be(CryptographyConstants.IvSize + CryptographyConstants.AuthTagSize);
        decrypted.Should().BeEmpty();
    }

    /// <summary>
    /// Большой payload (1 МиБ) проходит round-trip
    /// </summary>
    [Fact]
    public async Task LargePayloadShouldRoundTrip()
    {
        // Arrange
        using var pair = await EncryptorPair.CreateAsync();
        var plaintext = new byte[1024 * 1024];
        RandomNumberGenerator.Fill(plaintext);

        // Act
        var encrypted = pair.Alice.EncryptMessage(plaintext, pair.BobId);
        var decrypted = pair.Bob.DecryptMessage(encrypted, pair.AliceId);

        // Assert
        decrypted.Should().Equal(plaintext);
    }

    /// <summary>
    /// Подмена байта шифртекста ломает аутентификацию
    /// </summary>
    [Fact]
    public async Task DecryptTamperedCiphertextShouldThrow()
    {
        // Arrange
        using var pair = await EncryptorPair.CreateAsync();
        var encrypted = pair.Alice.EncryptMessage("данные"u8.ToArray(), pair.BobId);
        encrypted[CryptographyConstants.IvSize] ^= 0xFF;

        // Act
        var act = () => pair.Bob.DecryptMessage(encrypted, pair.AliceId);

        // Assert
        act.Should().Throw<AuthenticationTagMismatchException>();
    }

    /// <summary>
    /// Подмена тега аутентификации ломает расшифровку
    /// </summary>
    [Fact]
    public async Task DecryptTamperedTagShouldThrow()
    {
        // Arrange
        using var pair = await EncryptorPair.CreateAsync();
        var encrypted = pair.Alice.EncryptMessage("данные"u8.ToArray(), pair.BobId);
        encrypted[^1] ^= 0xFF;

        // Act
        var act = () => pair.Bob.DecryptMessage(encrypted, pair.AliceId);

        // Assert
        act.Should().Throw<AuthenticationTagMismatchException>();
    }

    /// <summary>
    /// Подмена IV ломает расшифровку
    /// </summary>
    [Fact]
    public async Task DecryptTamperedIvShouldThrow()
    {
        // Arrange
        using var pair = await EncryptorPair.CreateAsync();
        var encrypted = pair.Alice.EncryptMessage("данные"u8.ToArray(), pair.BobId);
        encrypted[0] ^= 0xFF;

        // Act
        var act = () => pair.Bob.DecryptMessage(encrypted, pair.AliceId);

        // Assert
        act.Should().Throw<AuthenticationTagMismatchException>();
    }

    /// <summary>
    /// Третья сторона с другими ключами не может расшифровать сообщение
    /// </summary>
    [Fact]
    public async Task DecryptWithWrongKeyShouldThrow()
    {
        // Arrange
        using var pair = await EncryptorPair.CreateAsync();
        using var carolPaths = new TestIdentityDataPathProvider();
        IMessageEncryptor carol = new MessageEncryptor(new TestLoggerProvider(), new EphemeralDataProtectionProvider(), carolPaths);
        await carol.InitializeSessionWithPartnerAsync(pair.BobId, await pair.Bob.GetLocalPublicKey());
        var encrypted = pair.Alice.EncryptMessage("секрет"u8.ToArray(), pair.BobId);

        // Act
        var act = () => carol.DecryptMessage(encrypted, pair.BobId);

        // Assert
        act.Should().Throw<AuthenticationTagMismatchException>();
        ((IDisposable)carol).Dispose();
    }

    /// <summary>
    /// Слишком короткие данные отклоняются с InvalidDataException
    /// </summary>
    [Fact]
    public async Task DecryptTooShortDataShouldThrowInvalidData()
    {
        // Arrange
        using var pair = await EncryptorPair.CreateAsync();

        // Act
        var act = () => pair.Bob.DecryptMessage(new byte[CryptographyConstants.IvSize + CryptographyConstants.AuthTagSize - 1], pair.AliceId);

        // Assert
        act.Should().Throw<InvalidDataException>();
    }

    /// <summary>
    /// Шифрование без инициализированной сессии отклоняется
    /// </summary>
    [Fact]
    public async Task EncryptWithoutSessionShouldThrow()
    {
        // Arrange
        using var pair = await EncryptorPair.CreateAsync();

        // Act
        var act = () => pair.Alice.EncryptMessage("данные"u8.ToArray(), Guid.NewGuid());

        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*Сессия не инициализирована*");
    }

    /// <summary>
    /// Шифрование на месте даёт тот же формат IV|ciphertext|tag
    /// </summary>
    [Fact]
    public async Task EncryptMessageInPlaceShouldRoundTrip()
    {
        // Arrange
        using var pair = await EncryptorPair.CreateAsync();
        var plaintext = "на месте"u8.ToArray();
        var buffer = new byte[CryptographyConstants.IvSize + plaintext.Length + CryptographyConstants.AuthTagSize];
        plaintext.CopyTo(buffer.AsSpan(CryptographyConstants.IvSize));

        // Act
        pair.Alice.EncryptMessageInPlace(buffer, pair.BobId);
        var decrypted = pair.Bob.DecryptMessage(buffer, pair.AliceId);

        // Assert
        decrypted.Should().Equal(plaintext);
    }

    /// <summary>
    /// Слишком короткий буфер для шифрования на месте отклоняется с InvalidDataException
    /// </summary>
    [Fact]
    public async Task EncryptMessageInPlaceTooShortShouldThrowInvalidData()
    {
        // Arrange
        using var pair = await EncryptorPair.CreateAsync();
        var buffer = new byte[CryptographyConstants.IvSize + CryptographyConstants.AuthTagSize - 1];

        // Act
        var act = () => pair.Alice.EncryptMessageInPlace(buffer, pair.BobId);

        // Assert
        act.Should().Throw<InvalidDataException>();
    }

    /// <summary>
    /// Сессия восстанавливается из сохранённого ключа партнёра после пересоздания шифратора
    /// </summary>
    [Fact]
    public async Task TryInitializeSessionFromStoredShouldRestoreSession()
    {
        // Arrange
        using var paths = new TestIdentityDataPathProvider();
        var dataProtection = new EphemeralDataProtectionProvider();
        var logger = new TestLoggerProvider();
        var partnerId = Guid.NewGuid();
        using var partnerPaths = new TestIdentityDataPathProvider();
        IMessageEncryptor partner = new MessageEncryptor(logger, dataProtection, partnerPaths);
        var partnerPublic = await partner.GetLocalPublicKey();

        IMessageEncryptor first = new MessageEncryptor(logger, dataProtection, paths);
        await first.InitializeSessionWithPartnerAsync(partnerId, partnerPublic);
        var encrypted = first.EncryptMessage("после перезапуска"u8.ToArray(), partnerId);
        ((IDisposable)first).Dispose();

        // Act
        IMessageEncryptor second = new MessageEncryptor(logger, dataProtection, paths);
        var restored = await second.TryInitializeSessionFromStoredAsync(partnerId);
        var decrypted = second.DecryptMessage(encrypted, partnerId);

        // Assert
        restored.Should().BeTrue();
        decrypted.Should().Equal("после перезапуска"u8.ToArray());
        ((IDisposable)second).Dispose();
        ((IDisposable)partner).Dispose();
    }

    /// <summary>
    /// Восстановление сессии для неизвестного партнёра возвращает false
    /// </summary>
    [Fact]
    public async Task TryInitializeSessionFromStoredUnknownPartnerShouldReturnFalse()
    {
        // Arrange
        using var paths = new TestIdentityDataPathProvider();
        IMessageEncryptor encryptor = new MessageEncryptor(new TestLoggerProvider(), new EphemeralDataProtectionProvider(), paths);

        // Act
        var restored = await encryptor.TryInitializeSessionFromStoredAsync(Guid.NewGuid());

        // Assert
        restored.Should().BeFalse();
        ((IDisposable)encryptor).Dispose();
    }

    /// <summary>
    /// Отпечаток ключа детерминирован и равен SHA-256 от байтов ключа
    /// </summary>
    [Fact]
    public async Task ComputeKeyFingerprintShouldBeDeterministicSha256()
    {
        // Arrange
        using var pair = await EncryptorPair.CreateAsync();
        var publicKey = await pair.Alice.GetLocalPublicKey();

        // Act
        var first = pair.Alice.ComputeKeyFingerprint(publicKey);
        var second = pair.Alice.ComputeKeyFingerprint(publicKey);

        // Assert
        first.Should().Equal(SHA256.HashData(publicKey));
        first.Should().Equal(second);
        first.Length.Should().Be(32);
    }

    /// <summary>
    /// Локальный публичный ключ стабилен между вызовами
    /// </summary>
    [Fact]
    public async Task GetLocalPublicKeyShouldBeStable()
    {
        // Arrange
        using var pair = await EncryptorPair.CreateAsync();

        // Act
        var first = await pair.Alice.GetLocalPublicKey();
        var second = await pair.Alice.GetLocalPublicKey();

        // Assert
        first.Should().Equal(second);
    }

    /// <summary>
    /// Отпечаток сохранённого ключа партнёра доступен после инициализации сессии
    /// </summary>
    [Fact]
    public async Task TryGetPartnerKeyFingerprintShouldReturnStoredKeyFingerprint()
    {
        // Arrange
        using var pair = await EncryptorPair.CreateAsync();
        var bobPublic = await pair.Bob.GetLocalPublicKey();

        // Act
        var fingerprint = await pair.Alice.TryGetPartnerKeyFingerprintAsync(pair.BobId);
        var missing = await pair.Alice.TryGetPartnerKeyFingerprintAsync(Guid.NewGuid());

        // Assert
        fingerprint.Should().Equal(SHA256.HashData(bobPublic));
        missing.Should().BeNull();
    }

    /// <summary>
    /// Шифрование и расшифровка 1 МиБ выполняются за разумное время
    /// </summary>
    [Fact]
    public async Task EncryptDecryptPerformanceShouldBeReasonable()
    {
        // Arrange
        using var pair = await EncryptorPair.CreateAsync();
        var plaintext = new byte[1024 * 1024];
        RandomNumberGenerator.Fill(plaintext);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        for (var i = 0; i < 50; i++)
        {
            var encrypted = pair.Alice.EncryptMessage(plaintext, pair.BobId);
            pair.Bob.DecryptMessage(encrypted, pair.AliceId);
        }

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000, "50 циклов AES-GCM по 1 МиБ не должны занимать вечность");
    }
}
