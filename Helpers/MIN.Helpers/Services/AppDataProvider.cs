using MIN.Helpers.Contracts.Interfaces;

namespace MIN.Helpers.Services;

/// <inheritdoc cref="IAppDataProvider"/>
public sealed class AppDataProvider : IAppDataProvider
{
    /// <inheritdoc />
    public string VersionedDirectory { get; }

    /// <inheritdoc />
    public string SharedDirectory { get; }

    void IAppDataProvider.ClearFolder(string folderName)
    {
        foreach (var file in Directory.EnumerateFiles(Path.Combine(VersionedDirectory, folderName)))
        {
            File.Delete(file);
        }
    }

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="AppDataProvider"/>
    /// </summary>
    public AppDataProvider(IVersionProvider versionProvider)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        VersionedDirectory = Path.Combine(appData, "MIN", $"v.{versionProvider.Version}");
        SharedDirectory = Path.Combine(appData, "MIN", "Shared");
    }
}
