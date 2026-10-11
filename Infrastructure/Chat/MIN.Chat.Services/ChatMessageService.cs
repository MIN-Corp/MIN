using MIN.Chat.Services.Contracts.Interfaces;
using MIN.Core.Entities.Contracts.Enums;
using MIN.Core.Identity.Contracts.Interfaces;
using MIN.Core.Messaging.Contracts.Interfaces;
using MIN.Core.Messaging.Stateless.RoomRelated.History;
using MIN.Core.Messaging.Stateless.RoomRelated.Messages;
using MIN.Core.Services.Contracts.Interfaces.Messaging;
using MIN.Core.Stores.Contracts.Interfaces;
using MIN.Core.Stores.Contracts.Registries.Interfaces;

namespace MIN.Chat.Services;

/// <inheritdoc cref="IChatMessageService"/>
public sealed class ChatMessageService : IChatMessageService
{
    private readonly IRoomFactory roomFactory;
    private readonly IRoomConnectionRegistry registry;
    private readonly IMessageRouter messageRouter;
    private readonly IIdentityService identityService;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="ChatMessageService"/>
    /// </summary>
    public ChatMessageService(IRoomFactory roomFactory,
        IRoomConnectionRegistry registry,
        IMessageRouter messageRouter,
        IIdentityService identityService)
    {
        this.roomFactory = roomFactory;
        this.registry = registry;
        this.messageRouter = messageRouter;
        this.identityService = identityService;
    }

    async Task IChatMessageService.EditTextMessageAsync(Guid roomId, Guid messageId, string newContent, CancellationToken cancellationToken)
    {
        var context = roomFactory.GetRoomContext(roomId)
            ?? throw new InvalidOperationException("Комната не нашлась");

        var existing = context.Messages.GetMessageById(messageId)
            ?? throw new InvalidOperationException("Сообщение не найдено для редактирования");

        if (existing is IContentEditable contentEditable)
        {
            contentEditable.Content = newContent;
        }

        await messageRouter.RouteAsync(new MessageUpdateMessage
        {
            MessageIdToEdit = messageId,
            NewMessage = existing,
        }, roomId, identityService.SelfParticipant.Id, cancellationToken);
    }

    async Task IChatMessageService.DeleteMessageAsync(Guid roomId, Guid messageId, CancellationToken cancellationToken)
        => await messageRouter.RouteAsync(new MessageDeleteMessage
        {
            MessageIdToDelete = messageId,
        }, roomId, identityService.SelfParticipant.Id, cancellationToken);

    async Task IChatMessageService.ClearMessageHistoryAsync(Guid roomId, CancellationToken cancellationToken)
    {
        var context = roomFactory.GetRoomContext(roomId)
            ?? throw new InvalidOperationException("Комната не нашлась");

        var firstMessage = context.Messages.GetFirstMessage()
            ?? throw new InvalidOperationException("История уже очищена");

        if (registry.IsHosting(roomId))
        {
            await messageRouter.RouteAsync(new ChatHistoryClearMessage
            {
                From = firstMessage.Timestamp,
                UpTo = DateTime.Now,
            }, roomId, identityService.SelfParticipant.Id, cancellationToken);
        }
        else
        {
            await messageRouter.PublishLocally(new ChatHistoryClearMessage
            {
                From = firstMessage.Timestamp,
                UpTo = DateTime.Now,
            }, roomId, Role.Client, null, cancellationToken);
        }
    }
}
