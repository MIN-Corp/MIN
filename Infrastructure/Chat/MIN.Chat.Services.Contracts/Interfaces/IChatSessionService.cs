using MIN.Sessions.Core.Messaging.OutOfSubRoom;
using MIN.Sessions.Core.Services.Contracts.Models;

namespace MIN.Chat.Services.Contracts.Interfaces;

/// <summary>
/// Сервис для работы с сессиями в чате
/// </summary>
public interface IChatSessionService
{
    /// <summary>
    /// Отправить запрос на хостинг сессии
    /// </summary>
    Task SendSessionHostRequestAsync(Guid roomId, Session selectedSession, CancellationToken cancellationToken = default);

    /// <summary>
    /// Запросить вход в сессию
    /// </summary>
    Task SendSessionJoinRequest(Guid roomId, SessionReadyMessage sessionReadyMessage, CancellationToken cancellationToken = default);

    /// <summary>
    /// Просканировать загруженные сессии
    /// </summary>
    Task ScanDownloadedSessions(CancellationToken cancellationToken = default);

    /// <summary>
    /// Создать и захостить туннельную сессию
    /// </summary>
    /// <param name="roomId">Идентификатор комнаты</param>
    /// <param name="name">Название игры</param>
    /// <param name="gamePort">Порт, на котором слушает игровой сервер</param>
    /// <param name="maximumParticipants">Максимальное количество игроков</param>
    /// <param name="version">Версия игры</param>
    /// <param name="thumbnailData">Обложка сессии</param>
    /// <param name="cancellationToken">Токен отмены</param>
    Task HostTunnelSessionAsync(Guid roomId, string name, int gamePort,
       int? maximumParticipants, string? version, byte[]? thumbnailData,
       CancellationToken cancellationToken = default);

    /// <summary>
    /// Удалить сообщение сессии (останавливает сессию, если запущена)
    /// </summary>
    Task DeleteSessionMessageAsync(Guid roomId, SessionReadyMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Покинуть сессию
    /// </summary>
    Task SendSessionLeaveAsync(Guid roomId, int subRoomId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Остановить сессию (только хост)
    /// </summary>
    Task SendSessionStopAsync(Guid roomId, int subRoomId, CancellationToken cancellationToken = default);
}
