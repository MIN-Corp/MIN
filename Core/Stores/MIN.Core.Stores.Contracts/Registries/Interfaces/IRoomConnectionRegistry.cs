using MIN.Core.Entities.Contracts.Enums;

namespace MIN.Core.Stores.Contracts.Registries.Interfaces;

/// <summary>
/// Единый источник правды о комнатах: роль и связь комнаты с соединениями
/// </summary>
public interface IRoomConnectionRegistry
{
    /// <summary>
    /// Роль в комнате (Host/Client)
    /// </summary>
    Role GetRole(Guid roomId);

    /// <summary>
    /// Хостится ли комната нами (серверная сторона)
    /// </summary>
    bool IsHosting(Guid roomId);

    /// <summary>
    /// Подключены ли мы клиентом к комнате
    /// </summary>
    bool IsConnected(Guid roomId);

    // --- Хост-сторона (пишет RoomHoster) ---

    /// <summary>
    /// Зарегистрировать серверное соединение
    /// </summary>
    void RegisterServerConnection(Guid roomId, Guid serverConnectionId);

    /// <summary>
    /// Отрегистрировать (забыть) серверное соединение
    /// </summary>
    void UnregisterServerConnection(Guid roomId);

    /// <summary>
    /// Пометить серверное соединение как оффлайн
    /// </summary>
    void DetachServerConnection(Guid roomId);

    /// <summary>
    /// Получить идентификатор серверного соединения по идентификатору комнаты
    /// </summary>
    Guid GetServerConnectionIdByRoomId(Guid roomId);

    /// <summary>
    /// Получить идентификатор комнаты по идентификатору серверного соединения
    /// </summary>
    Guid GetRoomIdByServerConnectionId(Guid serverConnectionId);

    /// <summary>
    /// Попытаться получить идентификатор серверного соединения по идентификатору комнаты
    /// </summary>
    bool TryGetServerConnectionIdByRoomId(Guid? roomId, out Guid connectionId);

    /// <summary>
    /// Попытаться получить идентификатор комнаты по идентификатору серверного соединения
    /// </summary>
    bool TryGetRoomIdByServerConnectionId(Guid? serverConnectionId, out Guid roomId);

    /// <summary>
    /// Получить число серверных комнат
    /// </summary>
    int GetServerConnectionCount();

    // --- Клиент-сторона (пишет RoomConnector) ---

    /// <summary>
    /// Зарегистрировать клиентское соединение
    /// </summary>
    void RegisterClientConnection(Guid roomId, Guid connectionId);

    /// <summary>
    /// Отрегистрировать клиентское соединение
    /// </summary>
    void UnregisterClientConnection(Guid connectionId);

    /// <summary>
    /// Получить идентификатор клиентского соединения по идентификатору комнаты
    /// </summary>
    Guid GetClientConnectionIdByRoomId(Guid roomId);

    /// <summary>
    /// Получить идентификатор комнаты по идентификатору клиентского соединения
    /// </summary>
    Guid GetRoomIdByClientConnectionId(Guid connectionId);

    /// <summary>
    /// Попытаться получить идентификатор клиентского соединения по идентификатору комнаты
    /// </summary>
    bool TryGetClientConnectionIdByRoomId(Guid? roomId, out Guid connectionId);

    /// <summary>
    /// Попытаться получить идентификатор комнаты по идентификатору клиентского соединения
    /// </summary>
    bool TryGetRoomIdByClientConnectionId(Guid? connectionId, out Guid roomId);

    /// <summary>
    /// Получить число клиентских комнат
    /// </summary>
    int GetClientConnectionCount();
}
