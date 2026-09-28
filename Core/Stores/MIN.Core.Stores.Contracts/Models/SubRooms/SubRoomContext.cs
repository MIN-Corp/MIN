namespace MIN.Core.Stores.Contracts.Models.SubRooms;

/// <summary>
/// Контекст подкомнаты в какой-то комнате
/// </summary>
public readonly record struct SubRoomContext
{
    /// <summary>
    /// Идентификатор комнаты
    /// </summary>
    public Guid RoomId { get; init; }

    /// <summary>
    /// Идентификатор подкомнаты
    /// </summary>
    public int SubRoomId { get; init; }

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="SubRoomContext"/>
    /// </summary>
    public SubRoomContext(Guid roomId, int subRoomId)
    {
        RoomId = roomId;
        SubRoomId = subRoomId;
    }
}
