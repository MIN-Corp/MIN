using MIN.Core.Entities.Contracts.Models;
using MIN.Core.Messaging.Contracts;
using MIN.Core.Messaging.Contracts.Messages;
using MIN.Core.SubRooms.Contracts.Interfaces.Messages;

namespace MIN.ScreenDemo.Messaging;

/// <summary>
/// Сообщение о присоединении зрителя к демонстрации экрана.
/// Рассылается самим зрителем после подтверждения — демонстрирующий
/// по нему узнаёт, что можно начинать отправку кадров
/// </summary>
public sealed class ScreenDemoViewerJoinedMessage : BaseMessage, IWithinSubRoom
{
    /// <inheritdoc />
    public override MessageTypeTag TypeTag => MessageTypeTag.ScreenDemoViewerJoined;

    /// <inheritdoc />
    public override bool IsPublic => true;

    /// <inheritdoc />
    public override bool RequiresLocalDuplication => true;

    /// <summary>
    /// Информация о зрителе
    /// </summary>
    public ParticipantInfo Participant { get; set; } = null!;

    /// <inheritdoc />
    public int SubRoomId { get; set; }
}
