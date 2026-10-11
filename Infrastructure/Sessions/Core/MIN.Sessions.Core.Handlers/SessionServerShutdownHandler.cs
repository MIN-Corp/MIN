using MIN.Core.Entities.Contracts.Enums;
using MIN.Core.Handlers.Contracts.Base;
using MIN.Core.Handlers.Contracts.Models;
using MIN.Core.Messaging.Contracts;
using MIN.Core.Messaging.Contracts.Interfaces;
using MIN.Core.Services.Contracts.Interfaces.Messaging;
using MIN.Helpers.Contracts.Interfaces;
using MIN.Sessions.Core.Messaging.OutOfSubRoom;
using MIN.Sessions.Core.Services.Contracts.Interfaces;
using MIN.Sessions.Core.Transport.Contracts.Enums;
using MIN.Sessions.Core.Transport.Contracts.Models;

namespace MIN.Sessions.Core.Handlers;

internal sealed class SessionServerShutdownHandler : BaseHandler
{
    private readonly ISessionProcessManager sessionProcessManager;
    private readonly IMessageSender messageSender;

    /// <summary>
    /// Инициализирует новый экземлпяр <see cref="SessionServerShutdownHandler"/>
    /// </summary>
    public SessionServerShutdownHandler(ISessionProcessManager sessionProcessManager,
        IMessageSender messageSender,
        ILoggerProvider logger) : base(logger)
    {
        this.sessionProcessManager = sessionProcessManager;
        this.messageSender = messageSender;
    }

    public override IEnumerable<MessageTypeTag> HandledTypes => [MessageTypeTag.SessionServerShutdown];

    protected override async Task<HandlerResult> HandleAsync(IMessage message, MessageContext context)
    {
        var sessionServerShutdownMessage = (SessionServerShutdownMessage)message;

        var roomId = context.RoomContext.RoomId;
        var subRoomId = sessionServerShutdownMessage.SubRoomId;

        await sessionProcessManager.StopAsync(new ProcessContext(roomId, subRoomId, SessionProcessRole.Client));

        if (context.Role == Role.Host)
        {
            var outOfSubRoomParticipants = context.RoomContext.Participants.GetParticipants()
                .Select(x => x.Id).Except(context.RoomContext.SubRooms.GetParticipantIds(subRoomId)).ToList();

            var subRoomInfo = context.RoomContext.SubRooms.GetSubRoom(subRoomId);
            var requesterStopped = sessionServerShutdownMessage.SenderId == subRoomInfo?.CreatorId;
            var hostStopped = sessionServerShutdownMessage.SenderId == context.SelfId;

            if (!requesterStopped && !hostStopped)
            {
                LogError("Произошла попытка остановки сервера участником, не имеющего на это права, либо отправившего неккоректный id подкомнаты");
                return HandlerResult.Failure("Произошла попытка остановки сервера участником, не имеющего на это права, либо отправившего неккоректный id подкомнаты");
            }

            context.RoomContext.SubRooms.TryStopSubRoom(subRoomId, sessionServerShutdownMessage.SenderId);

            var excludeConnectionIds = outOfSubRoomParticipants.Select(context.RoomContext.Connections.GetConnectionIdFromParticipantId);
            await messageSender.BroadcastAsync(sessionServerShutdownMessage, roomId, excludeConnectionIds, context.CancellationToken);
            return HandlerResult.Success(stopPropagation: true);
        }

        return HandlerResult.Failure($"Хост остановил сервер сессии: {sessionServerShutdownMessage.Reason}", stopPropagation: true);
    }
}
