using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using MIN.Core.Cryptography;
using MIN.Tests.Common;
using Xunit;

namespace MIN.Core.Cryptography.Tests;

/// <summary>
/// Тесты на <see cref="KeyProvider"/>
/// </summary>
public class KeyProviderTests
{
    /// <summary>
    /// Локальная пара ключей генерируется и переживает пересоздание провайдера
    /// </summary>
    [Fact]
    public async Task GetLocalKeysShouldPersistAcrossInstances()
    {
        // Arrange
        using var paths = new TestIdentityDataPathProvider();
        var dataProtection = new EphemeralDataProtectionProvider();
        var logger = new TestLoggerProvider();
        var first = new KeyProvider(dataProtection, paths, logger);
        var firstKeys = await first.GetLocalKeysAsync();
        first.Dispose();

        // Act
        var second = new KeyProvider(dataProtection, paths, logger);
        var secondKeys = await second.GetLocalKeysAsync();
        second.Dispose();

        // Assert
        secondKeys.EcdhPublicKeyBytes.Should().Equal(firstKeys.EcdhPublicKeyBytes);
    }

    /// <summary>
    /// Общий секрет симметричен: оба участника вычисляют одинаковый ключ
    /// </summary>
    [Fact]
    public async Task ComputeSharedSecretShouldBeSymmetric()
    {
        // Arrange
        using var alicePaths = new TestIdentityDataPathProvider();
        using var bobPaths = new TestIdentityDataPathProvider();
        var dataProtection = new EphemeralDataProtectionProvider();
        var logger = new TestLoggerProvider();
        using var alice = new KeyProvider(dataProtection, alicePaths, logger);
        using var bob = new KeyProvider(dataProtection, bobPaths, logger);
        var alicePublic = (await alice.GetLocalKeysAsync()).EcdhPublicKeyBytes;
        var bobPublic = (await bob.GetLocalKeysAsync()).EcdhPublicKeyBytes;

        // Act
        var aliceSecret = await alice.ComputeSharedSecretAsync(bobPublic);
        var bobSecret = await bob.ComputeSharedSecretAsync(alicePublic);

        // Assert
        aliceSecret.Should().Equal(bobSecret);
        aliceSecret.Length.Should().Be(32);
    }

    /// <summary>
    /// Мусор вместо публичного ключа партнёра отклоняется
    /// </summary>
    [Fact]
    public async Task ComputeSharedSecretWithGarbageKeyShouldThrow()
    {
        // Arrange
        using var paths = new TestIdentityDataPathProvider();
        using var provider = new KeyProvider(new EphemeralDataProtectionProvider(), paths, new TestLoggerProvider());

        // Act
        var act = () => provider.ComputeSharedSecretAsync([1, 2, 3, 4, 5]);

        // Assert
        await act.Should().ThrowAsync<CryptographicException>();
    }

    /// <summary>
    /// Сохранённый секрет для неизвестного партнёра отсутствует
    /// </summary>
    [Fact]
    public async Task TryComputeStoredSharedSecretUnknownShouldReturnNull()
    {
        // Arrange
        using var paths = new TestIdentityDataPathProvider();
        using var provider = new KeyProvider(new EphemeralDataProtectionProvider(), paths, new TestLoggerProvider());

        // Act
        var secret = await provider.TryComputeStoredSharedSecretAsync(Guid.NewGuid());

        // Assert
        secret.Should().BeNull();
    }

    /// <summary>
    /// Повреждённый сохранённый ключ партнёра даёт null, а не падение
    /// </summary>
    [Fact]
    public async Task TryComputeStoredSharedSecretCorruptedShouldReturnNull()
    {
        // Arrange
        using var paths = new TestIdentityDataPathProvider();
        using var provider = new KeyProvider(new EphemeralDataProtectionProvider(), paths, new TestLoggerProvider());
        var partnerId = Guid.NewGuid();
        await provider.SavePartnerPublicKeyAsync(partnerId, [9, 9, 9]);

        // Act
        var secret = await provider.TryComputeStoredSharedSecretAsync(partnerId);

        // Assert
        secret.Should().BeNull();
    }

    /// <summary>
    /// Сохранённый ключ партнёра читается обратно
    /// </summary>
    [Fact]
    public async Task SavePartnerPublicKeyShouldRoundTrip()
    {
        // Arrange
        using var paths = new TestIdentityDataPathProvider();
        using var provider = new KeyProvider(new EphemeralDataProtectionProvider(), paths, new TestLoggerProvider());
        var partnerId = Guid.NewGuid();
        var key = new byte[91];
        RandomNumberGenerator.Fill(key);

        // Act
        await provider.SavePartnerPublicKeyAsync(partnerId, key);
        var loaded = await provider.GetPartnerPublicKeyAsync(partnerId);

        // Assert
        loaded.Should().Equal(key);
    }

    /// <summary>
    /// Отпечаток ключа: null и пустой массив отклоняются
    /// </summary>
    [Fact]
    public void ComputeKeyFingerprintShouldRejectNullAndEmpty()
    {
        // Act
        var nullAct = () => KeyProvider.ComputeKeyFingerprint(null!);
        var emptyAct = () => KeyProvider.ComputeKeyFingerprint([]);

        // Assert
        nullAct.Should().Throw<ArgumentNullException>();
        emptyAct.Should().Throw<ArgumentException>();
    }

    /// <summary>
    /// Параллельные запросы локальных ключей возвращают один и тот же экземпляр
    /// </summary>
    [Fact]
    public async Task ConcurrentGetLocalKeysShouldReturnSameInstance()
    {
        // Arrange
        using var paths = new TestIdentityDataPathProvider();
        using var provider = new KeyProvider(new EphemeralDataProtectionProvider(), paths, new TestLoggerProvider());

        // Act
        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => provider.GetLocalKeysAsync()));

        // Assert
        results.Should().AllSatisfy(keys => keys.Should().BeSameAs(results[0]));
    }
}
