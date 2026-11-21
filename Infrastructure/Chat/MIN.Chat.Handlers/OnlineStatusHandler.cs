using MIN.Chat.Events;
using MIN.Chat.Messaging;
using MIN.Core.Entities.Contracts.Enums;
using MIN.Core.Entities.Contracts.Extensions;
using MIN.Core.Handlers.Contracts.Base;
using MIN.Core.Handlers.Contracts.Models;
using MIN.Core.Messaging.Contracts;
using MIN.Core.Messaging.Contracts.Interfaces;
using MIN.Helpers.Contracts.Interfaces;

namespace MIN.Chat.Handlers;

internal sealed class OnlineStatusHandler : BaseHandler
{
    /// <summary>
    /// Инициализирует новый экземлпяр <see cref="ChatTextHandler"/>
    /// </summary>
    public OnlineStatusHandler(ILoggerProvider logger) : base(logger) { }

    public override IEnumerable<MessageTypeTag> HandledTypes => [MessageTypeTag.OnlineStatusChanged];

    protected override Task<HandlerResult> HandleAsync(IMessage message, MessageContext context)
    {
        if (message.SenderId == context.SelfId)
        {
            return Task.FromResult(HandlerResult.Success());
        }

        var onlineStatusChangedMessage = (OnlineStatusChangedMessage)message;

        if (!context.RoomContext.Participants.TryGetParticipantById(message.SenderId, out var participant))
        {
            LogWarning($"Статус от неизвестного участника {message.SenderId} — игнорирую");
            return Task.FromResult(HandlerResult.Success());
        }

        var status = onlineStatusChangedMessage.Status;
        participant!.CurrentStatus = status;
        if (status == OnlineStatus.Offline)
        {
            participant.LastSeenOnline = DateTime.Now;
        }

        return Task.FromResult(HandlerResult.WithEvent(new OnlineStatusChangedEvent()
        {
            RoomId = context.RoomContext.RoomId,
            Status = onlineStatusChangedMessage.Status,
            Participant = participant.ToParticipantInfo(),
        }));
    }
}
