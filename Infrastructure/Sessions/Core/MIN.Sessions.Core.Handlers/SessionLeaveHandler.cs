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

namespace MIN.Sessions.Core.Handlers;

internal sealed class SessionLeaveHandler : BaseHandler
{
    private readonly IMessageRouter messageRouter;
    private readonly IEventBus eventBus;

    /// <summary>
    /// Инициализирует новый экземлпяр <see cref="SessionLeaveHandler"/>
    /// </summary>
    public SessionLeaveHandler(IMessageRouter messageRouter,
        IEventBus eventBus,
        ILoggerProvider logger) : base(logger)
    {
        this.messageRouter = messageRouter;
        this.eventBus = eventBus;
    }

    public override IEnumerable<MessageTypeTag> HandledTypes => [MessageTypeTag.SessionLeave];

    protected override async Task<HandlerResult> HandleAsync(IMessage message, MessageContext context)
    {
        var sessionLeaveMessage = (SessionLeaveMessage)message;

        if (!context.RoomContext.Participants.TryGetParticipantById(message.SenderId, out var sender))
        {
            return HandlerResult.Failure("Получил сообщение от неизвестного отправителя", stopPropagation: false, critical: true);
        }

        var roomId = context.RoomContext.RoomId;

        if (context.RoomContext.SubRooms.GetSubRoom(sessionLeaveMessage.SubRoomId) == null)
        {
            return HandlerResult.Failure("Клиент отправил запрос на выход из неизвестной сессии", stopPropagation: true);
        }

        var isLast = false;

        if (!context.RoomContext.SubRooms.LeaveSubRoom(sessionLeaveMessage.SubRoomId, message.SenderId))
        {
            await eventBus.PublishAsync(new SessionDeactivatedEvent()
            {
                RoomId = roomId,
                SubRoomId = sessionLeaveMessage.SubRoomId,
            });

            isLast = true;
        }

        await messageRouter.RouteAsync(new SessionParticipantLeftMessage()
        {
            SubRoomId = sessionLeaveMessage.SubRoomId,
            Participant = sender!.ToParticipantInfo(),
            IsLast = isLast
        }, roomId, context.SelfId, context.CancellationToken);

        return HandlerResult.Success();
    }
}
