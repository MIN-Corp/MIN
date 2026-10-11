using MIN.Chat.Messaging;
using MIN.Chat.Services.Contracts.Interfaces;
using MIN.Common.Core.Contracts.Interfaces;
using MIN.Core.Entities.Contracts.Extensions;
using MIN.Core.Identity.Contracts.Interfaces;
using MIN.Core.Services.Contracts.Interfaces.Messaging;
using MIN.Core.Stores.Contracts.Interfaces;

namespace MIN.Chat.Services;

/// <inheritdoc cref="IChatTextService"/>
public sealed class ChatTextService : IChatTextService
{
    private readonly IMessageRouter messageRouter;
    private readonly IRoomFactory roomFactory;
    private readonly IIdentityService identityService;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="ChatTextService"/>
    /// </summary>
    public ChatTextService(IMessageRouter messageRouter,
        IRoomFactory roomFactory,
        IIdentityService identityService)
    {
        this.messageRouter = messageRouter;
        this.roomFactory = roomFactory;
        this.identityService = identityService;
    }

    async Task IChatTextService.SendTextMessageAsync(Guid roomId, string content, Guid? recipientId, Guid? replyToId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("Сообщение не должно быть пустым", nameof(content));
        }

        var replyToMessage = roomFactory.GetOrCreateContext(roomId).Messages.GetMessageById(replyToId ?? Guid.Empty);
        var replyToDescription = (replyToMessage as IDescribable)?.GetDescription();

        var message = new ChatTextMessage
        {
            SenderName = identityService.SelfParticipant.ToParticipantInfo().Name,
            Content = content,
            RecipientId = recipientId,
            ReplyToMessageId = replyToId,
            ReplyToMessageDescription = replyToDescription,
        };

        await messageRouter.RouteAsync(message, roomId, identityService.SelfParticipant.Id, cancellationToken);
    }
}
