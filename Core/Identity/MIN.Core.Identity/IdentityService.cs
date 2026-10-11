using MIN.Core.Entities.Contracts.Interfaces;
using MIN.Core.Entities.Contracts.Models;
using MIN.Core.Identity.Contracts.Interfaces;
using MIN.Helpers.Contracts.Interfaces;

namespace MIN.Core.Identity;

/// <inheritdoc cref="IIdentityService"/>
public sealed class IdentityService : IIdentityService, IDisposable
{
    private readonly IdentityFileStore identityFileStore;
    private readonly SemaphoreSlim cacheLock = new(1, 1);

    private ParticipantInfo? currentParticipant;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="IdentityService"/>
    /// </summary>
    public IdentityService(IAppDataProvider appDataProvider, ILoggerProvider logger)
    {
        identityFileStore = new(appDataProvider, logger);
    }

    IParticipantData IIdentityService.SelfParticipant => ResolveParticipant();

    /// <inheritdoc />
    async Task IIdentityService.SaveParticipant(IParticipantData participantData)
    {
        currentParticipant?.Name = participantData.Name;
        await identityFileStore.SaveParticipantAsync(participantData);
    }

    private IParticipantData ResolveParticipant()
    {
        if (currentParticipant != null)
        {
            return currentParticipant;
        }

        cacheLock.Wait();
        try
        {
            if (currentParticipant != null)
            {
                return currentParticipant;
            }

            currentParticipant = identityFileStore.LoadParticipant();

            if (currentParticipant == null)
            {
                currentParticipant = new ParticipantInfo
                {
                    Id = Guid.NewGuid()
                };

                identityFileStore.SaveParticipant(currentParticipant);
            }

            return currentParticipant;
        }
        finally
        {
            cacheLock.Release();
        }
    }

    void IDisposable.Dispose() => identityFileStore.Dispose();
}
