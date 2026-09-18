using MIN.Core.Events.Contracts.Interfaces;
using MIN.Core.Events.Events;
using MIN.Core.Handlers.Contracts.Base;
using MIN.Core.Handlers.Contracts.Exceptions;
using MIN.Core.Handlers.Contracts.Models;
using MIN.Core.Messaging.Contracts;
using MIN.Core.Messaging.Contracts.Extensions;
using MIN.Core.Messaging.Contracts.Interfaces;
using MIN.Core.Messaging.Stateless.RoomRelated.Sync;
using MIN.Core.Stores.Contracts.Interfaces;
using MIN.Helpers.Contracts.Interfaces;

namespace MIN.Core.Handlers.Handlers;

internal sealed class RoomSyncHandler : BaseHandler
{
    private readonly IRoomStore roomStore;
    private readonly IEventBus eventBus;

    public RoomSyncHandler(IRoomStore roomStore,
        IEventBus eventBus,
        ILoggerProvider logger) : base(logger)
    {
        this.roomStore = roomStore;
        this.eventBus = eventBus;
    }

    public override IEnumerable<MessageTypeTag> HandledTypes
        => [MessageTypeTag.RoomSyncRequest, MessageTypeTag.RoomSyncResponse];

    protected override async Task<HandlerResult> HandleAsync(IMessage message, MessageContext context)
    {
        var roomId = context.RoomContext.RoomId;

        switch (message)
        {
            case RoomSyncRequestMessage syncRequest:
                LogInfo($"Отправляю упущенную информацию о комнате с id {roomId}");

                return HandlerResult.WithResponse(new RoomSyncResponseMessage()
                {
                    MissedMessages = context.RoomContext.Messages
                        .GetMessagesNewerThan(syncRequest.MessagesAfterTimestamp, syncRequest.MessagesAfterMessageId)
                        .SanitizeMessagesForParticipant(message.SenderId).ToList(),
                });

            case RoomSyncResponseMessage syncResponse:
                var missedMessages = syncResponse.MissedMessages;
                foreach (var roomMessage in missedMessages)
                {
                    context.RoomContext.Messages.AddMessage(roomMessage);
                }

                LogInfo($"Получил упущенную информацию о комнате с id {roomId} сообщений {syncResponse.MissedMessages.Count}");

                await eventBus.PublishAsync(new RoomSyncedEvent()
                {
                    RoomId = roomId,
                    MissedMessages = missedMessages
                }, context.CancellationToken);

                var room = roomStore.GetRoom(roomId);
                room.IsOnline = true;

                return HandlerResult.WithEvent(new RoomStateChangedEvent()
                {
                    Room = room,
                });

            default:
                throw new HandlerTypeMismatch(this, message);
        }
    }
}
