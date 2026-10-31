using MIN.Core.Entities.Contracts.Models;
using MIN.Core.Messaging.Contracts;
using MIN.Core.Messaging.Contracts.Messages;
using MIN.Core.SubRooms.Contracts.Interfaces.Messages;

namespace MIN.ScreenDemo.Messaging;

/// <summary>
/// Сообщение об уходе зрителя из демонстрации экрана.
/// Рассылается хостом после обработки выхода
/// </summary>
public sealed class ScreenDemoViewerLeftMessage : BaseMessage, IWithinSubRoom
{
    /// <inheritdoc />
    public override MessageTypeTag TypeTag => MessageTypeTag.ScreenDemoViewerLeft;

    /// <inheritdoc />
    public override bool IsPublic => true;

    /// <summary>
    /// RequiresLocalDuplication = true — в отличие от голосового ParticipantLeft:
    /// демонстрирующий (в том числе если он хост) обязан получить это сообщение,
    /// чтобы при нулях зрителей прекратить отправку кадров
    /// </summary>
    public override bool RequiresLocalDuplication => true;

    /// <summary>
    /// Информация о зрителе
    /// </summary>
    public ParticipantInfo Participant { get; set; } = null!;

    /// <inheritdoc />
    public int SubRoomId { get; set; }
}
