namespace MIN.Helpers.Contracts.Interfaces;

/// <summary>
/// Сервис по работе с хранением в файлах
/// </summary>
public interface IAppDataProvider
{
    /// <summary>
    /// Папка на текущую версию приложения
    /// </summary>
    string VersionedDirectory { get; }

    /// <summary>
    /// Общая папка
    /// </summary>
    string SharedDirectory { get; }

    /// <summary>
    /// Очистить содежимое в папке
    /// </summary>
    void ClearFolder(string folderName);
}
