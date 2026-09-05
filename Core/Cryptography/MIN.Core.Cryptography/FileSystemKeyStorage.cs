using System.Text.Json;
using System.Text.Json.Serialization;
using MIN.Core.Cryptography.Contracts.Models;
using MIN.Helpers.Contracts.Helpers;
using MIN.Helpers.Contracts.Interfaces;

namespace MIN.Core.Cryptography;

/// <summary>
/// Хранилище ключей на основе файловой системы
/// </summary>
public sealed class FileSystemKeyStorage : IDisposable
{
    private readonly string keysPath;
    private readonly string partnersPath;
    private readonly JsonSerializerOptions jsonOptions;
    private readonly SchemaFileStore schemaStore;
    private readonly SemaphoreSlim localKeyLock = new(1, 1);
    private readonly SemaphoreSlim partnersLock = new(1, 1);
    private bool disposed;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="FileSystemKeyStorage"/>
    /// </summary>
    public FileSystemKeyStorage(IAppDataProvider appDataProvider, ILoggerProvider logger)
    {
        var directory = Directory.CreateDirectory(Path.Combine(appDataProvider.SharedDirectory, "cryptography")).FullName;
        keysPath = Path.Combine(directory, "keys.json");
        partnersPath = Path.Combine(directory, "partners.json");

        jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        schemaStore = new(1, jsonOptions, logger);
    }

    /// <summary>
    /// Загрузить локальную пару ключей из хранилища
    /// </summary>
    public async Task<KeyPair?> LoadLocalKeyPairAsync(CancellationToken cancellationToken = default)
    {
        await localKeyLock.WaitAsync(cancellationToken);
        try
        {
            return await schemaStore.LoadOrCreateAsync<KeyPair?>(keysPath, () => null); // regenerate if error
        }
        finally
        {
            localKeyLock.Release();
        }
    }

    /// <summary>
    /// Сохранить локальную пару ключей в хранилище
    /// </summary>
    public async Task SaveLocalKeyPairAsync(KeyPair keyPair, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(keyPair);

        await localKeyLock.WaitAsync(cancellationToken);
        try
        {
            await schemaStore.SaveAsync(keysPath, keyPair);
        }
        finally
        {
            localKeyLock.Release();
        }
    }

    /// <summary>
    /// Загрузить словарь публичных ключей партнёров
    /// </summary>
    public async Task<Dictionary<Guid, byte[]>> LoadPartnerPublicKeysAsync(CancellationToken cancellationToken = default)
    {
        await partnersLock.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(partnersPath))
            {
                return [];
            }

            var json = await File.ReadAllTextAsync(partnersPath, cancellationToken);
            var stringDict = await schemaStore.LoadOrCreateAsync(partnersPath, () => new Dictionary<string, string>());
            if (stringDict == null)
            {
                return [];
            }

            var result = new Dictionary<Guid, byte[]>();
            foreach (var kvp in stringDict)
            {
                if (Guid.TryParse(kvp.Key, out var guid))
                {
                    result[guid] = Convert.FromBase64String(kvp.Value);
                }
            }
            return result;
        }
        catch (Exception ex)
        {
            throw new InvalidDataException("Failed to load partners file", ex);
        }
        finally
        {
            partnersLock.Release();
        }
    }

    /// <summary>
    /// Сохранить публичный ключ партнёра в хранилище
    /// </summary>
    /// <param name="partnerId">Идентификатор партнёра (участника)</param>
    /// <param name="publicKey">Публичный ключ партнёра</param>
    /// <param name="cancellationToken">Токен отмены</param>
    public async Task SavePartnerPublicKeyAsync(Guid partnerId, byte[] publicKey, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(publicKey);

        var partners = await LoadPartnerPublicKeysAsync(cancellationToken);
        partners[partnerId] = publicKey;

        var stringDict = partners.ToDictionary(
            kvp => kvp.Key.ToString(),
            kvp => Convert.ToBase64String(kvp.Value));

        await schemaStore.SaveAsync(partnersPath, stringDict);
    }

    /// <summary>
    /// Загрузить публичный ключ партнёра по его идентификатору
    /// </summary>
    /// <param name="partnerId">Идентификатор партнёра (участника)</param>
    /// <param name="cancellationToken">Токен отмены</param>
    public async Task<byte[]?> LoadPartnerPublicKeyAsync(Guid partnerId, CancellationToken cancellationToken = default)
    {
        var partners = await LoadPartnerPublicKeysAsync(cancellationToken);
        return partners.TryGetValue(partnerId, out var key) ? key : null;
    }

    /// <inheritdoc cref="IDisposable.Dispose"/>
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        localKeyLock.Dispose();
        partnersLock.Dispose();
        disposed = true;
    }
}
