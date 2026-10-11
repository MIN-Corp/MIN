using MIN.Core.Entities.Contracts.Enums;
using MIN.Core.Handlers.Contracts.Base;
using MIN.Core.Handlers.Contracts.Models;
using MIN.Core.Messaging.Contracts;
using MIN.Core.Messaging.Contracts.Interfaces;
using MIN.Core.Services.Contracts.Interfaces.Messaging;
using MIN.Core.Stores.Contracts.Enums;
using MIN.Helpers.Contracts.Interfaces;
using MIN.Sessions.Core.Events;
using MIN.Sessions.Core.Messaging.Ipc;
using MIN.Sessions.Core.Messaging.OutOfSubRoom;
using MIN.Sessions.Core.Services.Contracts.Interfaces;

namespace MIN.Sessions.Core.Handlers;

internal sealed class SessionParticipantJoinedHandler : BaseHandler
{
    private readonly ISessionProcessBridge sessionProcessBridge;
    private readonly IMessageRouter messageRouter;

    /// <summary>
    /// Инициализирует новый экземлпяр <see cref="SessionParticipantJoinedHandler"/>
    /// </summary>
    public SessionParticipantJoinedHandler(ISessionProcessBridge sessionProcessBridge,
        IMessageRouter messageRouter,
        ILoggerProvider logger) : base(logger)
    {
        this.messageRouter = messageRouter;
        this.sessionProcessBridge = sessionProcessBridge;
    }

    public override IEnumerable<MessageTypeTag> HandledTypes => [MessageTypeTag.SessionParticipantJoined];

    protected override async Task<HandlerResult> HandleAsync(IMessage message, MessageContext context)
    {
        var sessionParticipantJoinedMessage = (SessionParticipantJoinedMessage)message;

        var roomId = context.RoomContext.RoomId;
        var subRoomId = sessionParticipantJoinedMessage.SubRoomId;
        var participant = sessionParticipantJoinedMessage.Participant;

        if (context.Role == Role.Host)
        {
            var joinResult = context.RoomContext.SubRooms.TryJoinSubRoom(sessionParticipantJoinedMessage.SubRoomId, participant);

            if (joinResult != SubRoomJoinOutcome.Success)
            {
                var error = joinResult switch
                {
                    SubRoomJoinOutcome.RoomNotFound => "Комната не нашлась",
                    SubRoomJoinOutcome.SubRoomNotFound => "Нету информации о подкомнате",
                    SubRoomJoinOutcome.AlreadyJoined => "Вы уже учавствуете в этой сессии",
                    SubRoomJoinOutcome.MaximumParticipants => "Достигнут лимит участия в этой сессии",
                    _ => "Не удалось войти"
                };

                await messageRouter.RouteAsync(new SessionJoinFailedMessage()
                {
                    Message = error,
                    SubRoomId = subRoomId
                }, roomId, context.SelfId, context.CancellationToken);

                return HandlerResult.Success(stopPropagation: true);
            }
        }

        var processContexts = sessionProcessBridge.GetConnections(roomId, subRoomId);

        foreach (var processContext in processContexts)
        {
            await sessionProcessBridge.SendIpcMessage(new ParticipantConnectedMessage(participant.Id.ToString(),
                participant.Name), processContext, message.SenderId, context.CancellationToken);
        }

        var existingSessionReadyMessageId = context.RoomContext.Messages.GetHistory()
            .OfType<SessionReadyMessage>().FirstOrDefault(x => x.SubRoomId == sessionParticipantJoinedMessage.SubRoomId)?.Id;

        if (existingSessionReadyMessageId != null)
        {
            var existing = context.RoomContext.Messages.GetMessageById(existingSessionReadyMessageId.Value) as SessionReadyMessage;
            existing!.CurrentParticipantAmount++;
            context.RoomContext.Messages.UpdateMessage(existing.Id, existing);
        }

        return HandlerResult.WithEvent(new SessionParticipantJoinedEvent()
        {
            Participant = participant,
            SubRoomId = subRoomId,
            RoomId = roomId,
        });
    }
}
