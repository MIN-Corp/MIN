using MIN.Core.Entities.Contracts.Models;
using MIN.Core.Events.Contracts.Interfaces;
using MIN.Core.Events.Events;
using MIN.Core.Handlers.Contracts.Base;
using MIN.Core.Handlers.Contracts.Exceptions;
using MIN.Core.Handlers.Contracts.Models;
using MIN.Core.Messaging.Contracts;
using MIN.Core.Messaging.Contracts.Interfaces;
using MIN.Core.Messaging.Stateless.RoomRelated.Leaving;
using MIN.Core.Messaging.Stateless.RoomRelated.RoomInfo;
using MIN.Core.Stores.Contracts.Interfaces;
using MIN.Helpers.Contracts.Interfaces;

namespace MIN.Core.Handlers.Handlers;

internal sealed class RoomLeavingHandler : BaseHandler
{
    private readonly IRoomStore roomStore;
    private readonly IEventBus eventBus;

    public RoomLeavingHandler(IRoomStore roomStore,
        IEventBus eventBus,
        ILoggerProvider logger) : base(logger)
    {
        this.roomStore = roomStore;
        this.eventBus = eventBus;
    }

    public override IEnumerable<MessageTypeTag> HandledTypes
        => [MessageTypeTag.RoomLeave, MessageTypeTag.RoomLeaveAck];

    protected override async Task<HandlerResult> HandleAsync(IMessage message, MessageContext context)
    {
        var roomId = context.RoomContext.RoomId;

        switch (message)
        {
            case RoomLeaveMessage _:
                LogInfo($"Отправляю информацию о комнате с id {roomId}");
                return HandlerResult.WithResponse(new RoomInfoResponseMessage()
                {
                    Room = roomStore.GetRoomFor(message.SenderId, roomId),
                });

            case RoomInfoResponseMessage roomInfoResponse:
                roomStore.Register(roomInfoResponse.Room);

                var history = roomInfoResponse.Room.ChatHistory;
                foreach (var roomMessage in history)
                {
                    context.RoomContext.Messages.AddMessage(roomMessage);
                }

                LogInfo($"Получил информацию о комнате с id {roomInfoResponse.Room.Id} сообщений {roomInfoResponse.Room.TotalMessageCount}");

                await eventBus.PublishAsync(new RoomStateChangedEvent()
                {
                    Room = roomInfoResponse.Room,
                }, context.CancellationToken);

                return HandlerResult.WithEvent(new RoomJoinedEvent()
                {
                    RoomId = roomInfoResponse.Room.Id,
                    RoomInfo = new RoomInfo(roomInfoResponse.Room),
                });

            default:
                throw new HandlerTypeMismatch(this, message);
        }
    }
}
