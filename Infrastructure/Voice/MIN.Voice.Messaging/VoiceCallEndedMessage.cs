using MIN.Core.Messaging.Contracts;
using MIN.Core.Messaging.Contracts.Messages;

namespace MIN.Voice.Messaging;

/// <summary>
/// Сообщение прекращения звонка
/// </summary>
public sealed class VoiceCallEndedMessage : BaseMessage
{
    /// <inheritdoc />
    public override MessageTypeTag TypeTag => MessageTypeTag.VoiceCallEnded;

    /// <inheritdoc />
    public override bool IsPublic => true;

    /// <summary>
    /// Идентификатор подкомнаты
    /// </summary>
    public int SubRoomId { get; init; }
}
