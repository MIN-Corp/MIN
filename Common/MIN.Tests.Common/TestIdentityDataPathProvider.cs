using MIN.Core.Identity.Contracts.Interfaces;

namespace MIN.Tests.Common;

/// <summary>
/// Провайдер путей identity во временной директории
/// </summary>
public sealed class TestIdentityDataPathProvider : IIdentityDataPathProvider, IDisposable
{
    private readonly TemporaryDirectory directory;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="TestIdentityDataPathProvider"/>
    /// </summary>
    public TestIdentityDataPathProvider()
    {
        directory = new TemporaryDirectory();
        CryptographyDirectory = Path.Combine(directory.Path, "crypto");
        RoomsDirectory = Path.Combine(directory.Path, "rooms");
        FilesDirectory = Path.Combine(directory.Path, "files");
    }

    /// <inheritdoc />
    public string CryptographyDirectory { get; }

    /// <inheritdoc />
    public string RoomsDirectory { get; }

    /// <inheritdoc />
    public string FilesDirectory { get; }

    /// <inheritdoc />
    public void Dispose() => directory.Dispose();
}
