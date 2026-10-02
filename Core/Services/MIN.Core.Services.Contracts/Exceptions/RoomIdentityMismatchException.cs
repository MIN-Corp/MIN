using MIN.Core.Entities.Contracts.Models;

namespace MIN.Core.Services.Contracts.Exceptions;

/// <summary>
/// Ошибка смены идентфикации комнаты при подключении
/// </summary>
public class RoomIdentityMismatchException(Guid connectionId, Guid expectedRoomId, bool existedBefore, RoomInfo actualRoom)
: Exception($"Ожидалась комната {expectedRoomId}, но по адресу хостится {actualRoom.Id}")
{
    /// <summary>
    /// Идентификатор соединения
    /// </summary>
    public Guid ConnectionId { get; } = connectionId;

    /// <summary>
    /// Идентификатор ожидаемой комнаты
    /// </summary>
    public Guid ExpectedRoomId { get; } = expectedRoomId;

    /// <summary>
    /// Комната уже существовала до этого
    /// </summary>
    public bool ExistedBefore { get; } = existedBefore;

    /// <summary>
    /// Информация о новой комнате
    /// </summary>
    public RoomInfo ActualRoom { get; } = actualRoom;
}
