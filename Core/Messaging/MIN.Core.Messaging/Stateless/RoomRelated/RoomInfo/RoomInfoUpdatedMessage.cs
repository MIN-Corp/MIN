using MIN.Core.Messaging.Contracts;
using MIN.Core.Messaging.Contracts.Messages;
using MIN.Core.Transport.Contracts.Interfaces;

namespace MIN.Core.Messaging.Stateless.RoomRelated.RoomInfo;

/// <summary>
/// Обновление информации о комнате
/// </summary>
public sealed class RoomInfoUpdatedMessage : BaseMessage
{
    /// <inheritdoc />
    public override MessageTypeTag TypeTag => MessageTypeTag.RoomInfoUpdated;

    /// <inheritdoc />
    public override bool RequiresLocalDuplication => true;

    /// <summary>
    /// Информация о комнате
    /// </summary>
    public required Entities.Contracts.Models.RoomInfo Room { get; set; }

    /// <summary>
    /// Новые адреса подключения
    /// </summary>
    public required IEnumerable<IEndpoint> Endpoints { get; set; }
}
