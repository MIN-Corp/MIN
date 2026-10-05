using MIN.Core.Messaging.Contracts;
using MIN.Core.Messaging.Contracts.Messages;

namespace MIN.Core.Messaging.Stateless.RoomRelated.RoomInfo;

/// <summary>
/// Запрос информации о комнате
/// </summary>
public sealed class RoomInfoRequestMessage : BaseMessage
{
    /// <inheritdoc />
    public override MessageTypeTag TypeTag => MessageTypeTag.RoomInfoRequest;

    /// <inheritdoc />
    public override bool IsPublic => false;

    /// <summary>
    /// Перезаходит ли участник
    /// </summary>
    public bool IsRejoin { get; set; }
}
