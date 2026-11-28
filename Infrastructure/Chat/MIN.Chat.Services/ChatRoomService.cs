using MIN.Chat.Services.Contracts.Interfaces;
using MIN.Core.Entities.Contracts.Enums;
using MIN.Core.Entities.Contracts.Extensions;
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
    private readonly IBanService banService;
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
        IBanService banService,
        IDiscoveryService discoveryService,
        IIdentityService identityService)
    {
        this.messageRouter = messageRouter;
        this.registry = registry;
        this.lifecycleManager = lifecycleManager;
        this.roomFactory = roomFactory;
        this.networkErrorHandler = networkErrorHandler;
        this.banService = banService;
        this.discoveryService = discoveryService;
        this.identityService = identityService;
    }

    async Task IChatRoomService.KickParticipantAsync(Guid roomId, Guid participantId, string reason, CancellationToken cancellationToken)
    {
        if (!registry.IsHosting(roomId))
        {
            throw new InvalidOperationException("Ты не являешься хостом для этой комнаты");
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

    async Task IChatRoomService.SendUpdatedRoomInfoAsync(RoomInfo updatedRoomInfo, IEnumerable<IEndpoint> endpoints, CancellationToken cancellationToken)
    {
        var roomId = updatedRoomInfo.Id;

        if (!registry.IsHosting(roomId))
        {
            throw new InvalidOperationException("Ты не являешься хостом для этой комнаты");
        }

        await messageRouter.RouteAsync(new RoomInfoUpdatedMessage
        {
            Room = updatedRoomInfo,
            Endpoints = endpoints
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

    async Task IChatRoomService.BanParticipantAsync(Guid roomId, Guid participantId, string participantName, string reason, BanDuration duration, bool alsoBanIp, CancellationToken cancellationToken)
    {
        if (!registry.IsHosting(roomId))
        {
            throw new InvalidOperationException("Ты не являешься хостом для этой комнаты");
        }

        var context = roomFactory.GetOrCreateContext(roomId);

        var isConnected = context.Connections.TryGetConnectionIdFromParticipantId(participantId, out var connectionId);

        string? ip = null;
        if (isConnected && context.Connections.TryGetConnectionIp(connectionId, out var capturedIp))
        {
            ip = capturedIp;
        }

        var ban = banService.Ban(roomId, participantId, participantName, reason, duration.ToUnbanDate(), ip, alsoBanIp && ip != null);

        if (!isConnected)
        {
            await lifecycleManager.KickClientAsync(roomId, participantId, DisconnectReason.Kick, ban.GetBanMessage());
            return;
        }

        await networkErrorHandler.SendErrorAsync(ban.GetBanMessage(), participantId, roomId, critical: true);
    }

    Task IChatRoomService.UnbanParticipantAsync(Guid roomId, Guid participantId, CancellationToken cancellationToken)
    {
        banService.Unban(roomId, participantId);
        return Task.CompletedTask;
    }

    Task<BanInfo?> IChatRoomService.UpdateBanAsync(Guid roomId, Guid participantId, string reason, BanDuration duration, bool alsoBanIp, CancellationToken cancellationToken)
        => Task.FromResult(banService.UpdateBan(roomId, participantId, reason, duration.ToUnbanDate(), alsoBanIp));

    bool IChatRoomService.ParticipantIpAddressIsKnown(Guid roomId, Guid participantId)
    {
        var context = roomFactory.GetOrCreateContext(roomId);
        return context.Connections.TryGetConnectionIdFromParticipantId(participantId, out var connectionId)
               && context.Connections.TryGetConnectionIp(connectionId, out _);
    }

    Task<IReadOnlyList<BanInfo>> IChatRoomService.GetBannedParticipantsAsync(Guid roomId, CancellationToken cancellationToken)
        => Task.FromResult(banService.GetBannedParticipants(roomId));
}
