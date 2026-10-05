using MIN.Core.Events.Contracts.Interfaces;
using MIN.Core.Events.Events;
using MIN.Core.Handlers.Contracts.Base;
using MIN.Core.Handlers.Contracts.Exceptions;
using MIN.Core.Handlers.Contracts.Models;
using MIN.Core.Messaging.Contracts;
using MIN.Core.Messaging.Contracts.Extensions;
using MIN.Core.Messaging.Contracts.Interfaces;
using MIN.Core.Messaging.Stateless.RoomRelated.Messages;
using MIN.Core.Messaging.Stateless.RoomRelated.Sync;
using MIN.Core.Services.Contracts.Interfaces.Messaging;
using MIN.Core.Stores.Contracts.Interfaces;
using MIN.Helpers.Contracts.Interfaces;

namespace MIN.Core.Handlers.Handlers;

internal sealed class RoomSyncHandler : BaseHandler
{
    private readonly IRoomStore roomStore;
    private readonly IMessageRouter messageRouter;
    private readonly IEventBus eventBus;

    public RoomSyncHandler(IRoomStore roomStore,
        IMessageRouter messageRouter,
        IEventBus eventBus,
        ILoggerProvider logger) : base(logger)
    {
        this.roomStore = roomStore;
        this.messageRouter = messageRouter;
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

                var visible = context.RoomContext.Messages.GetHistory().SanitizeMessagesForParticipant(message.SenderId);

                return HandlerResult.WithResponse(new RoomSyncResponseMessage()
                {
                    MissedMessages = context.RoomContext.Messages
                        .GetMessagesNewerThan(syncRequest.MessagesAfterTimestamp, syncRequest.MessagesAfterMessageId)
                        .SanitizeMessagesForParticipant(message.SenderId).ToList(),
                    UpdatedMessages =
                        visible.Where(m => m is IUpdateableMessage updateable && updateable.UpdatedAt >= syncRequest.MessagesAfterTimestamp).ToList(),
                    ExistingMessageIds = visible.Select(x => x.Id).Distinct().ToList()
                });

            case RoomSyncResponseMessage syncResponse:
                var missedMessages = syncResponse.MissedMessages;
                foreach (var roomMessage in missedMessages)
                {
                    context.RoomContext.Messages.AddMessage(roomMessage);
                }

                var deletedMessages = context.RoomContext.Messages.GetHistory()
                    .Where(x => !syncResponse.ExistingMessageIds.Contains(x.Id)).Select(x => x.Id).ToList();

                foreach (var deletedMessageId in deletedMessages)
                {
                    context.RoomContext.Messages.RemoveMessage(deletedMessageId);
                    await messageRouter.PublishLocally(new MessageDeleteMessage
                    {
                        MessageIdToDelete = deletedMessageId,
                    }, roomId, context.Role, cancellationToken: context.CancellationToken);
                }

                foreach (var updatedMessage in syncResponse.UpdatedMessages)
                {
                    await messageRouter.PublishLocally(new MessageUpdateMessage
                    {
                        MessageIdToEdit = updatedMessage.Id,
                        NewMessage = updatedMessage
                    }, roomId, context.Role, cancellationToken: context.CancellationToken);
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
