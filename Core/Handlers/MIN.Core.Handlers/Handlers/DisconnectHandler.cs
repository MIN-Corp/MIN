using MIN.Core.Entities.Contracts.Enums;
using MIN.Core.Events.Events;
using MIN.Core.Handlers.Contracts.Base;
using MIN.Core.Handlers.Contracts.Exceptions;
using MIN.Core.Handlers.Contracts.Models;
using MIN.Core.Messaging.Contracts;
using MIN.Core.Messaging.Contracts.Interfaces;
using MIN.Core.Messaging.Stateless.RoomRelated.Disconnect;
using MIN.Core.Services.Contracts.Interfaces.Messaging;
using MIN.Core.Stores.Contracts.Interfaces;
using MIN.Helpers.Contracts.Interfaces;

namespace MIN.Core.Handlers.Handlers;

internal sealed class DisconnectHandler : BaseHandler
{
    private readonly IMessageSender messageSender;
    private readonly IRoomStore roomStore;

    public DisconnectHandler(IMessageSender messageSender,
        IRoomStore roomStore,
        ILoggerProvider logger) : base(logger)
    {
        this.messageSender = messageSender;
        this.roomStore = roomStore;
    }

    public override IEnumerable<MessageTypeTag> HandledTypes
        => [MessageTypeTag.Disconnect, MessageTypeTag.DisconnectAck];

    protected override async Task<HandlerResult> HandleAsync(IMessage message, MessageContext context)
    {
        switch (message)
        {
            case DisconnectMessage disconnectMessage:
                var reason = disconnectMessage.Reason;
                LogInfo($"Сервер нарошно отключил меня: {reason}");

                var isParticipantKnown = context.RoomContext.Participants.TryGetParticipantById(context.SelfId, out var self);
                var isKicked = isParticipantKnown && self?.CurrentStatus == OnlineStatus.Online;

                await messageSender.SendAsync(new DisconnectAckMessage
                {
                    Reason = reason,
                    SenderId = context.SelfId,
                }, context.RoomContext.RoomId, context.ConnectionId, context.CancellationToken);

                var roomExists = roomStore.TryGetRoom(context.RoomContext.RoomId, out var currentRoom);
                var roomName = roomExists ? currentRoom!.Name : null;
                var uiToShow = "Хост разорвал соединение" + (string.IsNullOrEmpty(roomName) ? $" для комнаты {roomName}" : string.Empty) + (reason != string.Empty ? $": {reason}" : string.Empty);
                var isLiveRoom = roomExists && currentRoom!.IsOnline;
                return HandlerResult.Failure(uiToShow, stopPropagation: true, critical: true, destroy: (isKicked && isLiveRoom) || !isParticipantKnown);

            case DisconnectAckMessage _:
                return HandlerResult.WithEvent(new DisconnectAckReceived()
                {
                    ParticipantId = message.SenderId,
                    ConnectionId = context.ConnectionId,
                    RoomId = context.RoomContext.RoomId,
                });

            default:
                throw new HandlerTypeMismatch(this, message);
        }
    }
}
