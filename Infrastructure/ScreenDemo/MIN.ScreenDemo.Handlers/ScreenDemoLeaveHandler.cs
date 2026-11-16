using MIN.Core.Entities.Contracts.Enums;
using MIN.Core.Entities.Contracts.Extensions;
using MIN.Core.Handlers.Contracts.Base;
using MIN.Core.Handlers.Contracts.Models;
using MIN.Core.Messaging.Contracts;
using MIN.Core.Messaging.Contracts.Interfaces;
using MIN.Core.Services.Contracts.Interfaces.Messaging;
using MIN.Helpers.Contracts.Interfaces;
using MIN.ScreenDemo.Messaging;
using MIN.ScreenDemo.Services.Contracts.Interfaces;

namespace MIN.ScreenDemo.Handlers;

internal sealed class ScreenDemoLeaveHandler : BaseHandler
{
    private readonly IMessageRouter messageRouter;
    private readonly IScreenDemoStateService screenDemoStateService;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="ScreenDemoLeaveHandler"/>
    /// </summary>
    public ScreenDemoLeaveHandler(IMessageRouter messageRouter,
        IScreenDemoStateService screenDemoStateService,
        ILoggerProvider logger) : base(logger)
    {
        this.messageRouter = messageRouter;
        this.screenDemoStateService = screenDemoStateService;
    }

    public override IEnumerable<MessageTypeTag> HandledTypes => [MessageTypeTag.ScreenDemoLeave];

    protected override async Task<HandlerResult> HandleAsync(IMessage message, MessageContext context)
    {
        var screenDemoLeaveMessage = (ScreenDemoLeaveMessage)message;

        if (context.Role != Role.Host)
        {
            return HandlerResult.Failure($"Получил сообщение {message.GetType()} в {nameof(ScreenDemoLeaveHandler)} как {context.Role}, хотя не должен был", stopPropagation: false);
        }

        if (!context.RoomContext.Participants.TryGetParticipantById(message.SenderId, out var sender))
        {
            return HandlerResult.Failure("Получил сообщение от неизвестного отправителя", stopPropagation: false, critical: true);
        }

        if (context.RoomContext.SubRooms.GetSubRoom(screenDemoLeaveMessage.SubRoomId) == null)
        {
            return HandlerResult.Failure("Клиент отправил запрос на выход из неизвестной демонстрации", stopPropagation: true);
        }

        var roomId = context.RoomContext.RoomId;

        await messageRouter.RouteAsync(new ScreenDemoViewerLeftMessage()
        {
            SubRoomId = screenDemoLeaveMessage.SubRoomId,
            Participant = sender!.ToParticipantInfo(),
        }, roomId, context.SelfId, context.CancellationToken);

        // В отличие от звонка, демонстрация не завершается, когда уходят все зрители —
        // демонстрирующий продолжает видеть свой предпросмотр и ждёт новых зрителей
        context.RoomContext.SubRooms.LeaveSubRoom(screenDemoLeaveMessage.SubRoomId, message.SenderId);

        screenDemoStateService.RemoveViewer(roomId, screenDemoLeaveMessage.SubRoomId, message.SenderId);

        return HandlerResult.Success();
    }
}
