using MIN.Core.Identity.Contracts.Interfaces;
using MIN.Helpers.Contracts.Interfaces;

namespace MIN.Core.Identity;

/// <inheritdoc cref="IIdentityDataPathProvider"/>
public sealed class IdentityDataPathProvider : IIdentityDataPathProvider
{
    private readonly string identityPath;

    /// <inheritdoc />
    public string RoomsDirectory { get; }

    /// <inheritdoc />
    public string CryptographyDirectory { get; }

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="IdentityDataPathProvider"/>
    /// </summary>
    public IdentityDataPathProvider(IAppDataProvider appDataProvider, IIdentityService identityService)
    {
        identityPath = Path.Combine(appDataProvider.SharedDirectory, "identities", $"{identityService.SelfParticipant.Id}");
        RoomsDirectory = Path.Combine(identityPath, "rooms");
        CryptographyDirectory = Path.Combine(identityPath, "cryptography");
    }
}
