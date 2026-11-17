using System.Text.Json;
using FluentAssertions;
using MIN.Core.Events.Services;
using MIN.Sessions.Core.Events;
using MIN.Sessions.Core.Services.Contracts.Interfaces;
using MIN.Sessions.Core.Services.Contracts.Models;
using MIN.Sessions.Core.Services.Services;
using MIN.Tests.Common;
using Xunit;

namespace MIN.Sessions.Core.Tests;

/// <summary>
/// Тесты на <see cref="SessionScanner"/>
/// </summary>
public class SessionScannerTests : IDisposable
{
    private readonly TestLoggerProvider logger = new();
    private readonly InMemoryEventBus eventBus;
    private readonly ISessionScanner scanner;
    private readonly List<string> createdDirs = [];

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="SessionScannerTests"/>
    /// </summary>
    public SessionScannerTests()
    {
        eventBus = new InMemoryEventBus(logger);
        scanner = new SessionScanner(eventBus, logger);
    }

    private string CreateSessionDir(string sessionId, string? json = null, string? thumbnail = null)
    {
        var dir = Path.Combine(scanner.SessionsDirectory, sessionId);
        Directory.CreateDirectory(dir);
        createdDirs.Add(dir);
        json ??= JsonSerializer.Serialize(new { sessionId, name = "Сессия", version = "1.0.0" });
        File.WriteAllText(Path.Combine(dir, "presenter.json"), json);
        if (thumbnail != null)
        {
            File.WriteAllBytes(Path.Combine(dir, thumbnail), [1, 2, 3]);
        }

        return dir;
    }

    /// <summary>
    /// Сканирование находит сессию с валидным presenter.json
    /// </summary>
    [Fact]
    public async Task ScanShouldFindValidSession()
    {
        // Arrange
        using var capture = new EventCapture<SessionRescanCompletedEvent>(eventBus);
        var sessionId = "test-session-" + Guid.NewGuid().ToString("N");
        CreateSessionDir(sessionId);

        // Act
        await scanner.ScanAsync(CancellationToken.None);
        await capture.WaitAsync();

        // Assert
        scanner.IsSessionInstalled(sessionId).Should().BeTrue();
        var session = scanner.GetSessionById(sessionId);
        session.Should().NotBeNull();
        session!.Name.Should().Be("Сессия");
        session.SessionDirectory.Should().Be(Path.Combine(scanner.SessionsDirectory, sessionId));
    }

    /// <summary>
    /// Битый presenter.json пропускается без падения
    /// </summary>
    [Fact]
    public async Task ScanShouldSkipBrokenJson()
    {
        // Arrange
        var sessionId = "broken-" + Guid.NewGuid().ToString("N");
        CreateSessionDir(sessionId, json: "{битый json");

        // Act
        await scanner.ScanAsync(CancellationToken.None);

        // Assert
        scanner.IsSessionInstalled(sessionId).Should().BeFalse();
    }

    /// <summary>
    /// presenter.json без sessionId пропускается
    /// </summary>
    [Fact]
    public async Task ScanShouldSkipMissingSessionId()
    {
        // Arrange
        var dirName = "no-id-" + Guid.NewGuid().ToString("N");
        CreateSessionDir(dirName, json: JsonSerializer.Serialize(new { name = "Без id", version = "1.0.0" }));

        // Act
        await scanner.ScanAsync(CancellationToken.None);

        // Assert
        scanner.DownloadedSessions.Should().NotContainKey(dirName);
    }

    /// <summary>
    /// Отсутствующий файл обложки обнуляется
    /// </summary>
    [Fact]
    public async Task ScanShouldNullMissingThumbnail()
    {
        // Arrange
        var sessionId = "thumb-" + Guid.NewGuid().ToString("N");
        CreateSessionDir(sessionId, json: JsonSerializer.Serialize(new { sessionId, name = "Сессия", version = "1.0.0", thumbnailFileName = "missing.png" }));

        // Act
        await scanner.ScanAsync(CancellationToken.None);

        // Assert
        scanner.GetSessionById(sessionId)!.ThumbnailFileName.Should().BeNull();
    }

    /// <summary>
    /// Существующая обложка читается
    /// </summary>
    [Fact]
    public async Task LoadThumbnailShouldReadBytes()
    {
        // Arrange
        var sessionId = "thumb-ok-" + Guid.NewGuid().ToString("N");
        CreateSessionDir(sessionId, json: JsonSerializer.Serialize(new { sessionId, name = "Сессия", version = "1.0.0", thumbnailFileName = "thumb.png" }), thumbnail: "thumb.png");
        await scanner.ScanAsync(CancellationToken.None);

        // Act
        var bytes = scanner.LoadThumbnail(sessionId);

        // Assert
        bytes.Should().Equal([1, 2, 3]);
    }

    /// <summary>
    /// AdHoc-сессии регистрируются и убираются по комнате
    /// </summary>
    [Fact]
    public void AdHocSessionsShouldRegisterAndUnregister()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var session = new Session { SessionId = "adhoc-1", Name = "AdHoc", Version = new Version(1, 0) };

        // Act
        scanner.RegisterAdHoc(roomId, session);
        var installed = scanner.IsSessionInstalled("adhoc-1");
        scanner.UnregisterAdHocForRoom(roomId);

        // Assert
        installed.Should().BeTrue();
        scanner.GetSessionById("adhoc-1").Should().BeNull();
    }

    /// <summary>
    /// Удаление сессии убирает директорию
    /// </summary>
    [Fact]
    public async Task DeleteSessionShouldRemoveDirectory()
    {
        // Arrange
        var sessionId = "delete-" + Guid.NewGuid().ToString("N");
        var dir = CreateSessionDir(sessionId);
        await scanner.ScanAsync(CancellationToken.None);

        // Act
        var deleted = await scanner.DeleteSessionAsync(sessionId, CancellationToken.None);

        // Assert
        deleted.Should().BeTrue();
        Directory.Exists(dir).Should().BeFalse();
        scanner.IsSessionInstalled(sessionId).Should().BeFalse();
    }

    /// <summary>
    /// Удаление неизвестной сессии возвращает false
    /// </summary>
    [Fact]
    public async Task DeleteUnknownSessionShouldReturnFalse()
    {
        // Act
        var deleted = await scanner.DeleteSessionAsync("unknown-" + Guid.NewGuid(), CancellationToken.None);

        // Assert
        deleted.Should().BeFalse();
    }

    /// <summary>
    /// Path traversal в идентификаторе сессии отклоняется
    /// </summary>
    [Fact]
    public async Task DeleteSessionPathTraversalShouldBeRejected()
    {
        // Arrange
        var sessionId = "traversal-" + Guid.NewGuid().ToString("N");
        CreateSessionDir(sessionId);
        await scanner.ScanAsync(CancellationToken.None);
        scanner.RegisterAdHoc(Guid.NewGuid(), new Session { SessionId = "..\evil", Name = "Evil", Version = new Version(1, 0) });

        // Act
        var deleted = await scanner.DeleteSessionAsync("..\evil", CancellationToken.None);

        // Assert
        deleted.Should().BeFalse();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var dir in createdDirs)
        {
            try
            {
                if (Directory.Exists(dir))
                {
                    Directory.Delete(dir, recursive: true);
                }
            }
            catch (IOException)
            {
            }
        }
    }
}
