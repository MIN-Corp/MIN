using MIN.Core.Entities.Contracts.Enums;
using MIN.Core.Events.Events;
using MIN.Core.Handlers.Contracts.Base;
using MIN.Core.Handlers.Contracts.Exceptions;
using MIN.Core.Handlers.Contracts.Models;
using MIN.Core.Messaging.Contracts;
using MIN.Core.Messaging.Contracts.Interfaces;
using MIN.Core.Messaging.Stateless.RoomRelated.Disconnect;
using MIN.Core.Services.Contracts.Interfaces.Lifecycle;
using MIN.Core.Services.Contracts.Interfaces.Messaging;
using MIN.Core.Stores.Contracts.Interfaces;
using MIN.Helpers.Contracts.Interfaces;

namespace MIN.Core.Handlers.Handlers;

internal sealed class DisconnectHandler : BaseHandler
{
    private readonly IMessageSender messageSender;
    private readonly IRoomLifecycleManager lifecycle;
    private readonly IRoomStore roomStore;

    public DisconnectHandler(IMessageSender messageSender,
        IRoomLifecycleManager lifecycle,
        IRoomStore roomStore,
        ILoggerProvider logger) : base(logger)
    {
        this.messageSender = messageSender;
        this.lifecycle = lifecycle;
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

                var isKicked = context.RoomContext.Participants.TryGetParticipantById(context.SelfId, out var self)
                    && self?.CurrentStatus == OnlineStatus.Online;

                if (isKicked)
                {
                    lifecycle.MarkRoomForDeletion(context.RoomContext.RoomId);
                }

                await messageSender.SendAsync(new DisconnectAckMessage()
                {
                    Reason = reason,
                    SenderId = context.SelfId,
                }, context.RoomContext.RoomId, context.ConnectionId, context.CancellationToken);

                var roomName = roomStore.GetRoom(context.RoomContext.RoomId).Name;
                var uiToShow = "Хост разорвал соединение" + (roomName != null ? $" для комнаты {roomName}" : string.Empty) + (reason != string.Empty ? $": {reason}" : string.Empty);
                return HandlerResult.Failure(uiToShow, stopPropagation: true, critical: true);

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
