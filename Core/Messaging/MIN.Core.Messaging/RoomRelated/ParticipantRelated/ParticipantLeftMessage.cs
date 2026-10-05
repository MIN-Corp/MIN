using MIN.Common.Core.Contracts.Interfaces;
using MIN.Core.Entities.Contracts.Models;
using MIN.Core.Messaging.Contracts;
using MIN.Core.Messaging.Contracts.Messages;
using MIN.Core.Transport.Contracts.Enum;

namespace MIN.Core.Messaging.RoomRelated.ParticipantRelated;

/// <summary>
/// Уведомление о выходе участника из комнаты
/// </summary>
public sealed class ParticipantLeftMessage : BaseMessage, IDescribable
{
    /// <inheritdoc />
    public override MessageTypeTag TypeTag => MessageTypeTag.ParticipantLeft;

    /// <inheritdoc />
    public override bool IsPublic => true;

    /// <summary>
    /// Информация о покинувшем участнике
    /// </summary>
    public ParticipantInfo Participant { get; set; } = null!;

    /// <summary>
    /// Причина выхода
    /// </summary>
    public DisconnectReason Reason { get; set; }

    /// <summary>
    /// Покинул ли участник комнату, да так что из списка больше не числиться
    /// </summary>
    public bool IsLeftRoom => Reason == DisconnectReason.Kick || Reason == DisconnectReason.LeftRoom;

    string IDescribable.GetDescription() => Reason == DisconnectReason.Kick
        ? $"Хост кикнул {Participant.Name}"
        : Reason == DisconnectReason.Timeout
        ? $"У участника {Participant.Name} пропала связь"
        : $"Участник {Participant.Name} покинул комнату";
}
