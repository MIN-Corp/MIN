using MIN.Core.Entities.Contracts.Enums;
using MIN.Core.Entities.Contracts.Extensions;
using MIN.Core.Events.Contracts.Interfaces;
using MIN.Core.Handlers.Contracts.Base;
using MIN.Core.Handlers.Contracts.Models;
using MIN.Core.Handlers.Contracts.Exceptions;
using MIN.Core.Identity.Contracts.Interfaces;
using MIN.Core.Messaging.Contracts;
using MIN.Core.Messaging.Contracts.Interfaces;
using MIN.Core.Services.Contracts.Interfaces.Messaging;
using MIN.Core.Stores.Contracts.Enums;
using MIN.Helpers.Contracts.Interfaces;
using MIN.ScreenDemo.Events;
using MIN.ScreenDemo.Messaging;
using MIN.ScreenDemo.Services.Contracts.Interfaces;

namespace MIN.ScreenDemo.Handlers;

internal sealed class ScreenDemoJoinHandler : BaseHandler
{
    private readonly IEventBus eventBus;
    private readonly IMessageRouter messageRouter;
    private readonly IScreenDemoStateService screenDemoStateService;
    private readonly IIdentityService identityService;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="ScreenDemoJoinHandler"/>
    /// </summary>
    public ScreenDemoJoinHandler(IEventBus eventBus,
        IMessageRouter messageRouter,
        IScreenDemoStateService screenDemoStateService,
        IIdentityService identityService,
        ILoggerProvider logger) : base(logger)
    {
        this.eventBus = eventBus;
        this.messageRouter = messageRouter;
        this.screenDemoStateService = screenDemoStateService;
        this.identityService = identityService;
    }

    public override IEnumerable<MessageTypeTag> HandledTypes =>
        [MessageTypeTag.ScreenDemoJoinRequest, MessageTypeTag.ScreenDemoEstablished];

    protected override async Task<HandlerResult> HandleAsync(IMessage message, MessageContext context)
    {
        switch (message)
        {
            case ScreenDemoJoinRequestMessage screenDemoJoinRequestMessage:
            {
                if (!context.RoomContext.Participants.TryGetParticipantById(message.SenderId, out var sender))
                {
                    return HandlerResult.Failure("Получил сообщение от неизвестного отправителя", stopPropagation: false, critical: true);
                }

                if (context.Role != Role.Host)
                {
                    return HandlerResult.Failure($"Получил сообщение {message.GetType()} в {nameof(ScreenDemoJoinHandler)} как {context.Role}, хотя не должен был", stopPropagation: false);
                }

                var subRoomInfo = context.RoomContext.SubRooms.GetSubRoom(screenDemoJoinRequestMessage.SubRoomId);

                if (subRoomInfo == null || subRoomInfo.Purpose != SubRoomPurpose.ScreenDemo)
                {
                    return HandlerResult.WithErrorHandled("Такая демонстрация не нашлась");
                }

                if (subRoomInfo.CreatorId == message.SenderId)
                {
                    return HandlerResult.WithErrorHandled("Нельзя смотреть собственную демонстрацию");
                }

                // Смотреть демонстрацию можно только находясь в звонке
                var voiceSubRoom = context.RoomContext.SubRooms.GetRoomSubRooms()
                    .FirstOrDefault(x => x.Purpose == SubRoomPurpose.Voice && x.IsActive);

                if (voiceSubRoom == null || voiceSubRoom.Participants.All(p => p.Id != message.SenderId))
                {
                    return HandlerResult.WithErrorHandled("Смотреть демонстрацию можно только во время звонка");
                }

                var senderParticipantInfo = sender!.ToParticipantInfo();

                var joinResult = context.RoomContext.SubRooms.TryJoinSubRoom(subRoomInfo.Id, senderParticipantInfo);

                if (joinResult == SubRoomJoinOutcome.Success)
                {
                    return HandlerResult.WithResponse(new ScreenDemoEstablishedMessage()
                    {
                        NeedToAnnounce = true, SubRoomId = subRoomInfo.Id, SharerId = subRoomInfo.CreatorId,
                    });
                }

                var error = joinResult switch
                {
                    SubRoomJoinOutcome.RoomNotFound => "Комната не нашлась",
                    SubRoomJoinOutcome.SubRoomNotFound => "Нету информации о демонстрации",
                    SubRoomJoinOutcome.AlreadyJoined => "Вы уже смотрите эту демонстрацию",
                    SubRoomJoinOutcome.MaximumParticipants => "Достигнут максимум зрителей",
                    _ => "Не удалось войти"
                };

                return HandlerResult.WithErrorHandled(error);

            }

            case ScreenDemoEstablishedMessage screenDemoEstablishedMessage:
            {
                var roomId = context.RoomContext.RoomId;

                // Зритель регистрирует себя, чтобы DataHandler начал принимать кадры
                if (screenDemoEstablishedMessage.NeedToAnnounce)
                {
                    screenDemoStateService.AddViewer(roomId, screenDemoEstablishedMessage.SubRoomId, context.SelfId);
                }

                await eventBus.PublishAsync(new ScreenDemoEstablishedEvent()
                {
                    RoomId = roomId,
                    SubRoomId = screenDemoEstablishedMessage.SubRoomId,
                    SharerId = screenDemoEstablishedMessage.SharerId,
                });

                if (!screenDemoEstablishedMessage.NeedToAnnounce)
                {
                    return HandlerResult.Success();
                }

                var selfParticipant = identityService.SelfParticipant.ToParticipantInfo();

                var viewerJoinedMessage = new ScreenDemoViewerJoinedMessage()
                {
                    SubRoomId = screenDemoEstablishedMessage.SubRoomId,
                    Participant = selfParticipant,
                };

                await messageRouter.RouteAsync(viewerJoinedMessage, roomId, selfParticipant.Id, context.CancellationToken);

                return HandlerResult.Success();
            }

            default:
                throw new HandlerTypeMismatch(this, message);
        }
    }
}
