using MIN.Chat.Services.Contracts.Interfaces;
using MIN.Core.Entities.Contracts.Models;
using MIN.Core.Identity.Contracts.Interfaces;
using MIN.Core.Messaging.Stateless.RoomRelated.History;
using MIN.Core.Messaging.Stateless.RoomRelated.RoomInfo;
using MIN.Core.Services.Contracts.Interfaces.Lifecycle;
using MIN.Core.Services.Contracts.Interfaces.Messaging;
using MIN.Core.Services.Contracts.Interfaces.Moderation;
using MIN.Core.Stores.Contracts.Constants;
using MIN.Core.Stores.Contracts.Interfaces;
using MIN.Core.Stores.Contracts.Registries.Interfaces;
using MIN.Core.Transport.Contracts.Enum;
using MIN.Core.Transport.Contracts.Interfaces;
using MIN.Core.Transport.Contracts.Models;
using MIN.Discovery.Services.Contracts.Interfaces;

namespace MIN.Chat.Services;

/// <inheritdoc cref="IChatRoomService"/>
public sealed class ChatRoomService : IChatRoomService
{
    private readonly IMessageRouter messageRouter;
    private readonly IRoomConnectionRegistry registry;
    private readonly IRoomLifecycleManager lifecycleManager;
    private readonly IRoomFactory roomFactory;
    private readonly INetworkErrorHandler networkErrorHandler;
    private readonly IDiscoveryService discoveryService;
    private readonly IIdentityService identityService;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="ChatRoomService"/>
    /// </summary>
    public ChatRoomService(IMessageRouter messageRouter,
        IRoomConnectionRegistry registry,
        IRoomLifecycleManager lifecycleManager,
        IRoomFactory roomFactory,
        INetworkErrorHandler networkErrorHandler,
        IDiscoveryService discoveryService,
        IIdentityService identityService)
    {
        this.messageRouter = messageRouter;
        this.registry = registry;
        this.lifecycleManager = lifecycleManager;
        this.roomFactory = roomFactory;
        this.networkErrorHandler = networkErrorHandler;
        this.discoveryService = discoveryService;
        this.identityService = identityService;
    }

    async Task IChatRoomService.KickParticipantAsync(Guid roomId, Guid participantId, string reason, CancellationToken cancellationToken)
    {
        if (!registry.IsHosting(roomId))
        {
            throw new InvalidOperationException("Ты не являешся хостом для этой комнаты");
        }

        var context = roomFactory.GetOrCreateContext(roomId);

        if (!context.Connections.TryGetConnectionIdFromParticipantId(participantId, out _))
        {
            await lifecycleManager.KickClientAsync(roomId, participantId, DisconnectReason.Kick, reason);
            return;
        }

        await networkErrorHandler.SendErrorAsync(reason, participantId, roomId, critical: true);
    }

    async Task IChatRoomService.SendChatHistoryRequest(Guid roomId, DateTime? oldestTimestamp, Guid? oldestMessageId, CancellationToken cancellationToken)
        => await messageRouter.RouteAsync(new ChatHistoryRequestMessage
        {
            OldestTimestamp = oldestTimestamp,
            OldestMessageId = oldestMessageId,
            PageSize = StoreConstants.MessagesPageSize,
        }, roomId, identityService.SelfParticipant.Id, cancellationToken);

    async Task IChatRoomService.SendUpdatedRoomInfoAsync(RoomInfo updatedRoomInfo, CancellationToken cancellationToken)
    {
        var roomId = updatedRoomInfo.Id;

        if (!registry.IsHosting(roomId))
        {
            throw new InvalidOperationException("Ты не являешся хостом для этой комнаты");
        }

        await messageRouter.RouteAsync(new RoomInfoUpdatedMessage
        {
            Room = updatedRoomInfo
        }, roomId, identityService.SelfParticipant.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task ManageDiscoveryOutOfSettings(RoomInfo room, IEnumerable<IEndpoint> endpoints, NetworkOptions newNetworkOptions, NetworkOptions? oldNetworkOptions, CancellationToken cancellationToken)
    {
        // ON
        if ((oldNetworkOptions == null && newNetworkOptions.EnableLocalDiscovery)
            || (oldNetworkOptions.HasValue && newNetworkOptions.EnableLocalDiscovery && !oldNetworkOptions.Value.EnableLocalDiscovery))
        {
            await discoveryService.StartDiscoveryAsync(room, endpoints, cancellationToken);
        }
        // OFF
        else if (oldNetworkOptions.HasValue && !newNetworkOptions.EnableLocalDiscovery && oldNetworkOptions.Value.EnableLocalDiscovery)
        {
            await discoveryService.StopDiscoveryAsync(room.Id);
        }

        // TODO: Make discovery type of web and lan
    }

    async Task IChatRoomService.UpdateNetworkOutOfSettings(RoomInfo room, IEnumerable<IEndpoint> endpoints, NetworkOptions newNetworkOptions, NetworkOptions? oldNetworkOptions, CancellationToken cancellationToken)
    {
        await ManageDiscoveryOutOfSettings(room, endpoints, newNetworkOptions, oldNetworkOptions, cancellationToken);
        await lifecycleManager.UpdateNetworkOptions(room.Id, newNetworkOptions, cancellationToken);
    }
}
