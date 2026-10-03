namespace MIN.Core.Identity.Contracts.Interfaces;

/// <summary>
/// Персистентое хранилище, scoped per identity
/// </summary>
public interface IIdentityDataPathProvider
{
    /// <summary>
    /// Папка на текущую версию приложения
    /// </summary>
    string RoomsDirectory { get; }

    /// <summary>
    /// Общая папка
    /// </summary>
    string CryptographyDirectory { get; }
}
