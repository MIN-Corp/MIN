using MIN.Core.Entities.Contracts.Models;
using MIN.Core.Messaging.Contracts;
using MIN.Core.Messaging.Contracts.Messages;

namespace MIN.ScreenDemo.Messaging;

/// <summary>
/// Сообщение о начале демонстрации экрана
/// </summary>
public sealed class ScreenDemoStartedMessage : BaseMessage
{
    /// <inheritdoc />
    public override MessageTypeTag TypeTag => MessageTypeTag.ScreenDemoStarted;

    /// <inheritdoc />
    public override bool IsPublic => true;

    /// <inheritdoc />
    public override bool RequiresLocalDuplication => true;

    /// <summary>
    /// Идентификатор подкомнаты
    /// </summary>
    public int SubRoomId { get; set; }

    /// <summary>
    /// Участник, который начал демонстрацию
    /// </summary>
    public ParticipantInfo Sharer { get; set; } = null!;
}
