using System.Collections.Concurrent;
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

        if (!roomStore.RoomExists(roomId))
        {
            return false;
        }

        var context = roomFactory.GetOrCreateContext(roomId);
        if (!context.Connections.TryGetParticipantFromConnectionId(e.ConnectionId, out var leavingParticipant))
        {
            return false;
        }

        var hostParticipantId = roomStore.GetRoomHostParticipantId(roomId);
        var needToDisconnect = hostParticipantId == leavingParticipant.Id;

        if (needToDisconnect)
        {
            roomStore.Remove(roomId);
            roomFactory.DestroyContext(roomId);
            await eventBus.PublishAsync(new RoomClosedEvent() { RoomId = roomId });
        }
        else if (context.Participants.TryGetParticipantById(leavingParticipant.Id, out _))
        {
            context.Connections.Unregister(e.ConnectionId);
            var participantLeftMessage = new ParticipantLeftMessage()
            {
                Participant = leavingParticipant,
                Reason = e.DisconnectReason,
            };

            await messageRouter.RouteAsync(participantLeftMessage, roomId, hostParticipantId, CancellationToken.None);
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

        if (registry.IsHosting(roomId))
        {
            return roomStore.GetRoom(roomId);
        }

        roomCancellationTokenSources[roomId] = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var context = roomFactory.GetOrCreateContext(roomId);

        var localParticipant = identityService.SelfParticipant.ToParticipantInfo();

        context.Connections.RegisterLocalParticipant(localParticipant);
        roomInfo.HostParticipant = localParticipant;

        var room = new Room(roomInfo);

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

        var connectionId = await transport.StartHostingAsync(cancellationToken: cancellationToken);
        room.ConnectionAddresses = await transport.SetUpEndpoints(connectionId, networkOptions, cancellationToken: cancellationToken);
        room.LocalRoomSettings.NetworkOptions = networkOptions;

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

    public async Task StopHostingAsync(Guid roomId)
    {
        if (!registry.TryGetServerConnectionIdByRoomId(roomId, out var connectionId))
        {
            return;
        }

        if (roomCancellationTokenSources.TryGetValue(roomId, out var cancellationTokenSource))
        {
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
            roomCancellationTokenSources.Remove(roomId);
        }

        await transport.StopHostingAsync(connectionId);
        subRoomManager.ClearRoomSubRooms(roomId);

        registry.UnregisterServerConnection(roomId);
        readyRoomInfos.TryRemove(roomId, out _);

        roomStore.Remove(roomId);
        roomFactory.DestroyContext(roomId);

        await eventBus.PublishAsync(new RoomClosedEvent() { RoomId = roomId });
    }

    public async Task KickClientAsync(Guid roomId, Guid participantId, DisconnectReason reason)
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
            var connectionId = context.Connections.GetConnectionIdFromParticipantId(participantId);
            await transport.DisconnectClientAsync(connectionId, serverConnectionId, reason);
        }
        catch (ParticipantNotRegistredException ex)
        {
            logger.Log($"Не удалось кикнуть участника {ex.Message}", LogLevel.Warning);
        }
    }
}
