using MIN.Core.Entities.Contracts.Enums;
using MIN.Core.Entities.Contracts.Extensions;
using MIN.Core.Events.Contracts.Interfaces;
using MIN.Core.Handlers.Contracts.Base;
using MIN.Core.Handlers.Contracts.Models;
using MIN.Core.Messaging.Contracts;
using MIN.Core.Messaging.Contracts.Interfaces;
using MIN.Core.Services.Contracts.Interfaces.Messaging;
using MIN.Helpers.Contracts.Interfaces;
using MIN.Sessions.Core.Events;
using MIN.Sessions.Core.Messaging.OutOfSubRoom;
using MIN.Sessions.Core.Services.Contracts.Interfaces;
using MIN.Sessions.Core.Transport.Contracts.Enums;
using MIN.Sessions.Core.Transport.Contracts.Models;

namespace MIN.Sessions.Core.Handlers;

internal sealed class SessionServerShutdownHandler : BaseHandler
{
    private readonly ISessionInstanceManager sessionInstanceManager;
    private readonly IMessageRouter messageRouter;
    private readonly IEventBus eventBus;
    private readonly IMessageSender messageSender;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="SessionServerShutdownHandler"/>
    /// </summary>
    public SessionServerShutdownHandler(ISessionInstanceManager sessionInstanceManager,
        IMessageRouter messageRouter,
        IEventBus eventBus,
        IMessageSender messageSender,
        ILoggerProvider logger) : base(logger)
    {
        this.sessionInstanceManager = sessionInstanceManager;
        this.messageRouter = messageRouter;
        this.eventBus = eventBus;
        this.messageSender = messageSender;
    }

    public override IEnumerable<MessageTypeTag> HandledTypes => [MessageTypeTag.SessionServerShutdown];

    protected override async Task<HandlerResult> HandleAsync(IMessage message, MessageContext context)
    {
        var sessionServerShutdownMessage = (SessionServerShutdownMessage)message;

        var roomId = context.RoomContext.RoomId;
        var subRoomId = sessionServerShutdownMessage.SubRoomId;

        await sessionInstanceManager.StopAsync(new SessionInstanceContext(roomId, subRoomId, SessionInstanceRole.Client));

        if (context.Role != Role.Host)
        {
            return HandlerResult.Failure($"Хост остановил сервер сессии: {sessionServerShutdownMessage.Reason}", stopPropagation: true);
        }

        var outOfSubRoomParticipants = context.RoomContext.Participants.GetParticipants()
            .Select(x => x.Id).Except(context.RoomContext.SubRooms.GetParticipantIds(subRoomId)).ToList();

        var subRoomInfo = context.RoomContext.SubRooms.GetSubRoom(subRoomId);
        var requesterStopped = sessionServerShutdownMessage.SenderId == subRoomInfo?.CreatorId;
        var hostStopped = sessionServerShutdownMessage.SenderId == context.SelfId;

        if (!requesterStopped && !hostStopped)
        {
            LogError("Произошла попытка остановки сервера участником, не имеющего на это права, либо отправившего некорректный id подкомнаты");
            return HandlerResult.Failure("Произошла попытка остановки сервера участником, не имеющего на это права, либо отправившего некорректный id подкомнаты");
        }

        var inSubRoomParticipantIds = context.RoomContext.SubRooms.GetParticipantIds(subRoomId);

        context.RoomContext.SubRooms.TryStopSubRoom(subRoomId, sessionServerShutdownMessage.SenderId);

        await eventBus.PublishAsync(new SessionDeactivatedEvent()
        {
            RoomId = roomId,
            SubRoomId = subRoomId,
        }).ConfigureAwait(false);

        foreach (var participantId in inSubRoomParticipantIds)
        {
            if (context.RoomContext.Participants.TryGetParticipantById(participantId, out var participant))
            {
                await messageRouter.RouteAsync(new SessionParticipantLeftMessage()
                {
                    SubRoomId = subRoomId,
                    Participant = participant!.ToParticipantInfo(),
                    IsLast = false,
                }, roomId, context.SelfId, context.CancellationToken).ConfigureAwait(false);
            }
        }

        var excludeConnectionIds = outOfSubRoomParticipants
            .Where(x => context.RoomContext.Connections.TryGetConnectionIdFromParticipantId(x, out _))
            .Select(context.RoomContext.Connections.GetConnectionIdFromParticipantId);

        await messageSender.BroadcastAsync(sessionServerShutdownMessage, roomId, excludeConnectionIds, context.CancellationToken);
        return HandlerResult.Success(stopPropagation: true);
    }
}
