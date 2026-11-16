using MIN.Core.Entities.Contracts.Enums;
using MIN.Core.Entities.Contracts.Extensions;
using MIN.Core.Handlers.Contracts.Base;
using MIN.Core.Handlers.Contracts.Models;
using MIN.Core.Messaging.Contracts;
using MIN.Core.Messaging.Contracts.Interfaces;
using MIN.Core.Services.Contracts.Interfaces.Messaging;
using MIN.Core.Stores.Contracts.Enums;
using MIN.Helpers.Contracts.Interfaces;
using MIN.ScreenDemo.Events;
using MIN.ScreenDemo.Messaging;
using MIN.ScreenDemo.Services.Contracts.Interfaces;
using MIN.ScreenDemo.Services.Contracts.Models;

namespace MIN.ScreenDemo.Handlers;

internal sealed class ScreenDemoStartHandler : BaseHandler
{
    private readonly IMessageSender messageSender;
    private readonly IMessageRouter messageRouter;
    private readonly IScreenDemoStateService screenDemoStateService;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="ScreenDemoStartHandler"/>
    /// </summary>
    public ScreenDemoStartHandler(IMessageSender messageSender,
        IMessageRouter messageRouter,
        IScreenDemoStateService screenDemoStateService,
        ILoggerProvider logger) : base(logger)
    {
        this.messageSender = messageSender;
        this.messageRouter = messageRouter;
        this.screenDemoStateService = screenDemoStateService;
    }

    public override IEnumerable<MessageTypeTag> HandledTypes => [MessageTypeTag.ScreenDemoStartRequest];

    protected override async Task<HandlerResult> HandleAsync(IMessage message, MessageContext context)
    {
        if (context.Role != Role.Host)
        {
            return HandlerResult.Failure($"Получил сообщение {message.GetType()} в {nameof(ScreenDemoStartHandler)} как {context.Role}, хотя не должен был", stopPropagation: false);
        }

        if (!context.RoomContext.Participants.TryGetParticipantById(message.SenderId, out var sender))
        {
            return HandlerResult.Failure("Получил сообщение от неизвестного отправителя", stopPropagation: false, critical: true);
        }

        // Демонстрация возможна только во время звонка (как в Discord)
        var voiceSubRoom = context.RoomContext.SubRooms.GetRoomSubRooms()
            .FirstOrDefault(x => x.Purpose == SubRoomPurpose.Voice && x.IsActive);

        if (voiceSubRoom == null || voiceSubRoom.Participants.All(p => p.Id != message.SenderId))
        {
            return HandlerResult.WithErrorHandled("Демонстрация экрана доступна только во время звонка");
        }

        var senderParticipantInfo = sender!.ToParticipantInfo();

        // Демонстрирующий не является зрителем своей демонстрации
        var subRoomInfo = context.RoomContext.SubRooms.HostSubRoom(senderParticipantInfo, SubRoomPurpose.ScreenDemo, includeCreator: false);
        var subRoomId = subRoomInfo.Id;

        var screenDemoStartedMessage = new ScreenDemoStartedMessage()
        {
            SubRoomId = subRoomId,
            Sharer = senderParticipantInfo,
            SenderId = message.SenderId,
        };

        await messageRouter.RouteAsync(screenDemoStartedMessage, context.RoomContext.RoomId, message.SenderId, context.CancellationToken);

        screenDemoStateService.AddDemo(new ScreenDemoSessionInfo(
            context.RoomContext.RoomId,
            subRoomId,
            senderParticipantInfo,
            []));

        if (context.SelfId == message.SenderId)
        {
            return HandlerResult.WithEvent(new ScreenDemoEstablishedEvent()
            {
                RoomId = context.RoomContext.RoomId,
                SubRoomId = subRoomId,
                SharerId = context.SelfId,
            });
        }
        else
        {
            // Отправляем напрямую, т.к. Широковещательная отправка фильтрует отправителя
            await messageSender.SendAsync(screenDemoStartedMessage, context.RoomContext.RoomId, context.ConnectionId, context.CancellationToken);

            return HandlerResult.WithResponse(new ScreenDemoEstablishedMessage()
            {
                NeedToAnnounce = false,
                SubRoomId = subRoomId,
                SharerId = message.SenderId,
            });
        }
    }
}
