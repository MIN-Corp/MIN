using System.Collections.Concurrent;
using MIN.Common.Core.Extensions;
using MIN.Core.Entities;
using MIN.Core.Entities.Contracts.Enums;
using MIN.Core.Entities.Contracts.Extensions;
using MIN.Core.Entities.Contracts.Models;
using MIN.Core.Events.Contracts.Interfaces;
using MIN.Core.Events.Events;
using MIN.Core.Identity.Contracts.Interfaces;
using MIN.Core.Messaging.RoomRelated;
using MIN.Core.Messaging.RoomRelated.ParticipantRelated;
using MIN.Core.Protocol.Contracts.Interfaces;
using MIN.Core.Services.Contracts.Constants;
using MIN.Core.Services.Contracts.Interfaces.Messaging;
using MIN.Core.Stores.Contracts.Exceptions;
using MIN.Core.Stores.Contracts.Interfaces;
using MIN.Core.Stores.Contracts.Registries.Interfaces;
using MIN.Core.SubRooms.Contracts.Interfaces;
using MIN.Core.Transport.Contracts.Enum;
using MIN.Core.Transport.Contracts.Events;
using MIN.Core.Transport.Contracts.Interfaces;
using MIN.Core.Transport.Contracts.Models;
using MIN.Helpers.Contracts.Interfaces;
using MIN.Helpers.Contracts.Models.Enums;

namespace MIN.Core.Services.Services;

internal sealed class HostRoomService
{
    private readonly IRoomFactory roomFactory;
    private readonly IHostHandshake hostHandshake;
    private readonly ITransport transport;
    private readonly IRoomStore roomStore;
    private readonly IEventBus eventBus;
    private readonly ISubRoomManager subRoomManager;
    private readonly IRoomConnectionRegistry registry;
    private readonly IIdentityService identityService;
    private readonly IMessageRouter messageRouter;
    private readonly ILoggerProvider logger;
    private readonly PingService pingService;

    private readonly ConcurrentDictionary<Guid, RoomInfo> readyRoomInfos = [];
    private readonly HashSet<(Guid, Guid)> markedParticipantsAsLeft = [];
    private readonly HashSet<Guid> markedRoomsToDestroy = [];
    private readonly Dictionary<Guid, CancellationTokenSource> roomCancellationTokenSources = [];
    private readonly HashSet<Guid> protocolPhase = [];

    public HostRoomService(IRoomFactory roomFactory,
        IHostHandshake hostHandshake,
        ITransport transport,
        IRoomStore roomStore,
        IEventBus eventBus,
        ISubRoomManager subRoomManager,
        IRoomConnectionRegistry registry,
        IIdentityService identityService,
        IMessageRouter messageRouter,
        ILoggerProvider logger,
        PingService pingService)
    {
        this.roomFactory = roomFactory;
        this.hostHandshake = hostHandshake;
        this.transport = transport;
        this.roomStore = roomStore;
        this.eventBus = eventBus;
        this.subRoomManager = subRoomManager;
        this.registry = registry;
        this.identityService = identityService;
        this.messageRouter = messageRouter;
        this.logger = logger;
        this.pingService = pingService;
    }

    public bool TryResolveRoom(ConnectionStateChangedEventArgs e, out Guid roomId)
    {
        if (e.ServerConnectionId is Guid serverConnectionId)
        {
            return registry.TryGetRoomIdByServerConnectionId(serverConnectionId, out roomId);
        }

        roomId = Guid.Empty;
        return false;
    }

    public bool TryResolveRoom(RawMessageReceivedEventArgs e, out Guid roomId)
    {
        if (e.ServerConnectionId is Guid serverConnectionId
            && registry.TryGetRoomIdByServerConnectionId(serverConnectionId, out roomId))
        {
            return !protocolPhase.Contains(e.ConnectionId);
        }

        roomId = Guid.Empty;
        return false;
    }

    public async Task<bool> HandleConnectionConnectedAsync(Guid roomId, ConnectionStateChangedEventArgs e)
    {
        protocolPhase.Add(e.ConnectionId);
        logger.Log($"Новое подключение к комнате {roomId}: {e.RemoteEndPoint ?? "unknown"}");

        var roomInfo = readyRoomInfos[roomId];
        var result = await hostHandshake.HandleServerAsync(
            e.ServerConnectionId!.Value, e.ConnectionId, roomInfo, roomCancellationTokenSources[roomId].Token);

        if (!result.IsSuccess)
        {
            logger.Log($"Клиент {e.RemoteEndPoint} не прошёл протокол: {result.ErrorMessage}");
            await transport.DisconnectClientAsync(e.ConnectionId, e.ServerConnectionId, DisconnectReason.ProtocolError);
            return false;
        }

        protocolPhase.Remove(e.ConnectionId);
        logger.Log($"Клиент {e.RemoteEndPoint} прошёл протокол для комнаты {roomId}");

        await pingService.RegisterHeartbeatSession(Role.Host, roomId, e.ConnectionId);
        return true;
    }

    public async Task<bool> HandleConnectionLostAsync(Guid roomId, ConnectionStateChangedEventArgs e)
    {
        await pingService.UnregisterHeartbeatSession(Role.Host, roomId, e.ConnectionId);

        if (!roomStore.TryGetRoom(roomId, out var room))
        {
            return false;
        }

        var context = roomFactory.GetOrCreateContext(roomId);
        if (!context.Connections.TryGetParticipantFromConnectionId(e.ConnectionId, out var leavingParticipant))
        {
            return false;
        }
        context.Connections.Unregister(e.ConnectionId);

        var hostParticipantId = roomStore.GetRoomHostParticipantId(roomId);
        var needToDisconnect = hostParticipantId == leavingParticipant.Id;

        if (needToDisconnect)
        {
            room.IsOnline = false;
            registry.DetachServerConnection(roomId);
            if (markedRoomsToDestroy.Remove(roomId))
            {
                await DestroyRoom(roomId);
            }
            else
            {
                await eventBus.PublishAsync(new RoomWentOfflineEvent()
                {
                    RoomId = roomId,
                    Reason = e.DisconnectReason.GetDescription(),
                });
            }
        }
        else if (context.Participants.TryGetParticipantById(leavingParticipant.Id, out _))
        {
            var reason = e.DisconnectReason;

            if (reason == DisconnectReason.Kick)
            {
                markedParticipantsAsLeft.Remove((roomId, leavingParticipant.Id));
                room.LocalRoomSettings.PendingKickParticipantIds.Remove(leavingParticipant.Id);
            }
            else
            {
                reason = markedParticipantsAsLeft.Remove((roomId, leavingParticipant.Id))
                    ? DisconnectReason.LeftRoom
                    : e.DisconnectReason;
            }

            await messageRouter.RouteAsync(new ParticipantLeftMessage()
            {
                Participant = leavingParticipant,
                Reason = reason,
            }, roomId, hostParticipantId, CancellationToken.None);
        }

        return needToDisconnect;
    }

    public async Task HandleConnectionTimeoutAsync(Guid roomId, Guid connectionId)
    {
        if (registry.TryGetServerConnectionIdByRoomId(roomId, out var serverConnectionId))
        {
            await transport.DisconnectClientAsync(connectionId, serverConnectionId, DisconnectReason.Timeout);
        }
    }

    public async Task<Room> StartHostingAsync(RoomInfo roomInfo, NetworkOptions networkOptions, CancellationToken cancellationToken)
    {
        if (registry.GetServerConnectionCount() + 1 > ServicesConstants.MaximumRoomHosts)
        {
            throw new InvalidOperationException($"Можно хостить максимум {ServicesConstants.MaximumRoomHosts} комнат");
        }

        var roomId = roomInfo.Id;

        if (registry.TryGetServerConnectionIdByRoomId(roomId, out _))
        {
            return roomStore.GetRoom(roomId);
        }

        var localParticipant = identityService.SelfParticipant.ToParticipantInfo();

        var connectionId = await transport.StartHostingAsync(prefferedPort: networkOptions.PrefferredPort,
            sequentialAttempts: ServicesConstants.MaximumRoomHosts, cancellationToken: cancellationToken);

        if (roomStore.TryGetRoom(roomId, out var existingRoom))
        {
            existingRoom.IsOnline = true;
            existingRoom.LocalRoomSettings.NetworkOptions = networkOptions;
            existingRoom.ConnectionAddresses = await transport.SetUpEndpoints(connectionId, networkOptions, cancellationToken: cancellationToken);
            roomCancellationTokenSources[roomId] = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            var existingContext = roomFactory.GetOrCreateContext(roomId);
            existingContext.Connections.RegisterLocalParticipant(localParticipant);

            registry.RegisterServerConnection(roomId, connectionId);
            readyRoomInfos[roomId] = roomInfo;

            return existingRoom;
        }

        roomInfo.HostParticipant = localParticipant;
        var room = new Room(roomInfo)
        {
            ConnectionAddresses = await transport.SetUpEndpoints(connectionId, networkOptions, cancellationToken: cancellationToken)
        };
        room.LocalRoomSettings.NetworkOptions = networkOptions;

        roomCancellationTokenSources[roomId] = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var context = roomFactory.GetOrCreateContext(roomId);
        context.Connections.RegisterLocalParticipant(localParticipant);

        roomStore.Register(room);

        context.Messages.AddMessage(new SystemTextMessage()
        {
            Content = $"Комната {roomInfo.Name} была создана в {DateTime.Now.ToShortTimeString()}",
        });

        context.Messages.AddMessage(new ParticipantJoinedMessage()
        {
            Participant = new Participant(localParticipant)
        });

        room.TotalMessageCount = context.Messages.GetMessageCount();

        context.Participants.AddParticipant(new Participant(localParticipant));

        logger.Log($"Комната создана: {string.Join(',', room.ConnectionAddresses)} ({roomInfo.Name})");

        registry.RegisterServerConnection(roomId, connectionId);
        readyRoomInfos[roomId] = roomInfo;

        return roomStore.GetRoom(roomId);
    }

    public async Task<IEnumerable<IEndpoint>> UpdateNetworkOptions(Guid roomId, NetworkOptions newNetworkOptions, CancellationToken cancellationToken)
    {
        var room = roomStore.GetRoom(roomId);

        if (!registry.TryGetServerConnectionIdByRoomId(roomId, out var connectionId))
        {
            return room.ConnectionAddresses;
        }

        var newEndpoints = await transport.SetUpEndpoints(connectionId, newNetworkOptions, room.LocalRoomSettings.NetworkOptions, cancellationToken);
        room.ConnectionAddresses = newEndpoints;
        room.LocalRoomSettings.NetworkOptions = newNetworkOptions;

        return room.ConnectionAddresses;
    }

    public void MarkParticipantAsLeftRoom(Guid roomId, Guid participantId)
        => markedParticipantsAsLeft.Add((roomId, participantId));

    public async Task KickClientAsync(Guid roomId, Guid participantId, DisconnectReason reason, string message)
    {
        if (!registry.TryGetServerConnectionIdByRoomId(roomId, out var serverConnectionId))
        {
            return;
        }

        if (!roomFactory.TryGetContext(roomId, out var context) || context == null)
        {
            return;
        }

        try
        {
            if (context.Connections.TryGetConnectionIdFromParticipantId(participantId, out _))
            {
                // Online

                if (reason == DisconnectReason.Kick)
                {
                    markedParticipantsAsLeft.Add((roomId, participantId));
                }
                var connectionId = context.Connections.GetConnectionIdFromParticipantId(participantId);
                await transport.DisconnectClientAsync(connectionId, serverConnectionId, reason);
            }
            else
            {
                // Offline

                if (reason != DisconnectReason.Kick)
                {
                    return;
                }

                var room = roomStore.GetRoom(roomId);
                room.LocalRoomSettings.PendingKickParticipantIds[participantId] = message;

                var hostParticipantId = roomStore.GetRoomHostParticipantId(roomId);
                context.Participants.TryGetParticipantById(participantId, out var leavingParticipant);

                await messageRouter.RouteAsync(new ParticipantLeftMessage()
                {
                    Participant = leavingParticipant!.ToParticipantInfo(),
                    Reason = reason,
                }, roomId, hostParticipantId, CancellationToken.None);
            }
        }
        catch (ParticipantNotRegistredException ex)
        {
            logger.Log($"Не удалось кикнуть участника {ex.Message}", LogLevel.Warning);
        }
    }

    public async Task KickConnectionAsync(Guid roomId, Guid connectionId, DisconnectReason reason)
    {
        if (!registry.TryGetServerConnectionIdByRoomId(roomId, out var serverConnectionId))
        {
            return;
        }

        if (!roomFactory.TryGetContext(roomId, out var context) || context == null)
        {
            return;
        }

        try
        {
            await transport.DisconnectClientAsync(connectionId, serverConnectionId, reason);
        }
        catch (Exception ex)
        {
            logger.Log($"Не удалось кикнуть соединение {ex.Message}", LogLevel.Warning);
        }
    }

    public async Task StopHostingAsync(Guid roomId)
    {
        if (!registry.IsHosting(roomId))
        {
            return;
        }

        var isLive = registry.TryGetServerConnectionIdByRoomId(roomId, out var connectionId);

        if (isLive)
        {
            await transport.StopHostingAsync(connectionId);
            //subRoomManager.ClearRoomSubRooms(roomId);
        }

        if (roomCancellationTokenSources.TryGetValue(roomId, out var cancellationTokenSource))
        {
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
            roomCancellationTokenSources.Remove(roomId);
        }

        var context = roomFactory.GetOrCreateContext(roomId);
        context.Participants.MarkAllParticipansOffline(exceptId: identityService.SelfParticipant.Id);
        var toDisconnect = context.Connections.UnregisterAllExceptLocal();
        foreach (var participantConnectionId in toDisconnect)
        {
            await pingService.UnregisterHeartbeatSession(Role.Host, roomId, participantConnectionId);
        }

        registry.DetachServerConnection(roomId);
        readyRoomInfos.TryRemove(roomId, out _);

        if (markedRoomsToDestroy.Remove(roomId))
        {
            await DestroyRoom(roomId);
            return;
        }

        await eventBus.PublishAsync(new RoomWentOfflineEvent() { RoomId = roomId });
    }

    public async Task ForgetRoom(Guid roomId)
    {
        markedRoomsToDestroy.Add(roomId);
        await StopHostingAsync(roomId);
    }

    private async Task DestroyRoom(Guid roomId)
    {
        registry.UnregisterServerConnection(roomId);
        roomStore.Remove(roomId);
        roomFactory.DestroyContext(roomId);
        await eventBus.PublishAsync(new RoomWentOfflineEvent() { RoomId = roomId });
        await eventBus.PublishAsync(new RoomDestroyedEvent() { RoomId = roomId });
    }
}
