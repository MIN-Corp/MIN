using System.Text.Json;
using FluentAssertions;
using MIN.Core.Cryptography.Contracts.Models;
using MIN.Tests.Common;
using Xunit;

namespace MIN.Core.Cryptography.Tests;

/// <summary>
/// Тесты на <see cref="FileSystemKeyStorage"/>
/// </summary>
public class FileSystemKeyStorageTests
{
    /// <summary>
    /// Локальная пара ключей сохраняется и читается
    /// </summary>
    [Fact]
    public async Task SaveLoadLocalKeyPairShouldRoundTrip()
    {
        // Arrange
        using var paths = new TestIdentityDataPathProvider();
        using var storage = new FileSystemKeyStorage(paths, new TestLoggerProvider());
        var keyPair = new KeyPair { EncryptedEcdhPrivateKeyPem = "pem", EcdhPublicKeyBytes = [1, 2, 3] };

        // Act
        await storage.SaveLocalKeyPairAsync(keyPair);
        var loaded = await storage.LoadLocalKeyPairAsync();

        // Assert
        loaded.Should().NotBeNull();
        loaded!.EncryptedEcdhPrivateKeyPem.Should().Be("pem");
        loaded.EcdhPublicKeyBytes.Should().Equal([1, 2, 3]);
    }

    /// <summary>
    /// Отсутствующий файл ключей даёт null
    /// </summary>
    [Fact]
    public async Task LoadLocalKeyPairWhenMissingShouldReturnNull()
    {
        // Arrange
        using var paths = new TestIdentityDataPathProvider();
        using var storage = new FileSystemKeyStorage(paths, new TestLoggerProvider());

        // Act
        var loaded = await storage.LoadLocalKeyPairAsync();

        // Assert
        loaded.Should().BeNull();
    }

    /// <summary>
    /// Повреждённый файл ключей откладывается в .bak и даёт null (перегенерация)
    /// </summary>
    [Fact]
    public async Task CorruptedKeysFileShouldBackupAndReturnNull()
    {
        // Arrange
        using var paths = new TestIdentityDataPathProvider();
        using var storage = new FileSystemKeyStorage(paths, new TestLoggerProvider());
        var keysPath = Path.Combine(paths.CryptographyDirectory, "keys.json");
        await File.WriteAllTextAsync(keysPath, "{это не json");

        // Act
        var loaded = await storage.LoadLocalKeyPairAsync();

        // Assert
        loaded.Should().BeNull();
        File.Exists(keysPath + ".bak").Should().BeTrue();
    }

    /// <summary>
    /// Ключи нескольких партнёров сохраняются независимо
    /// </summary>
    [Fact]
    public async Task SavePartnerPublicKeysShouldKeepMultiplePartners()
    {
        // Arrange
        using var paths = new TestIdentityDataPathProvider();
        using var storage = new FileSystemKeyStorage(paths, new TestLoggerProvider());
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        // Act
        await storage.SavePartnerPublicKeyAsync(first, [1, 1, 1]);
        await storage.SavePartnerPublicKeyAsync(second, [2, 2, 2]);
        var loadedFirst = await storage.LoadPartnerPublicKeyAsync(first);
        var loadedSecond = await storage.LoadPartnerPublicKeyAsync(second);
        var loadedMissing = await storage.LoadPartnerPublicKeyAsync(Guid.NewGuid());

        // Assert
        loadedFirst.Should().Equal([1, 1, 1]);
        loadedSecond.Should().Equal([2, 2, 2]);
        loadedMissing.Should().BeNull();
    }

    /// <summary>
    /// Null вместо ключа партнёра отклоняется
    /// </summary>
    [Fact]
    public async Task SavePartnerPublicKeyNullShouldThrow()
    {
        // Arrange
        using var paths = new TestIdentityDataPathProvider();
        using var storage = new FileSystemKeyStorage(paths, new TestLoggerProvider());

        // Act
        var act = () => storage.SavePartnerPublicKeyAsync(Guid.NewGuid(), null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    /// <summary>
    /// Файл партнёров с невалидным base64 отклоняется с InvalidDataException
    /// </summary>
    [Fact]
    public async Task PartnersFileWithInvalidBase64ShouldThrowInvalidData()
    {
        // Arrange
        using var paths = new TestIdentityDataPathProvider();
        using var storage = new FileSystemKeyStorage(paths, new TestLoggerProvider());
        var partnersPath = Path.Combine(paths.CryptographyDirectory, "partners.json");
        var content = JsonSerializer.Serialize(new { schemaVersion = 1, data = new Dictionary<string, string> { [Guid.NewGuid().ToString()] = "!!!не base64!!!" } });
        await File.WriteAllTextAsync(partnersPath, content);

        // Act
        var act = () => storage.LoadPartnerPublicKeyAsync(Guid.NewGuid());

        // Assert
        await act.Should().ThrowAsync<InvalidDataException>();
    }

    /// <summary>
    /// Записи с не-Guid ключами пропускаются при чтении
    /// </summary>
    [Fact]
    public async Task PartnersFileWithInvalidGuidKeysShouldSkipThem()
    {
        // Arrange
        using var paths = new TestIdentityDataPathProvider();
        using var storage = new FileSystemKeyStorage(paths, new TestLoggerProvider());
        var partnersPath = Path.Combine(paths.CryptographyDirectory, "partners.json");
        var validId = Guid.NewGuid();
        var content = JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            data = new Dictionary<string, string>
            {
                ["not-a-guid"] = Convert.ToBase64String([1]),
                [validId.ToString()] = Convert.ToBase64String([2]),
            }
        });
        await File.WriteAllTextAsync(partnersPath, content);

        // Act
        var loaded = await storage.LoadPartnerPublicKeyAsync(validId);

        // Assert
        loaded.Should().Equal([2]);
    }
}
