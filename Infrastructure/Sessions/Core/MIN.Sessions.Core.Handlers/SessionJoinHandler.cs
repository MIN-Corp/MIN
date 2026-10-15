using MIN.Core.Entities.Contracts.Enums;
using MIN.Core.Entities.Contracts.Extensions;
using MIN.Core.Handlers.Contracts.Base;
using MIN.Core.Handlers.Contracts.Exceptions;
using MIN.Core.Handlers.Contracts.Models;
using MIN.Core.Identity.Contracts.Interfaces;
using MIN.Core.Messaging.Contracts;
using MIN.Core.Messaging.Contracts.Interfaces;
using MIN.Core.Services.Contracts.Interfaces.Messaging;
using MIN.Helpers.Contracts.Interfaces;
using MIN.Sessions.Core.Messaging.OutOfSubRoom;
using MIN.Sessions.Core.Services.Contracts.Interfaces;
using MIN.Sessions.Core.Transport.Contracts.Enums;
using MIN.Sessions.Core.Transport.Contracts.Models;

namespace MIN.Sessions.Core.Handlers;

internal sealed class SessionJoinHandler : BaseHandler
{
    private readonly ISessionInstanceManager sessionInstanceManager;
    private readonly IMessageRouter messageRouter;
    private readonly ISessionScanner sessionScanner;
    private readonly IIdentityService identityService;

    public SessionJoinHandler(ISessionInstanceManager sessionInstanceManager,
        IMessageRouter messageRouter,
        ISessionScanner sessionScanner,
        IIdentityService identityService,
        ILoggerProvider logger) : base(logger)
    {
        this.sessionInstanceManager = sessionInstanceManager;
        this.messageRouter = messageRouter;
        this.sessionScanner = sessionScanner;
        this.identityService = identityService;
    }

    public override IEnumerable<MessageTypeTag> HandledTypes
        => [MessageTypeTag.SessionJoinRequest, MessageTypeTag.SessionJoinResponse, MessageTypeTag.SessionJoinFailed];

    protected override async Task<HandlerResult> HandleAsync(IMessage message, MessageContext context)
    {
        var roomId = context.RoomContext.RoomId;

        switch (message)
        {
            case SessionJoinRequestMessage sessionJoinRequestMessage:
                if (context.Role != Role.Host)
                {
                    return HandlerResult.Failure($"Получил сообщение {message.GetType()} в {nameof(SessionJoinHandler)} как {context.Role}, хотя не должен был",
                        stopPropagation: false);
                }

                if (!context.RoomContext.Participants.TryGetParticipantById(message.SenderId, out var sender))
                {
                    return HandlerResult.Failure("Получил сообщение от неизвестного отправителя", stopPropagation: false, critical: true);
                }

                var subRoomInfo = context.RoomContext.SubRooms.GetSubRoom(sessionJoinRequestMessage.SubRoomId);

                if (subRoomInfo == null)
                {
                    return HandlerResult.WithErrorHandled("Такая подкомната не нашлась");
                }

                var session = sessionScanner.GetSessionById(sessionJoinRequestMessage.SessionId);

                if (session == null)
                {
                    return HandlerResult.WithErrorHandled("У хоста не установлена программа сервера этой сессии");
                }

                if (session.Version != sessionJoinRequestMessage.SessionVersion)
                {
                    var clientOnOlderVersion = session.Version > sessionJoinRequestMessage.SessionVersion ? "Вы" : "Хост";
                    return HandlerResult.WithErrorHandled($"{clientOnOlderVersion} на устаревшей версии сессии: " +
                        $"\nВаша версия сессии - {sessionJoinRequestMessage.SessionVersion}" +
                        $"\nВерсия сессии хоста комнаты - {session.Version}");
                }

                if (session.MaximumParticipants.HasValue && subRoomInfo.Participants.Count >= session.MaximumParticipants)
                {
                    return HandlerResult.WithErrorHandled($"Сессия {session.Name} уже заполнена");
                }

                if (subRoomInfo.Participants.Any(x => x.Id == sender!.Id))
                {
                    return HandlerResult.WithErrorHandled("Вы уже учавствуете в этой сессии");
                }

                if (!subRoomInfo.IsActive)
                {
                    await messageRouter.RouteAsync(new SessionHostRequestMessage()
                    {
                        SubRoomId = subRoomInfo.Id,
                        SessionId = sessionJoinRequestMessage.SessionId,
                        SessionVersion = sessionJoinRequestMessage.SessionVersion,
                    }, context.RoomContext.RoomId, message.SenderId, context.CancellationToken).ConfigureAwait(false);

                    return HandlerResult.Success();
                }

                return HandlerResult.WithResponse(new SessionJoinResponseMessage()
                {
                    NeedToAnnounce = true,
                    SubRoomId = subRoomInfo.Id,
                    SessionId = sessionJoinRequestMessage.SessionId
                });

            case SessionJoinResponseMessage sessionJoinResponseMessage:
                var responseSession = sessionScanner.GetSessionById(sessionJoinResponseMessage.SessionId);

                if (responseSession == null)
                {
                    return HandlerResult.Failure($"У вас не установлена сессия с id {sessionJoinResponseMessage.SessionId}");
                }

                var clientResult = await sessionInstanceManager.StartAsync(responseSession,
                    new SessionInstanceContext(roomId, sessionJoinResponseMessage.SubRoomId, SessionInstanceRole.Client),
                    context.CancellationToken).ConfigureAwait(false);

                if (clientResult == null)
                {
                    return HandlerResult.Failure($"У вас повреждёна или утеряна программа для {responseSession.Name}");
                }

                if (!sessionJoinResponseMessage.NeedToAnnounce)
                {
                    return HandlerResult.Success();
                }

                var selfParticipant = identityService.SelfParticipant.ToParticipantInfo();

                var sessionParticipantJoinedMessage = new SessionParticipantJoinedMessage()
                {
                    SubRoomId = sessionJoinResponseMessage.SubRoomId,
                    Participant = selfParticipant,
                };

                await messageRouter.RouteAsync(sessionParticipantJoinedMessage, roomId, selfParticipant.Id, context.CancellationToken)
                    .ConfigureAwait(false);

                return HandlerResult.Success();

            case SessionJoinFailedMessage sessionJoinFailedMessage:
                await sessionInstanceManager.StopAsync(new SessionInstanceContext(roomId, sessionJoinFailedMessage.SubRoomId, SessionInstanceRole.Client))
                    .ConfigureAwait(false);

                return HandlerResult.Failure($"Не удалось запустить сессию: {sessionJoinFailedMessage.Message}");

            default:
                throw new HandlerTypeMismatch(this, message);
        }
    }
}
