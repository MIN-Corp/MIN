using MIN.Core.Entities.Contracts.Enums;
using MIN.Core.Entities.Contracts.Extensions;
using MIN.Core.Handlers.Contracts.Base;
using MIN.Core.Handlers.Contracts.Models;
using MIN.Core.Messaging.Contracts;
using MIN.Core.Messaging.Contracts.Interfaces;
using MIN.Core.Services.Contracts.Interfaces.Messaging;
using MIN.Core.Stores.Contracts.Enums;
using MIN.Helpers.Contracts.Interfaces;
using MIN.Voice.Events;
using MIN.Voice.Messaging;

namespace MIN.Voice.Handlers;

internal sealed class VoiceCallStartHandler : BaseHandler
{
    private readonly IMessageSender messageSender;
    private readonly IMessageRouter messageRouter;

    /// <summary>
    /// Инициализирует новый экземлпяр <see cref="VoiceCallStartHandler"/>
    /// </summary>
    public VoiceCallStartHandler(IMessageSender messageSender,
        IMessageRouter messageRouter,
        ILoggerProvider logger) : base(logger)
    {
        this.messageSender = messageSender;
        this.messageRouter = messageRouter;
    }

    public override IEnumerable<MessageTypeTag> HandledTypes => [MessageTypeTag.VoiceCallStartRequest];

    protected override async Task<HandlerResult> HandleAsync(IMessage message, MessageContext context)
    {
        if (context.Role != Role.Host)
        {
            return HandlerResult.Failure($"Получил сообщение {message.GetType()} в {nameof(VoiceCallStartHandler)} как {context.Role}, хотя не должен был", stopPropagation: false);
        }

        if (!context.RoomContext.Participants.TryGetParticipantById(message.SenderId, out var sender))
        {
            return HandlerResult.Failure("Получил сообщение от неизвестного отправителя", stopPropagation: false, critical: true);
        }

        var senderParicipantInfo = sender!.ToParticipantInfo();
        var subRoomInfo = context.RoomContext.SubRooms.HostSubRoom(senderParicipantInfo, SubRoomPurpose.Voice);
        var subRoomId = subRoomInfo.Id;

        var voiceCallStartedMessage = new VoiceCallStartedMessage()
        {
            SubRoomId = subRoomId,
            Sender = senderParicipantInfo,
            SenderId = message.SenderId,
        };

        await messageRouter.RouteAsync(voiceCallStartedMessage, context.RoomContext.RoomId, message.SenderId, context.CancellationToken);

        if (context.SelfId == message.SenderId)
        {
            return HandlerResult.WithEvent(new VoiceCallEstablishedEvent()
            {
                RoomId = context.RoomContext.RoomId,
                SubRoomId = subRoomId,
            });
        }
        else
        {
            // sending him ready as he didnt received by sender filtering
            await messageSender.SendAsync(voiceCallStartedMessage, context.RoomContext.RoomId, context.ConnectionId, context.CancellationToken);

            return HandlerResult.WithResponse(new VoiceCallEstablishedMessage()
            {
                NeedToAnnounce = false,
                SubRoomId = subRoomId,
            });
        }
    }
}
