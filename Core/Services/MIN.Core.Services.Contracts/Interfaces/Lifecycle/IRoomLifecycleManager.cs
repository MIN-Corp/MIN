using MIN.Core.Entities;
using MIN.Core.Entities.Contracts.Models;
using MIN.Core.Services.Contracts.Models;
using MIN.Core.Transport.Contracts.Enum;
using MIN.Core.Transport.Contracts.Interfaces;
using MIN.Core.Transport.Contracts.Models;

namespace MIN.Core.Services.Contracts.Interfaces.Lifecycle;

/// <summary>
/// Единый оркестратор жизненного цикла комнат: подключение/отключение (клиент) и хостинг (сервер)
/// </summary>
public interface IRoomLifecycleManager
{
    /// <summary>
    /// Подключиться к удалённой комнате (клиентская сторона)
    /// </summary>
    /// <param name="endpoint">Точка подключения</param>
    /// <param name="expectedRoomId">Ожидаемый id комнаты</param>
    /// <param name="password">Пароль комнаты (null - не указан)</param>
    /// <param name="cancellationToken">Токен отмены</param>
    Task<ConnectionResult> ConnectAsync(IEndpoint endpoint, Guid? expectedRoomId, string? password = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Пометить комнату на удаление при следующем подключении (клиентская сторона)
    /// </summary>
    void MarkRoomForDeletion(Guid roomId);

    /// <summary>
    /// Отключиться от удалённой комнаты (клиентская сторона)
    /// </summary>
    Task DisconnectAsync(Guid roomId, Guid connectionId);

    /// <summary>
    /// Отключиться от удалённой комнаты и забыть её (клиентская сторона)
    /// </summary>
    Task ForgetRoomAsync(Guid roomId, Guid connectionId);

    /// <summary>
    /// Хост уведомлён о выходе, можно отключиться
    /// </summary>
    void CompleteRoomLeaveAck(Guid roomId);

    /// <summary>
    /// Начать хостинг комнаты (серверная сторона)
    /// </summary>
    Task<Room> StartHostingAsync(RoomInfo roomInfo, NetworkOptions networkOptions, string? password, CancellationToken cancellationToken = default);

    /// <summary>
    /// Обновить настройки сети комнаты
    /// </summary>
    Task<IEnumerable<IEndpoint>> UpdateNetworkOptions(Guid roomId, NetworkOptions newNetworkOptions, CancellationToken cancellationToken = default);

    /// <summary>
    /// Установить или снять пароль хостимой комнаты
    /// </summary>
    Task UpdateRoomPasswordAsync(Guid roomId, string? password, CancellationToken cancellationToken = default);

    /// <summary>
    /// Пометить участника, как вышедшего из комнаты надолго
    /// </summary>
    void MarkParticipantAsLeftRoom(Guid roomId, Guid participantId);

    /// <summary>
    /// Кикнуть участника из комнаты
    /// </summary>
    Task KickClientAsync(Guid roomId, Guid participantId, DisconnectReason reason, string message);

    /// <summary>
    /// Кикнуть участника по соединению из комнаты
    /// </summary>
    Task KickConnectionAsync(Guid roomId, Guid connectionId, DisconnectReason reason);

    /// <summary>
    /// Остановить хостинг комнаты
    /// </summary>
    Task StopHostingAsync(Guid roomId);

    /// <summary>
    /// Остановить хостинг комнаты и забыть её
    /// </summary>
    Task ForgetHostingAsync(Guid roomId);
}
