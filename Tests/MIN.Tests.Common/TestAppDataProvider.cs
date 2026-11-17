using MIN.Helpers.Contracts.Interfaces;

namespace MIN.Tests.Common;

/// <summary>
/// Провайдер директорий приложения во временной директории
/// </summary>
public sealed class TestAppDataProvider : IAppDataProvider, IDisposable
{
    private readonly TemporaryDirectory directory = new();

    /// <inheritdoc />
    public string VersionedDirectory => directory.Path;

    /// <inheritdoc />
    public string SharedDirectory => directory.Path;

    /// <inheritdoc />
    public void ClearFolder(string folderName)
    {
        var path = Path.Combine(VersionedDirectory, folderName);
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    /// <inheritdoc />
    public void Dispose() => directory.Dispose();
}
