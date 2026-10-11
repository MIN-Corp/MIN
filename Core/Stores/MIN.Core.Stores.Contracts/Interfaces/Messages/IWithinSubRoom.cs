namespace MIN.Core.SubRooms.Contracts.Interfaces.Messages;

/// <summary>
/// Сообщение относиться к подкомнате
/// </summary>
public interface IWithinSubRoom
{
    /// <summary>
    /// Идентификатор подкомнаты
    /// </summary>
    int SubRoomId { get; set; }
}
