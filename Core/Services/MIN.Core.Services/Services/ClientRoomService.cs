using MIN.Core.Cryptography.Contracts.Interfaces;
using MIN.Core.Entities;
using MIN.Core.Entities.Contracts.Enums;
using MIN.Core.Entities.Contracts.Extensions;
using MIN.Core.Events.Contracts.Interfaces;
using MIN.Core.Events.Events;
using MIN.Core.Identity.Contracts.Interfaces;
using MIN.Core.Messaging.Stateless.Handshake;
using MIN.Core.Protocol.Contracts.Interfaces;
using MIN.Core.Services.Contracts.Interfaces.Messaging;
using MIN.Core.Services.Contracts.Models;
using MIN.Core.Stores.Contracts.Interfaces;
using MIN.Core.Stores.Contracts.Registries.Interfaces;
using MIN.Core.Transport.Contracts.Enum;
using MIN.Core.Transport.Contracts.Events;
using MIN.Core.Transport.Contracts.Interfaces;
using MIN.Helpers.Contracts.Interfaces;
using MIN.Helpers.Contracts.Models.Enums;

namespace MIN.Core.Services.Services;

internal sealed class ClientRoomService
{
    private readonly ITransport transport;
    private readonly IClientHandshake clientHandshake;
    private readonly IRoomStore roomStore;
    private readonly IRoomFactory roomFactory;
    private readonly IMessageSender messageSender;
    private readonly IIdentityService identityService;
    private readonly IMessageEncryptor encryptor;
    private readonly IRoomConnectionRegistry registry;
    private readonly IVersionProvider versionProvider;
    private readonly IEventBus eventBus;
    private readonly ILoggerProvider logger;
    private readonly PingService pingService;

    public ClientRoomService(ITransport transport,
        IClientHandshake clientHandshake,
        IRoomStore roomStore,
        IRoomFactory roomFactory,
        IMessageSender messageSender,
        IIdentityService identityService,
        IMessageEncryptor encryptor,
        IRoomConnectionRegistry registry,
        IVersionProvider versionProvider,
        IEventBus eventBus,
        ILoggerProvider logger,
        PingService pingService)
    {
        this.transport = transport;
        this.clientHandshake = clientHandshake;
        this.roomStore = roomStore;
        this.roomFactory = roomFactory;
        this.messageSender = messageSender;
        this.identityService = identityService;
        this.encryptor = encryptor;
        this.registry = registry;
        this.versionProvider = versionProvider;
        this.eventBus = eventBus;
        this.logger = logger;
        this.pingService = pingService;
    }

    public bool TryResolveRoom(ConnectionStateChangedEventArgs e, out Guid roomId)
        => registry.TryGetRoomIdByClientConnectionId(e.ConnectionId, out roomId);

    public bool TryResolveRoom(RawMessageReceivedEventArgs e, out Guid roomId)
        => registry.TryGetRoomIdByClientConnectionId(e.ConnectionId, out roomId);

    public async Task<ConnectionResult> ConnectAsync(IEndpoint endpoint, CancellationToken cancellationToken)
    {
        var connectionResult = new ConnectionResult();

        try
        {
            logger.Log($"Подключаюсь к {endpoint}");

            connectionResult.ConnectionId = await transport.ConnectAsync(endpoint, cancellationToken: cancellationToken);

            var result = await clientHandshake.HandleClientAsync(connectionResult.ConnectionId, cancellationToken);

            if (!result.IsSuccess)
            {
                logger.Log($"Протокол не пройден для {endpoint}: {result.ErrorMessage}", LogLevel.Error);
                await transport.DisconnectAsync(connectionResult.ConnectionId, DisconnectReason.ProtocolError);
                throw new InvalidOperationException(result.ErrorMessage);
            }

            if (registry.IsConnected(result.RoomInfo.Id) || registry.IsHosting(result.RoomInfo.Id))
            {
                throw new InvalidOperationException("Вы уже подключены к этой комнате");
            }

            connectionResult.RoomId = result.RoomInfo.Id;
            logger.Log($"Протокол успешен, комната {connectionResult.RoomId}");

            await pingService.RegisterHeartbeatSession(Role.Client, connectionResult.RoomId, connectionResult.ConnectionId);

            var selfParticipant = identityService.SelfParticipant.ToParticipantInfo();

            roomFactory.GetOrCreateContext(connectionResult.RoomId)
                .Connections.RegisterLocalParticipant(selfParticipant);

            roomStore.Register(new Room(result.RoomInfo));

            logger.Log($"Подключились к комнате с id {connectionResult.RoomId}, соединение с id {connectionResult.ConnectionId}");

            var selfHandshake = new HandshakeMessage()
            {
                Participant = selfParticipant,
                PublicKey = await encryptor.GetLocalPublicKey(),
                Version = versionProvider.Version
            };

            await messageSender.SendAsync(selfHandshake, connectionResult.RoomId, connectionResult.ConnectionId, cancellationToken);
            registry.RegisterClientConnection(connectionResult.RoomId, connectionResult.ConnectionId);

            return connectionResult;
        }
        catch (TimeoutException) { return connectionResult; }
        catch (OperationCanceledException) { return connectionResult; }
        catch
        {
            if (connectionResult.RoomId != Guid.Empty)
            {
                roomStore.Remove(connectionResult.RoomId);
                roomFactory.DestroyContext(connectionResult.RoomId);
            }

            throw;
        }
    }

    public async Task DisconnectAsync(Guid roomId, Guid connectionId, DisconnectReason reason)
    {
        if (!registry.IsConnected(roomId))
        {
            return;
        }

        logger.Log($"Я сам инициирую отключение от комнаты с id {roomId} с соединением {connectionId}: {reason}");

        // Transport will fire event, where it would cleanup further
        await transport.DisconnectAsync(connectionId, reason);
    }

    public async Task<bool> HandleConnectionLostAsync(Guid roomId, ConnectionStateChangedEventArgs e)
    {
        await pingService.UnregisterHeartbeatSession(Role.Client, roomId, e.ConnectionId);
        registry.UnregisterClientConnection(e.ConnectionId);
        logger.Log($"Отключились от комнаты с id {roomId}, соединение было с id {e.ConnectionId}");

        if (!roomStore.RoomExists(roomId))
        {
            return false;
        }

        var context = roomFactory.GetOrCreateContext(roomId);
        if (!context.Connections.TryGetParticipantFromConnectionId(e.ConnectionId, out var leavingParticipant))
        {
            return false;
        }

        var isHostLeaving = roomStore.GetRoomHostParticipantId(roomId) == leavingParticipant.Id;

        if (isHostLeaving)
        {
            roomStore.Remove(roomId);
            roomFactory.DestroyContext(roomId);
            await eventBus.PublishAsync(new RoomClosedEvent() { RoomId = roomId });
        }

        return isHostLeaving;
    }

    public async Task HandleConnectionTimeoutAsync(Guid roomId, Guid connectionId)
    {
        if (registry.IsConnected(roomId))
        {
            await DisconnectAsync(roomId, connectionId, DisconnectReason.Timeout);
        }
    }
}
