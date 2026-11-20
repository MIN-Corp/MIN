using Microsoft.AspNetCore.DataProtection;
using MIN.Core.Cryptography;
using MIN.Core.Cryptography.Contracts.Interfaces;

namespace MIN.Tests.Common;

/// <summary>
/// Пара шифраторов с согласованными ECDH-сессиями (Алиса и Боб)
/// </summary>
public sealed class EncryptorPair : IDisposable
{
    private readonly TestIdentityDataPathProvider alicePaths;
    private readonly TestIdentityDataPathProvider bobPaths;

    /// <summary>
    /// Шифратор Алисы
    /// </summary>
    public IMessageEncryptor Alice { get; }

    /// <summary>
    /// Шифратор Боба
    /// </summary>
    public IMessageEncryptor Bob { get; }

    /// <summary>
    /// Идентификатор партнёра-Алисы (с точки зрения Боба)
    /// </summary>
    public Guid AliceId { get; } = Guid.NewGuid();

    /// <summary>
    /// Идентификатор партнёра-Боба (с точки зрения Алисы)
    /// </summary>
    public Guid BobId { get; } = Guid.NewGuid();

    private EncryptorPair(TestLoggerProvider logger, TestIdentityDataPathProvider alicePaths, TestIdentityDataPathProvider bobPaths)
    {
        this.alicePaths = alicePaths;
        this.bobPaths = bobPaths;
        var dataProtection = new EphemeralDataProtectionProvider();
        Alice = new MessageEncryptor(logger, dataProtection, alicePaths);
        Bob = new MessageEncryptor(logger, dataProtection, bobPaths);
    }

    /// <summary>
    /// Создать пару с выполненным обменом публичными ключами
    /// </summary>
    public static async Task<EncryptorPair> CreateAsync()
    {
        var pair = new EncryptorPair(new TestLoggerProvider(), new TestIdentityDataPathProvider(), new TestIdentityDataPathProvider());
        var alicePublic = await pair.Alice.GetLocalPublicKey();
        var bobPublic = await pair.Bob.GetLocalPublicKey();
        await pair.Alice.InitializeSessionWithPartnerAsync(pair.BobId, bobPublic);
        await pair.Bob.InitializeSessionWithPartnerAsync(pair.AliceId, alicePublic);
        return pair;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        ((IDisposable)Alice).Dispose();
        ((IDisposable)Bob).Dispose();
        alicePaths.Dispose();
        bobPaths.Dispose();
    }
}
