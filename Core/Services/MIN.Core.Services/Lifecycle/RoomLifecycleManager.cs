using MIN.Common.Core.Extensions;
using MIN.Core.Cryptography.Contracts.Interfaces;
using MIN.Core.Entities;
using MIN.Core.Entities.Contracts.Enums;
using MIN.Core.Entities.Contracts.Models;
using MIN.Core.Events.Contracts.Interfaces;
using MIN.Core.Events.Events;
using MIN.Core.Identity.Contracts.Interfaces;
using MIN.Core.Protocol.Contracts.Interfaces;
using MIN.Core.Services.Contracts.Events;
using MIN.Core.Services.Contracts.Interfaces.Lifecycle;
using MIN.Core.Services.Contracts.Interfaces.Messaging;
using MIN.Core.Services.Contracts.Models;
using MIN.Core.Services.Services;
using MIN.Core.Stores.Contracts.Interfaces;
using MIN.Core.Stores.Contracts.Registries.Interfaces;
using MIN.Core.SubRooms.Contracts.Interfaces;
using MIN.Core.Transport.Contracts.Enum;
using MIN.Core.Transport.Contracts.Events;
using MIN.Core.Transport.Contracts.Interfaces;
using MIN.Core.Transport.Contracts.Models;
using MIN.Helpers.Contracts.Interfaces;

namespace MIN.Core.Services.Lifecycle;

/// <inheritdoc cref="IRoomLifecycleManager"/>
public sealed class RoomLifecycleManager : IRoomLifecycleManager
{
    private readonly ITransport transport;
    private readonly IRoomConnectionRegistry registry;
    private readonly IEventBus eventBus;
    private readonly PingService pingService;
    private readonly ClientRoomService clientService;
    private readonly HostRoomService hostService;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="RoomLifecycleManager"/>
    /// </summary>
    public RoomLifecycleManager(ITransport transport,
        IClientHandshake clientHandshake,
        IHostHandshake hostHandshake,
        IRoomStore roomStore,
        IRoomFactory roomFactory,
        IMessageSender messageSender,
        IMessageRouter messageRouter,
        IIdentityService identityService,
        IMessageEncryptor encryptor,
        IRoomConnectionRegistry registry,
        IVersionProvider versionProvider,
        ISubRoomManager subRoomManager,
        IEventBus eventBus,
        ILoggerProvider logger)
    {
        this.transport = transport;
        this.registry = registry;
        this.eventBus = eventBus;

        pingService = new PingService(eventBus, messageSender);

        clientService = new ClientRoomService(transport, clientHandshake, roomStore, roomFactory,
            messageSender, identityService, encryptor, registry, versionProvider, eventBus, logger, pingService);

        hostService = new HostRoomService(roomFactory, hostHandshake, transport, roomStore, eventBus,
            subRoomManager, registry, identityService, messageRouter, logger, pingService);

        SubscribeToEvents();
    }

    // CLIENT

    async Task<ConnectionResult> IRoomLifecycleManager.ConnectAsync(IEndpoint endpoint, CancellationToken cancellationToken)
        => await clientService.ConnectAsync(endpoint, cancellationToken);

    void IRoomLifecycleManager.MarkRoomForDeletion(Guid roomId)
        => clientService.MarkRoomForDeletion(roomId);

    async Task IRoomLifecycleManager.DisconnectAsync(Guid roomId, Guid connectionId)
        => await clientService.DisconnectAsync(roomId, connectionId);

    async Task IRoomLifecycleManager.ForgetRoomAsync(Guid roomId, Guid connectionId)
        => await clientService.ForgetRoomAsync(roomId, connectionId);

    void IRoomLifecycleManager.CompleteRoomLeaveAck(Guid roomId)
        => clientService.CompleteRoomLeaveAck(roomId);

    // HOST

    async Task<Room> IRoomLifecycleManager.StartHostingAsync(RoomInfo roomInfo, NetworkOptions networkOptions, CancellationToken cancellationToken)
        => await hostService.StartHostingAsync(roomInfo, networkOptions, cancellationToken);

    async Task<IEnumerable<IEndpoint>> IRoomLifecycleManager.UpdateNetworkOptions(Guid roomId, NetworkOptions newNetworkOptions, CancellationToken cancellationToken)
        => await hostService.UpdateNetworkOptions(roomId, newNetworkOptions, cancellationToken);

    void IRoomLifecycleManager.MarkParticipantAsLeftRoom(Guid roomId, Guid participantId)
        => hostService.MarkParticipantAsLeftRoom(roomId, participantId);

    async Task IRoomLifecycleManager.KickClientAsync(Guid roomId, Guid participantId, DisconnectReason reason)
        => await hostService.KickClientAsync(roomId, participantId, reason);

    async Task IRoomLifecycleManager.KickConnectionAsync(Guid roomId, Guid connectionId, DisconnectReason reason)
        => await hostService.KickConnectionAsync(roomId, connectionId, reason);

    async Task IRoomLifecycleManager.StopHostingAsync(Guid roomId)
       => await hostService.StopHostingAsync(roomId);

    async Task IRoomLifecycleManager.ForgetHostingAsync(Guid roomId)
       => await hostService.ForgetRoom(roomId);

    private void SubscribeToEvents()
    {
        transport.RawMessageReceived += Transport_RawMessageReceived;
        transport.ConnectionStateChanged += Transport_ConnectionStateChanged;
        pingService.OnConnectionTimeout += PingService_OnConnectionTimeout;
    }

    private async void Transport_RawMessageReceived(object? sender, RawMessageReceivedEventArgs e)
    {
        if (!clientService.TryResolveRoom(e, out var roomId) && !hostService.TryResolveRoom(e, out roomId))
        {
            return;
        }

        await eventBus.PublishAsync(new RoomRawMessageReceivedEvent()
        {
            EventArgs = new RoomRawMessageReceivedEventArgs(roomId, e)
        });
    }

    private async void Transport_ConnectionStateChanged(object? sender, ConnectionStateChangedEventArgs e)
    {
        Role role;

        if (clientService.TryResolveRoom(e, out var roomId))
        {
            role = Role.Client;
        }
        else if (hostService.TryResolveRoom(e, out roomId))
        {
            role = Role.Host;
        }
        else
        {
            return;
        }

        if (e.IsConnected)
        {
            if (role == Role.Host && !await hostService.HandleConnectionConnectedAsync(roomId, e))
            {
                return;
            }

            await PublishConnectionStatusAsync(roomId, e, needToDisconnect: false);
            return;
        }

        var needToDisconnect = role == Role.Client
            ? await clientService.HandleConnectionLostAsync(roomId, e)
            : await hostService.HandleConnectionLostAsync(roomId, e);

        await PublishConnectionStatusAsync(roomId, e, needToDisconnect);
    }

    private async Task PingService_OnConnectionTimeout(Guid roomId, Guid connectionId)
    {
        if (registry.IsHosting(roomId))
        {
            await hostService.HandleConnectionTimeoutAsync(roomId, connectionId);
        }
        else if (registry.IsConnected(roomId))
        {
            await clientService.HandleConnectionTimeoutAsync(roomId, connectionId);
        }
    }

    private async Task PublishConnectionStatusAsync(Guid roomId, ConnectionStateChangedEventArgs e, bool needToDisconnect)
        => await eventBus.PublishAsync(new ConnectionStatusChangedEvent
        {
            RoomId = roomId,
            ConnectionId = e.ConnectionId,
            LeavingMessage = e.DisconnectReason.GetDescription(),
            NeedToDisconnect = needToDisconnect,
            IsConnected = e.IsConnected
        });
}
