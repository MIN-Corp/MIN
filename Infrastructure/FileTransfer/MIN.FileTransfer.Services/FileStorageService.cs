using MIN.Core.Identity.Contracts.Interfaces;
using MIN.FileTransfer.Services.Contracts.Interfaces;
using MIN.Helpers.Contracts.Interfaces;

namespace MIN.FileTransfer.Services;

/// <inheritdoc cref="IFileStorageService"/>
public sealed class FileStorageService : IFileStorageService
{
    private const string RoomFilesFolderName = "Файлы комнат";

    private readonly ILoggerProvider logger;
    private readonly string baseDirectory;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="FileStorageService"/>
    /// </summary>
    public FileStorageService(IIdentityDataPathProvider identityDataPath, ILoggerProvider logger)
    {
        this.logger = logger;

        baseDirectory = Path.Combine(identityDataPath.FilesDirectory, RoomFilesFolderName);

        if (!Directory.Exists(baseDirectory))
        {
            Directory.CreateDirectory(baseDirectory);
            logger.Log($"Создана базовая папка для файлов: {baseDirectory}");
        }
    }

    /// <inheritdoc />
    public string GetRoomDirectory(Guid roomId)
    {
        var roomDir = Path.Combine(baseDirectory, roomId.ToString());
        return roomDir;
    }

    /// <inheritdoc />
    public void EnsureRoomDirectoryExists(Guid roomId)
    {
        var roomDir = GetRoomDirectory(roomId);
        if (!Directory.Exists(roomDir))
        {
            Directory.CreateDirectory(roomDir);
            logger.Log($"Создана папка для комнаты {roomId}: {roomDir}");
        }
    }

    bool IFileStorageService.FileExists(Guid roomId, string fileName)
    {
        var filePath = Path.Combine(GetRoomDirectory(roomId), fileName);
        return File.Exists(filePath);
    }

    /// <inheritdoc />
    public string? GetFilePath(Guid roomId, string fileName)
    {
        var filePath = Path.Combine(GetRoomDirectory(roomId), fileName);
        return File.Exists(filePath) ? filePath : null;
    }

    async Task<string> IFileStorageService.SaveFileAsync(Guid roomId, Stream source, string fileName, CancellationToken cancellationToken)
    {
        EnsureRoomDirectoryExists(roomId);
        var roomDir = GetRoomDirectory(roomId);

        var finalFileName = ResolveUniqueFileName(roomDir, fileName);
        var filePath = Path.Combine(roomDir, finalFileName);

        logger.Log($"Сохраняю файл: {fileName} → {finalFileName} ({source.Length} байт)");

        await using var fileStream = File.Create(filePath);
        await source.CopyToAsync(fileStream, cancellationToken);

        logger.Log($"Файл сохранён: {filePath}");
        return finalFileName;
    }

    Task<Stream?> IFileStorageService.OpenFileForReadingAsync(Guid roomId, string fileName, CancellationToken cancellationToken)
    {
        var filePath = GetFilePath(roomId, fileName);
        if (filePath == null)
        {
            logger.Log($"Файл не найден для чтения: {fileName} (комната {roomId})");
            return Task.FromResult<Stream?>(null);
        }

        logger.Log($"Открываю файл для чтения: {filePath}");
        Stream stream = File.OpenRead(filePath);
        return Task.FromResult<Stream?>(stream);
    }

    Task IFileStorageService.DeleteFileAsync(Guid roomId, string fileName, CancellationToken cancellationToken)
    {
        var filePath = GetFilePath(roomId, fileName);
        if (filePath != null)
        {
            logger.Log($"Удаляю файл: {filePath}");
            File.Delete(filePath);
        }
        return Task.CompletedTask;
    }

    Task IFileStorageService.DeleteRoomFilesAsync(Guid roomId, CancellationToken cancellationToken)
    {
        var roomDir = GetRoomDirectory(roomId);
        if (Directory.Exists(roomDir))
        {
            logger.Log($"Удаляю все файлы комнаты {roomId}: {roomDir}");
            Directory.Delete(roomDir, recursive: true);
        }
        return Task.CompletedTask;
    }

    async Task<string> IFileStorageService.MoveTempFileToRoomAsync(Guid roomId, string tempFilePath, string fileName, CancellationToken cancellationToken)
    {
        EnsureRoomDirectoryExists(roomId);
        var roomDir = GetRoomDirectory(roomId);

        var finalFileName = ResolveUniqueFileName(roomDir, fileName);
        var finalPath = Path.Combine(roomDir, finalFileName);

        logger.Log($"Перемещаю временный файл: {tempFilePath} → {finalPath} ({new FileInfo(tempFilePath).Length} байт)");

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            await using (var source = File.OpenRead(tempFilePath))
            await using (var destination = File.Create(finalPath))
            {
                await source.CopyToAsync(destination, cancellationToken);
            }

            File.Delete(tempFilePath);
        }
        catch
        {
            TryDeleteFile(finalPath);
            throw;
        }

        logger.Log($"Временный файл удалён: {tempFilePath}");
        return finalFileName;
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static string ResolveUniqueFileName(string directory, string fileName)
    {
        var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        var candidate = fileName;
        var counter = 1;

        while (File.Exists(Path.Combine(directory, candidate)))
        {
            candidate = $"{nameWithoutExt}_{counter}{extension}";
            counter++;
        }

        return candidate;
    }
}
