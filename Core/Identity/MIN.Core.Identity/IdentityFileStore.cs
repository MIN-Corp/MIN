using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MIN.Core.Entities.Contracts.Interfaces;
using MIN.Core.Entities.Contracts.Models;
using MIN.Core.Identity.Contracts.Constants;
using MIN.Core.Identity.Contracts.Interfaces;
using MIN.Core.Identity.Contracts.Models;
using MIN.Helpers.Contracts.Helpers;
using MIN.Helpers.Contracts.Interfaces;
using MIN.Helpers.Contracts.Models.Enums;

namespace MIN.Core.Identity;

/// <inheritdoc cref="IIdentityService"/>
public sealed class IdentityFileStore : IDisposable
{
    private readonly string identityPath;
    private readonly int appSlot;
    private readonly ILoggerProvider logger;
    private readonly JsonSerializerOptions jsonOptions;
    private readonly IdentityAppInstanceResolver appInstanceResolver;
    private readonly SchemaFileStore schemaFileStore;
    private readonly SemaphoreSlim localKeyLock = new(1, 1);
    private readonly Mutex fileMutex = new(false, "Global\\MIN-Identity-File");

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="IdentityService"/>
    /// </summary>
    public IdentityFileStore(IAppDataProvider appDataProvider, ILoggerProvider logger)
    {
        identityPath = Path.Combine(appDataProvider.SharedDirectory, "identity", "uuid.json");

        jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        schemaFileStore = new(1, jsonOptions, logger);

        this.logger = logger;
        appInstanceResolver = new IdentityAppInstanceResolver();
        appSlot = appInstanceResolver.Slot;
    }

    /// <summary>
    /// Загрузить участника
    /// </summary>
    public ParticipantInfo? LoadParticipant()
    {
        localKeyLock.Wait();
        fileMutex.WaitOne();

        try
        {
            if (!File.Exists(identityPath))
            {
                return null;
            }

            var entries = schemaFileStore.LoadOrCreate<List<IdentityEntry>>(identityPath, () => []);

            var foundEntry = entries.FirstOrDefault(x => x.Slot == appSlot);

            if (foundEntry == null)
            {
                return null;
            }

            if (!VerifyHash(foundEntry.Id, foundEntry.Hash))
            {
                logger.Log($"Хеш участника в слоте {appSlot} не совпал — возможно ручное изменение. id пересоздан.", LogLevel.Warning);
                return null;
            }

            return new ParticipantInfo
            {
                Id = foundEntry.Id,
                Name = foundEntry.Name
            };
        }
        finally
        {
            localKeyLock.Release();
            fileMutex.ReleaseMutex();
        }
    }

    /// <summary>
    /// Сохранить данные участника
    /// </summary>
    public void SaveParticipant(IParticipantData participant)
    {
        localKeyLock.Wait();
        fileMutex.WaitOne();
        try
        {
            var entries = schemaFileStore.LoadOrCreate<List<IdentityEntry>>(identityPath, () => []);

            var entry = entries.FirstOrDefault(x => x.Slot == appSlot);

            var wasNull = false;

            if (entry == null)
            {
                wasNull = true;
                entry = new IdentityEntry()
                {
                    Slot = appSlot,
                };
            }

            entry.Id = participant.Id;
            entry.Name = participant.Name;
            entry.Hash = ComputeHash(entry.Id);

            if (wasNull)
            {
                entries.Add(entry);
            }

            schemaFileStore.Save(identityPath, entries);
        }
        finally
        {
            localKeyLock.Release();
            fileMutex.ReleaseMutex();
        }
    }

    /// <summary>
    /// Сохранить данные участника асинхронно
    /// </summary>
    public async Task SaveParticipantAsync(IParticipantData participant)
    {
        await localKeyLock.WaitAsync();
        fileMutex.WaitOne();
        try
        {
            var entries = await schemaFileStore.LoadOrCreateAsync<List<IdentityEntry>>(identityPath, () => []);

            var entry = entries.FirstOrDefault(x => x.Slot == appSlot);

            var wasNull = false;

            if (entry == null)
            {
                wasNull = true;
                entry = new IdentityEntry()
                {
                    Slot = appSlot,
                };
            }

            entry.Id = participant.Id;
            entry.Name = participant.Name;
            entry.Hash = ComputeHash(entry.Id);

            if (wasNull)
            {
                entries.Add(entry);
            }

            await schemaFileStore.SaveAsync(identityPath, entries);
        }
        finally
        {
            localKeyLock.Release();
            fileMutex.ReleaseMutex();
        }
    }

    private static string ComputeHash(Guid id)
        => Convert.ToBase64String(HMACSHA256.HashData(IdentityConstants.HashSecret, id.ToByteArray()));

    private static bool VerifyHash(Guid id, string stored)
    {
        var expected = ComputeHash(id);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(stored), Encoding.UTF8.GetBytes(expected));
    }


    /// <inheritdoc cref="IDisposable.Dispose"/>
    public void Dispose()
    {
        fileMutex.Dispose();
        appInstanceResolver.Dispose();
    }
}
