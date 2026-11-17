using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using MIN.Core.Cryptography;
using MIN.Core.Cryptography.Contracts.Constants;
using MIN.Core.Cryptography.Contracts.Interfaces;
using MIN.Tests.Common;
using Xunit;

namespace MIN.Core.Cryptography.Tests;

/// <summary>
/// Тесты на <see cref="RoomFileEncryptor"/>
/// </summary>
public class RoomFileEncryptorTests
{
    /// <summary>
    /// Защищённые данные комнаты расшифровываются в исходные байты
    /// </summary>
    [Fact]
    public void ProtectDecryptShouldRoundTrip()
    {
        // Arrange
        using var paths = new TestIdentityDataPathProvider();
        IRoomFileEncryptor encryptor = new RoomFileEncryptor(new TestLoggerProvider(), new EphemeralDataProtectionProvider(), paths);
        var data = "история комнаты"u8.ToArray();

        // Act
        var protectedBytes = encryptor.Protect(data);
        var decrypted = encryptor.DecryptMessage(protectedBytes);

        // Assert
        decrypted.Should().Equal(data);
        ((IDisposable)encryptor).Dispose();
    }

    /// <summary>
    /// Защищённые данные не равны исходным и имеют размер data + IV + tag
    /// </summary>
    [Fact]
    public void ProtectShouldNotEqualPlain()
    {
        // Arrange
        using var paths = new TestIdentityDataPathProvider();
        IRoomFileEncryptor encryptor = new RoomFileEncryptor(new TestLoggerProvider(), new EphemeralDataProtectionProvider(), paths);
        var data = new byte[128];
        RandomNumberGenerator.Fill(data);

        // Act
        var protectedBytes = encryptor.Protect(data);

        // Assert
        protectedBytes.Should().NotEqual(data);
        protectedBytes.Length.Should().Be(data.Length + CryptographyConstants.IvSize + CryptographyConstants.AuthTagSize);
        ((IDisposable)encryptor).Dispose();
    }

    /// <summary>
    /// Повторная защита тех же данных даёт другой результат (случайный IV)
    /// </summary>
    [Fact]
    public void ProtectTwiceShouldDiffer()
    {
        // Arrange
        using var paths = new TestIdentityDataPathProvider();
        IRoomFileEncryptor encryptor = new RoomFileEncryptor(new TestLoggerProvider(), new EphemeralDataProtectionProvider(), paths);
        var data = "данные"u8.ToArray();

        // Act
        var first = encryptor.Protect(data);
        var second = encryptor.Protect(data);

        // Assert
        first.Should().NotEqual(second);
        ((IDisposable)encryptor).Dispose();
    }

    /// <summary>
    /// Пустой payload защищается и расшифровывается
    /// </summary>
    [Fact]
    public void EmptyPayloadShouldRoundTrip()
    {
        // Arrange
        using var paths = new TestIdentityDataPathProvider();
        IRoomFileEncryptor encryptor = new RoomFileEncryptor(new TestLoggerProvider(), new EphemeralDataProtectionProvider(), paths);

        // Act
        var protectedBytes = encryptor.Protect([]);
        var decrypted = encryptor.DecryptMessage(protectedBytes);

        // Assert
        decrypted.Should().BeEmpty();
        ((IDisposable)encryptor).Dispose();
    }

    /// <summary>
    /// Слишком короткий payload отклоняется с InvalidDataException
    /// </summary>
    [Fact]
    public void DecryptTooShortShouldThrowInvalidData()
    {
        // Arrange
        using var paths = new TestIdentityDataPathProvider();
        IRoomFileEncryptor encryptor = new RoomFileEncryptor(new TestLoggerProvider(), new EphemeralDataProtectionProvider(), paths);

        // Act
        var act = () => encryptor.DecryptMessage(new byte[27]);

        // Assert
        act.Should().Throw<InvalidDataException>();
        ((IDisposable)encryptor).Dispose();
    }

    /// <summary>
    /// Повреждённые данные: текущее поведение — нулевой plaintext и ошибка в логе
    /// </summary>
    [Fact]
    public void DecryptTamperedShouldReturnZerosAndLogError()
    {
        // Arrange
        using var paths = new TestIdentityDataPathProvider();
        var logger = new TestLoggerProvider();
        IRoomFileEncryptor encryptor = new RoomFileEncryptor(logger, new EphemeralDataProtectionProvider(), paths);
        var protectedBytes = encryptor.Protect("данные"u8.ToArray());
        protectedBytes[^1] ^= 0xFF;

        // Act
        var decrypted = encryptor.DecryptMessage(protectedBytes);

        // Assert
        decrypted.Should().OnlyContain(b => b == 0);
        logger.HasErrors.Should().BeTrue();
        ((IDisposable)encryptor).Dispose();
    }

    /// <summary>
    /// Мастер-ключ комнаты переживает пересоздание шифратора
    /// </summary>
    [Fact]
    public void MasterKeyShouldPersistAcrossInstances()
    {
        // Arrange
        using var paths = new TestIdentityDataPathProvider();
        var dataProtection = new EphemeralDataProtectionProvider();
        var logger = new TestLoggerProvider();
        IRoomFileEncryptor first = new RoomFileEncryptor(logger, dataProtection, paths);
        var protectedBytes = first.Protect("ключ должен сохраниться"u8.ToArray());
        ((IDisposable)first).Dispose();

        // Act
        IRoomFileEncryptor second = new RoomFileEncryptor(logger, dataProtection, paths);
        var decrypted = second.DecryptMessage(protectedBytes);

        // Assert
        decrypted.Should().Equal("ключ должен сохраниться"u8.ToArray());
        ((IDisposable)second).Dispose();
    }

    /// <summary>
    /// Другой мастер-ключ не расшифровывает данные (нулевой результат)
    /// </summary>
    [Fact]
    public void DifferentMasterKeyShouldNotDecrypt()
    {
        // Arrange
        using var firstPaths = new TestIdentityDataPathProvider();
        using var secondPaths = new TestIdentityDataPathProvider();
        var dataProtection = new EphemeralDataProtectionProvider();
        IRoomFileEncryptor first = new RoomFileEncryptor(new TestLoggerProvider(), dataProtection, firstPaths);
        IRoomFileEncryptor second = new RoomFileEncryptor(new TestLoggerProvider(), dataProtection, secondPaths);
        var protectedBytes = first.Protect("чужие данные"u8.ToArray());

        // Act
        var decrypted = second.DecryptMessage(protectedBytes);

        // Assert
        decrypted.Should().OnlyContain(b => b == 0);
        ((IDisposable)first).Dispose();
        ((IDisposable)second).Dispose();
    }
}
