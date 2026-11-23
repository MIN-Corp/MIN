using MIN.Common.Core.Contracts.Interfaces;
using MIN.Core.Entities.Contracts.Models;
using MIN.Core.Messaging.Contracts;
using MIN.Core.Messaging.Contracts.Interfaces;
using MIN.Core.Messaging.Contracts.Messages;
using MIN.Sessions.Core.Services.Contracts.Models;

namespace MIN.Sessions.Core.Messaging.OutOfSubRoom;

/// <summary>
/// Сообщение готовности хостинга сессии
/// </summary>
public sealed class SessionReadyMessage : BaseUpdatableMessage, IDescribable, IAuthoredMessage
{
    /// <inheritdoc />
    public override MessageTypeTag TypeTag => MessageTypeTag.SessionReady;

    /// <inheritdoc />
    public override bool IsPublic => true;

    /// <summary>
    /// Идентификатор подкомнаты
    /// </summary>
    public int SubRoomId { get; init; }

    /// <summary>
    /// Текущее количество участников в ней
    /// </summary>
    public int CurrentParticipantAmount { get; set; }

    /// <summary>
    /// Выбранная сессия
    /// </summary>
    public Session Session { get; init; } = null!;

    /// <summary>
    /// Обложка сессии
    /// </summary>
    /// <remarks>
    /// Null, если её нет
    /// </remarks>
    public byte[]? ThumbnailData { get; init; }

    /// <summary>
    /// Отправитель сообщения
    /// </summary>
    public ParticipantInfo Sender { get; init; } = new();

    /// <inheritdoc />
    public string SenderName { get => Sender.Name; set => Sender.Name = value; }

    string IDescribable.GetDescription() => $"{Sender.Name} запустил \"{Session.Name}\"";

    /// <inheritdoc />
    public override void Update(IMessage newer)
    {
        base.Update(newer);
        if (newer is SessionReadyMessage sessionReadyMessage)
        {
            CurrentParticipantAmount = sessionReadyMessage.CurrentParticipantAmount;
        }
    }
}
