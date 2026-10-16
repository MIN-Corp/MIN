using MIN.Core.Entities;
using MIN.Core.Messaging.Contracts;
using MIN.Core.Messaging.Contracts.Messages;

namespace MIN.Core.Messaging.Stateless.RoomRelated.ParticipantRelated;

/// <summary>
/// Участник обновил данные о себе
/// </summary>
public sealed class ParticipantUpdatedMessage : BaseMessage
{
    /// <inheritdoc />
    public override MessageTypeTag TypeTag => MessageTypeTag.ParticipantUpdated;

    /// <inheritdoc />
    public override bool RequiresLocalDuplication => true;

    /// <inheritdoc />
    public override bool IsPublic => true;

    /// <summary>
    /// Новые данные об участнике
    /// </summary>
    public required Participant NewParticipant { get; set; }
}
