namespace MIN.Core.Identity.Contracts.Interfaces;

/// <summary>
/// Персистентое хранилище, scoped per identity
/// </summary>
public interface IIdentityDataPathProvider
{
    /// <summary>
    /// Папка cо всеми сохранёнными комнатами
    /// </summary>
    string RoomsDirectory { get; }

    /// <summary>
    /// Общая папка для ключей
    /// </summary>
    string CryptographyDirectory { get; }

    /// <summary>
    /// Общая папка для хранения файлов
    /// </summary>
    string FilesDirectory { get; }
}
