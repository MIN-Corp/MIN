using FluentAssertions;
using MIN.FileTransfer.Services;
using MIN.FileTransfer.Services.Contracts.Interfaces;
using MIN.Tests.Common;
using Xunit;

namespace MIN.FileTransfer.Tests;

/// <summary>
/// Тесты на <see cref="FileStorageService"/>
/// </summary>
public class FileStorageServiceTests : IDisposable
{
    private readonly TestIdentityDataPathProvider paths = new();
    private readonly IFileStorageService storage;
    private readonly Guid roomId = Guid.NewGuid();

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="FileStorageServiceTests"/>
    /// </summary>
    public FileStorageServiceTests()
    {
        storage = new FileStorageService(paths, new TestLoggerProvider());
    }

    /// <summary>
    /// Сохранение и чтение файла возвращают те же байты
    /// </summary>
    [Fact]
    public async Task SaveAndOpenShouldRoundTrip()
    {
        // Arrange
        var data = new byte[] { 1, 2, 3, 4, 5 };
        using var source = new MemoryStream(data);

        // Act
        var fileName = await storage.SaveFileAsync(roomId, source, "file.bin", CancellationToken.None);
        var exists = storage.FileExists(roomId, fileName);
        await using var readStream = await storage.OpenFileForReadingAsync(roomId, fileName, CancellationToken.None);
        using var buffer = new MemoryStream();
        await readStream!.CopyToAsync(buffer);

        // Assert
        fileName.Should().Be("file.bin");
        exists.Should().BeTrue();
        buffer.ToArray().Should().Equal(data);
    }

    /// <summary>
    /// Повторное имя файла получает уникальное имя
    /// </summary>
    [Fact]
    public async Task SaveDuplicateNameShouldResolveUnique()
    {
        // Arrange
        using var first = new MemoryStream([1]);
        using var second = new MemoryStream([2]);

        // Act
        var firstName = await storage.SaveFileAsync(roomId, first, "file.bin", CancellationToken.None);
        var secondName = await storage.SaveFileAsync(roomId, second, "file.bin", CancellationToken.None);

        // Assert
        secondName.Should().NotBe(firstName);
        storage.FileExists(roomId, firstName).Should().BeTrue();
        storage.FileExists(roomId, secondName).Should().BeTrue();
    }

    /// <summary>
    /// Отсутствующий файл не находится
    /// </summary>
    [Fact]
    public async Task MissingFileShouldNotBeFound()
    {
        // Act
        var exists = storage.FileExists(roomId, "missing.bin");
        var path = ((FileStorageService)storage).GetFilePath(roomId, "missing.bin");
        var stream = await storage.OpenFileForReadingAsync(roomId, "missing.bin", CancellationToken.None);

        // Assert
        exists.Should().BeFalse();
        path.Should().BeNull();
        stream.Should().BeNull();
    }

    /// <summary>
    /// Удаление файла убирает его, повторное удаление безопасно
    /// </summary>
    [Fact]
    public async Task DeleteFileShouldWork()
    {
        // Arrange
        using var source = new MemoryStream([1]);
        var fileName = await storage.SaveFileAsync(roomId, source, "file.bin", CancellationToken.None);

        // Act
        await storage.DeleteFileAsync(roomId, fileName, CancellationToken.None);
        var act = () => storage.DeleteFileAsync(roomId, fileName, CancellationToken.None);

        // Assert
        storage.FileExists(roomId, fileName).Should().BeFalse();
        await act.Should().NotThrowAsync();
    }

    /// <summary>
    /// Удаление файлов комнаты убирает директорию
    /// </summary>
    [Fact]
    public async Task DeleteRoomFilesShouldRemoveDirectory()
    {
        // Arrange
        using var source = new MemoryStream([1]);
        await storage.SaveFileAsync(roomId, source, "file.bin", CancellationToken.None);
        var roomDir = ((FileStorageService)storage).GetRoomDirectory(roomId);

        // Act
        await storage.DeleteRoomFilesAsync(roomId, CancellationToken.None);

        // Assert
        Directory.Exists(roomDir).Should().BeFalse();
    }

    /// <summary>
    /// Временный файл переносится в директорию комнаты
    /// </summary>
    [Fact]
    public async Task MoveTempFileShouldPlaceInRoom()
    {
        // Arrange
        using var tempDir = new TemporaryDirectory();
        var tempPath = Path.Combine(tempDir.Path, "temp.bin");
        await File.WriteAllBytesAsync(tempPath, "\t\t\t"u8.ToArray());

        // Act
        var fileName = await storage.MoveTempFileToRoomAsync(roomId, tempPath, "moved.bin", CancellationToken.None);

        // Assert
        storage.FileExists(roomId, fileName).Should().BeTrue();
        File.Exists(tempPath).Should().BeFalse();
    }

    /// <inheritdoc />
    public void Dispose() => paths.Dispose();
}
