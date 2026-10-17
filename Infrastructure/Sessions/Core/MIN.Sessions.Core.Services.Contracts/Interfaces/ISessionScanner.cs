using MIN.Sessions.Core.Services.Contracts.Models;

namespace MIN.Sessions.Core.Services.Contracts.Interfaces;

/// <summary>
/// Сканер установленных сессий
/// </summary>
public interface ISessionScanner
{
    /// <summary>
    /// Путь к папке установленных сессий
    /// </summary>
    string SessionsDirectory { get; }

    /// <summary>
    /// Установленные сессии
    /// </summary>
    IReadOnlyDictionary<string, Session> DownloadedSessions { get; }

    /// <summary>
    /// Установлена ли сессия с таким идентификатором
    /// </summary>
    bool IsSessionInstalled(string sessionId);

    /// <summary>
    /// Получить установленную сессию по его идентификатору
    /// </summary>
    Session? GetSessionById(string sessionId);

    /// <summary>
    /// Сканировать папку установленных сессий
    /// </summary>
    Task ScanAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Загрузить картинку локальной установленной сессии
    /// </summary>
    byte[]? LoadThumbnail(string sessionId);

    /// <summary>
    /// Зарегистрировать кастомную сессию
    /// </summary>
    void RegisterAdHoc(Guid roomId, Session session);

    /// <summary>
    /// Отрегестрировать кастомную сессию
    /// </summary>
    void UnregisterAdHocForRoom(Guid roomId);
}
