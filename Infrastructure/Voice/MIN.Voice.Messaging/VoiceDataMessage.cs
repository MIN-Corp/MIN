using MIN.Core.Messaging.Contracts;
using MIN.Core.Messaging.Contracts.Enums;
using MIN.Core.Messaging.Contracts.Messages;
using MIN.Core.SubRooms.Contracts.Interfaces.Messages;

namespace MIN.Voice.Messaging;

/// <summary>
/// Сообщение для передачи звуковых данных
/// </summary>
public sealed class VoiceDataMessage : BaseMessage, IWithinSubRoom
{
    /// <inheritdoc />
    public override MessageTypeTag TypeTag => MessageTypeTag.VoiceData;

    /// <inheritdoc />
    public override MessageChannel Channel => MessageChannel.Fast;

    /// <inheritdoc />
    public override bool RequireStreamAcks => false;

    /// <inheritdoc />
    public override bool RequiresLocalDuplication => true;

    /// <inheritdoc />
    public int SubRoomId { get; set; }

    /// <summary>
    /// Порядковый номер
    /// </summary>
    public long SequenceNumber { get; init; }

    /// <summary>
    /// Содержимое сообщения (звук)
    /// </summary>
    public required byte[] Data { get; init; }
}
