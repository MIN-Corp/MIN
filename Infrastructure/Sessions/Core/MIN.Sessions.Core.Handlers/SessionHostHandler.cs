using MIN.Core.Entities.Contracts.Enums;
using MIN.Core.Entities.Contracts.Extensions;
using MIN.Core.Handlers.Contracts.Base;
using MIN.Core.Handlers.Contracts.Models;
using MIN.Core.Messaging.Contracts;
using MIN.Core.Messaging.Contracts.Interfaces;
using MIN.Core.Services.Contracts.Interfaces.Messaging;
using MIN.Core.SubRooms.Contracts.Enums;
using MIN.Core.SubRooms.Contracts.Interfaces;
using MIN.Helpers.Contracts.Interfaces;
using MIN.Sessions.Core.Messaging.Ipc;
using MIN.Sessions.Core.Messaging.OutOfSubRoom;
using MIN.Sessions.Core.Services.Contracts.Interfaces;
using MIN.Sessions.Core.Transport.Contracts.Enums;
using MIN.Sessions.Core.Transport.Contracts.Models;

namespace MIN.Sessions.Core.Handlers;

internal sealed class SessionHostHandler : BaseHandler
{
    private readonly ISubRoomManager subRoomManager;
    private readonly IMessageSender messageSender;
    private readonly IMessageRouter messageRouter;
    private readonly ISessionScanner sessionScanner;
    private readonly ISessionProcessBridge sessionProcessBridge;
    private readonly ISessionProcessManager sessionProcessManager;

    public SessionHostHandler(ISubRoomManager subRoomManager,
        IMessageSender messageSender,
        IMessageRouter messageRouter,
        ISessionScanner sessionScanner,
        ISessionProcessBridge sessionProcessBridge,
        ISessionProcessManager sessionProcessManager,
        ILoggerProvider logger) : base(logger)
    {
        this.subRoomManager = subRoomManager;
        this.messageSender = messageSender;
        this.messageRouter = messageRouter;
        this.sessionScanner = sessionScanner;
        this.sessionProcessBridge = sessionProcessBridge;
        this.sessionProcessManager = sessionProcessManager;
    }

    public override IEnumerable<MessageTypeTag> HandledTypes => [MessageTypeTag.SessionHostRequest];

    protected override async Task<HandlerResult> HandleAsync(IMessage message, MessageContext context)
    {
        var sessionHostRequestMessage = (SessionHostRequestMessage)message;

        if (context.Role != Role.Host)
        {
            return HandlerResult.Failure($"Получил сообщение {message.GetType()} в {nameof(SessionHostHandler)} как {context.Role}, хотя не должен был", stopPropagation: false);
        }

        if (!context.RoomContext.Participants.TryGetParticipantById(message.SenderId, out var sender))
        {
            return HandlerResult.Failure("Получил сообщение от неизвестного отправителя", stopPropagation: false, critical: true);
        }

        var senderParicipantInfo = sender!.ToParticipantInfo();
        var roomId = context.RoomContext.RoomId;

        if (sessionHostRequestMessage.SubRoomId != null
            && !subRoomManager.ActivateSubRoom(roomId, sessionHostRequestMessage.SubRoomId.Value, senderParicipantInfo))
        {
            return HandlerResult.WithErrorHandled("Хост не смог создать подкомнату");
        }

        var session = sessionScanner.GetSessionById(sessionHostRequestMessage.SessionId);

        if (session == null)
        {
            return HandlerResult.WithErrorHandled("У хоста не установлена программа сервера этой сессии");
        }

        if (session.Version != sessionHostRequestMessage.SessionVersion)
        {
            var clientOnOlderVersion = session.Version > sessionHostRequestMessage.SessionVersion ? "Вы" : "Хост";
            return HandlerResult.WithErrorHandled($"{clientOnOlderVersion} на устаревшей версии сессии: " +
                $"\nВаша версия сессии - {sessionHostRequestMessage.SessionVersion}" +
                $"\nВерсия сессии хоста комнаты - {session.Version}");
        }

        var subRoomId = sessionHostRequestMessage.SubRoomId;
        var isHosted = false;

        if (subRoomId == null)
        {
            var subRoomInfo = subRoomManager.HostSubRoom(roomId, senderParicipantInfo, SubRoomPurpose.Activity, session.MaximumParticipants);
            isHosted = true;
            subRoomId = subRoomInfo.Id;
        }

        var processContext = new ProcessContext(roomId, subRoomId.Value, SessionProcessRole.Server);

        var hostResult = await sessionProcessManager.StartAsync(session,
            processContext, context.CancellationToken).ConfigureAwait(false);

        if (hostResult == false)
        {
            subRoomManager.TryStopSubRoom(roomId, subRoomId.Value, message.SenderId);
            return HandlerResult.WithErrorHandled("У хоста повреждёна или утеряна программа сервера");
        }

        if (isHosted)
        {
            var hostReadyMessage = new SessionReadyMessage()
            {
                SubRoomId = subRoomId.Value,
                CurrentParticipantAmount = 1,
                Session = session,
                Sender = senderParicipantInfo,
                SenderId = message.SenderId,
                ThumbnailData = sessionScanner.LoadThumbnail(session.SessionId)
            };

            await sessionProcessBridge.SendIpcMessage(new ParticipantConnectedMessage(senderParicipantInfo.Id.ToString(), senderParicipantInfo.Name),
                processContext, message.SenderId, context.CancellationToken).ConfigureAwait(false);

            await messageRouter.RouteAsync(hostReadyMessage, roomId, message.SenderId, context.CancellationToken).ConfigureAwait(false);

            if (context.SelfId == message.SenderId)
            {
                await messageRouter.RouteAsync(new SessionJoinResponseMessage()
                {
                    NeedToAnnounce = false,
                    SessionId = session.SessionId,
                    SubRoomId = subRoomId.Value,
                }, context.RoomContext.RoomId, context.SelfId, context.CancellationToken).ConfigureAwait(false);

                return HandlerResult.Success();
            }
            else
            {
                // sending him ready as he didnt received by sender filtering
                await messageSender.SendAsync(hostReadyMessage, roomId, context.ConnectionId, context.CancellationToken).ConfigureAwait(false);

                return HandlerResult.WithResponse(new SessionJoinResponseMessage()
                {
                    NeedToAnnounce = false,
                    SessionId = session.SessionId,
                    SubRoomId = subRoomId.Value,
                });
            }
        }

        if (context.SelfId == message.SenderId)
        {
            await messageRouter.RouteAsync(new SessionJoinResponseMessage()
            {
                NeedToAnnounce = true,
                SessionId = session.SessionId,
                SubRoomId = subRoomId.Value,
            }, roomId, context.SelfId, context.CancellationToken).ConfigureAwait(false);
        }
        else
        {
            await messageSender.SendAsync(new SessionJoinResponseMessage()
            {
                NeedToAnnounce = true,
                SessionId = session.SessionId,
                SubRoomId = subRoomId.Value,
            }, roomId, context.RoomContext.Connections.GetConnectionIdFromParticipantId(message.SenderId), context.CancellationToken).ConfigureAwait(false);
        }

        return HandlerResult.Success();
    }
}
