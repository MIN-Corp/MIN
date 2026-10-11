using MIN.Core.Entities;

namespace MIN.Core.Stores.Contracts.Interfaces;

/// <summary>
/// Реестр комнат и участников в ней
/// </summary>
public interface IRoomStore
{
    /// <summary>
    /// Получить всю информацию о комнате по идентификатору
    /// </summary>
    Room GetRoom(Guid roomId);

    /// <summary>
    /// Попытаться получить комнату по идентификатору
    /// </summary>
    bool TryGetRoom(Guid roomId, out Room room);

    /// <summary>
    /// Получить всю информацию о комнате по идентификатору участника (для того, чтобы сохранить его приватные сообщения)
    /// </summary>
    Room GetRoomFor(Guid participantId, Guid roomId, bool asRejoin = false);

    /// <summary>
    /// Получить количество сообщений для участника (для того, чтобы сохранить его приватные сообщения)
    /// </summary>
    int GetRoomChatHistoryCountFor(Guid participantId, Guid roomId);

    /// <summary>
    /// Существует ли такая комната в списке?
    /// </summary>
    bool RoomExists(Guid roomId);

    /// <summary>
    /// Получить идентификатор хоста комнаты по её идентификатору
    /// </summary>
    Guid GetRoomHostParticipantId(Guid roomId);

    /// <summary>
    /// Получить все комнаты
    /// </summary>
    IEnumerable<Room> GetAllRooms();

    /// <summary>
    /// Добавить комнату 
    /// </summary>
    void Register(Room room);

    /// <summary>
    /// Удалить комнату
    /// </summary>
    void Remove(Guid roomId);
}
