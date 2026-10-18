using System.Text.Json;
using MIN.Core.Events.Contracts.Interfaces;
using MIN.Helpers.Contracts.Interfaces;
using MIN.Sessions.Core.Events;
using MIN.Sessions.Core.Services.Contracts.Interfaces;
using MIN.Sessions.Core.Services.Contracts.Models;

namespace MIN.Sessions.Core.Services.Services;

/// <inheritdoc cref="ISessionScanner"/>
public class SessionScanner : ISessionScanner
{
    private const string DownloadedSessionsFolderName = "Скаченные сессии";

    private readonly string sessionsDirectory;
    private readonly IEventBus eventBus;
    private readonly ILoggerProvider logger;

    string ISessionScanner.SessionsDirectory => sessionsDirectory;

    private readonly static JsonSerializerOptions jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private Dictionary<string, Session> downloadedSessions = [];
    private readonly Dictionary<string, (Session Session, Guid RoomId)> adHocSessions = [];

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="SessionScanner"/>
    /// </summary>
    public SessionScanner(IEventBus eventBus, ILoggerProvider logger)
    {
        this.eventBus = eventBus;
        this.logger = logger;
        sessionsDirectory = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, DownloadedSessionsFolderName);
    }

    IReadOnlyDictionary<string, Session> ISessionScanner.DownloadedSessions => downloadedSessions;


    Session? ISessionScanner.GetSessionById(string sessionId)
    {
        if (downloadedSessions.TryGetValue(sessionId, out var session))
        {
            return session;
        }

        return adHocSessions.TryGetValue(sessionId, out var adHoc) ? adHoc.Session : null;
    }

    bool ISessionScanner.IsSessionInstalled(string sessionId)
        => downloadedSessions.ContainsKey(sessionId) || adHocSessions.ContainsKey(sessionId);

    async Task ISessionScanner.ScanAsync(CancellationToken cancellationToken)
    {
        var scanned = new Dictionary<string, Session>();

        if (!Directory.Exists(sessionsDirectory))
        {
            Directory.CreateDirectory(sessionsDirectory);
            downloadedSessions = scanned;
            return;
        }

        foreach (var dir in Directory.EnumerateDirectories(sessionsDirectory))
        {
            var jsonPath = Path.Combine(dir, "presenter.json");
            if (!File.Exists(jsonPath))
            {
                continue;
            }

            try
            {
                await using var stream = File.OpenRead(jsonPath);
                var session = await JsonSerializer.DeserializeAsync<Session>(stream, jsonOptions, cancellationToken: cancellationToken);

                if (session?.SessionId == null || session.Name == null)
                {
                    logger.Log($"Skipping {jsonPath}: missing sessionId or name");
                    continue;
                }

                if (session.ThumbnailFileName != null && !Path.Exists(Path.Combine(dir, session.ThumbnailFileName)))
                {
                    session.ThumbnailFileName = null;
                }

                session.SessionDirectory = dir;
                scanned[session.SessionId] = session;
            }
            catch (Exception ex)
            {
                logger.Log($"Failed to parse {jsonPath}: {ex.Message}");
            }
        }

        downloadedSessions = scanned;
        await eventBus.PublishAsync(new SessionRescanCompletedEvent()
        {
            DownloadedSessions = downloadedSessions
        }, cancellationToken);
    }

    byte[]? ISessionScanner.LoadThumbnail(string sessionId)
    {
        if (!downloadedSessions.TryGetValue(sessionId, out var session)
            || session.ThumbnailFileName == null)
        {
            return null;
        }

        var path = Path.Combine(
            sessionsDirectory, session.SessionId, session.GetThumbnailPath());
        return File.Exists(path) ? File.ReadAllBytes(path) : null;
    }

    void ISessionScanner.RegisterAdHoc(Guid roomId, Session session)
        => adHocSessions[session.SessionId] = (session, roomId);

    void ISessionScanner.UnregisterAdHocForRoom(Guid roomId)
    {
        foreach (var sessionId in adHocSessions
                     .Where(x => x.Value.RoomId == roomId)
                     .Select(x => x.Key)
                     .ToList())
        {
            adHocSessions.Remove(sessionId);
        }
    }

    async Task<bool> ISessionScanner.DeleteSessionAsync(string sessionId, CancellationToken cancellationToken)
    {
        if (!downloadedSessions.ContainsKey(sessionId))
        {
            return false;
        }

        var sessionDirectory = Path.Combine(sessionsDirectory, sessionId);

        if (!Path.GetFullPath(sessionDirectory).StartsWith(
                Path.GetFullPath(sessionsDirectory) + Path.DirectorySeparatorChar))
        {
            return false;
        }

        if (Directory.Exists(sessionDirectory))
        {
            var deletingPath = sessionDirectory + ".deleting";
            Directory.Move(sessionDirectory, deletingPath);

            try
            {
                Directory.Delete(deletingPath, recursive: true);
            }
            catch (Exception ex)
            {
                logger.Log($"Не удалось полностью очистить {deletingPath}: {ex.Message}");
            }
        }

        downloadedSessions.Remove(sessionId);
        adHocSessions.Remove(sessionId);

        await eventBus.PublishAsync(new SessionRescanCompletedEvent()
        {
            DownloadedSessions = downloadedSessions,
        }, cancellationToken);

        return true;
    }
}
