using MIN.Core.Entities.Contracts.Enums;
using MIN.Core.Events.Contracts.Interfaces;
using MIN.Core.Messaging.Contracts.Interfaces;
using MIN.Core.Services.Contracts.Events;
using MIN.Core.Services.Contracts.Interfaces.Messaging;
using MIN.Core.Stores.Contracts.Interfaces;
using MIN.Core.Stores.Contracts.Registries.Interfaces;

namespace MIN.Core.Services.Messaging;

/// <inheritdoc cref="IMessageRouter"/>
public sealed class MessageRouter : IMessageRouter
{
    private readonly IRoomConnectionRegistry registry;
    private readonly IRoomStore roomStore;
    private readonly IEventBus eventBus;
    private readonly IMessageSender messageSender;
    private readonly IRoomFactory roomFactory;

    /// <summary>
    /// Инициализирует новый экземлпяр <see cref="MessageRouter"/>
    /// </summary>
    public MessageRouter(IRoomConnectionRegistry registry,
        IRoomStore roomStore,
        IEventBus eventBus,
        IMessageSender messageSender,
        IRoomFactory roomFactory)
    {
        this.registry = registry;
        this.roomStore = roomStore;
        this.eventBus = eventBus;
        this.messageSender = messageSender;
        this.roomFactory = roomFactory;
    }

    async Task IMessageRouter.RouteAsync(IMessage message, Guid roomId, Guid senderId, CancellationToken cancellationToken, IEnumerable<Guid>? broadcastExcludeIds)
    {
        message.SenderId = senderId;

        var role = registry.GetRole(roomId);

        if (role == Role.Host)
        {
            // Server

            // If its public, dispatcher will broadcast to anyone except server and sender (cuz they already handled it)
            // Regardless of recipient - they had to put recipientId and public = false if they wanted it to be private
            // So basically dispatcher will handle all of it

            await PublishLocally(message, roomId, role, broadcastExcludeIds, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            // Client

            if (message.RequiresLocalDuplication)
            {
                await PublishLocally(message, roomId, role, null, cancellationToken).ConfigureAwait(false);
            }

            var hostId = roomStore.GetRoomHostParticipantId(roomId);
            var hostConnectionId = GetHostConnectionId(roomId, hostId);
            await messageSender.SendAsync(message, roomId, hostConnectionId, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task PublishLocally(IMessage message, Guid roomId, Role role, IEnumerable<Guid>? broadcastExcludeIds, CancellationToken cancellationToken)
        => await eventBus.PublishAsync(new LocalMessageReceivedEvent(message, roomId, role, broadcastExcludeIds), cancellationToken).ConfigureAwait(false);

    private Guid GetHostConnectionId(Guid roomId, Guid hostId)
    {
        if (!roomFactory.GetOrCreateContext(roomId).Connections.TryGetConnectionIdFromParticipantId(hostId, out var connectionId))
        {
            throw new InvalidOperationException($"Host participant {hostId} is not registered in room {roomId}");
        }
        return connectionId;
    }
}
