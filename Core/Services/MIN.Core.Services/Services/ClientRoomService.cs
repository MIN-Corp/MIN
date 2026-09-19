using System.Collections.Concurrent;
using MIN.Common.Core.Extensions;
using MIN.Core.Cryptography.Contracts.Interfaces;
using MIN.Core.Entities;
using MIN.Core.Entities.Contracts.Enums;
using MIN.Core.Entities.Contracts.Extensions;
using MIN.Core.Events.Contracts.Interfaces;
using MIN.Core.Events.Events;
using MIN.Core.Identity.Contracts.Interfaces;
using MIN.Core.Messaging.Stateless.Handshake;
using MIN.Core.Messaging.Stateless.RoomRelated.Leaving;
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
    private const int RoomLeavingAckTimeoutSeconds = 5;

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

    private readonly HashSet<Guid> destroyOnDropRoomIds = [];
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource> pendingRoomLeaves = new();

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

        var roomExistedBefore = false;

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
                await transport.DisconnectAsync(connectionResult.ConnectionId, DisconnectReason.Error);
                throw new InvalidOperationException("Вы уже подключены к этой комнате");
            }

            connectionResult.RoomId = result.RoomInfo.Id;
            logger.Log($"Протокол успешен, комната {connectionResult.RoomId}");

            await pingService.RegisterHeartbeatSession(Role.Client, connectionResult.RoomId, connectionResult.ConnectionId);

            var selfParticipant = identityService.SelfParticipant.ToParticipantInfo();

            roomFactory.GetOrCreateContext(connectionResult.RoomId)
                .Connections.RegisterLocalParticipant(selfParticipant);

            roomExistedBefore = roomStore.RoomExists(connectionResult.RoomId);

            var room = roomExistedBefore
                ? roomStore.GetRoom(connectionResult.RoomId)
                : new Room(result.RoomInfo)
                {
                    ConnectionAddresses = [endpoint]
                };

            if (!roomExistedBefore)
            {
                roomStore.Register(room);
            }

            logger.Log($"Подключились к комнате с id {connectionResult.RoomId}, соединение с id {connectionResult.ConnectionId}");

            var localKey = await encryptor.GetLocalPublicKey();

            var selfHandshake = new HandshakeMessage()
            {
                SenderId = selfParticipant.Id,
                Participant = selfParticipant,
                Version = versionProvider.Version,
                PublicKey = roomExistedBefore ? null : localKey,
                PublicKeyFingerprint = roomExistedBefore ? encryptor.ComputeKeyFingerprint(localKey) : null,
            };

            await messageSender.SendAsync(selfHandshake, connectionResult.RoomId, connectionResult.ConnectionId, cancellationToken);
            registry.RegisterClientConnection(connectionResult.RoomId, connectionResult.ConnectionId);

            return connectionResult;
        }
        catch (TimeoutException) { return connectionResult; }
        catch (OperationCanceledException) { return connectionResult; }
        catch
        {
            if (connectionResult.RoomId != Guid.Empty && !roomExistedBefore)
            {
                roomStore.Remove(connectionResult.RoomId);
                roomFactory.DestroyContext(connectionResult.RoomId);
            }

            throw;
        }
    }

    public async Task<bool> HandleConnectionLostAsync(Guid roomId, ConnectionStateChangedEventArgs e)
    {
        await pingService.UnregisterHeartbeatSession(Role.Client, roomId, e.ConnectionId);
        registry.UnregisterClientConnection(e.ConnectionId);
        logger.Log($"Отключились от комнаты с id {roomId}, соединение было с id {e.ConnectionId}");

        var context = roomFactory.GetOrCreateContext(roomId);
        if (!context.Connections.ConnectionExists(e.ConnectionId))
        {
            return false;
        }

        if (!roomStore.TryGetRoom(roomId, out var room))
        {
            return false;
        }

        if (destroyOnDropRoomIds.Remove(roomId))
        {
            await DestroyRoom(roomId, e.DisconnectReason);
            return true;
        }

        room.IsOnline = false;
        var allParticipantsExceptSelf = context.Participants.GetParticipants().Where(x => x.Id != identityService.SelfParticipant.Id);
        foreach (var participant in allParticipantsExceptSelf)
        {
            participant.CurrentStatus = OnlineStatus.Offline;
        }

        await eventBus.PublishAsync(new RoomWentOfflineEvent()
        {
            RoomId = roomId,
            Reason = e.DisconnectReason != DisconnectReason.Intentionally
                ? e.DisconnectReason.GetDescription()
                : null
        });
        return false;
    }

    public async Task HandleConnectionTimeoutAsync(Guid roomId, Guid connectionId)
    {
        if (registry.IsConnected(roomId))
        {
            await transport.DisconnectAsync(connectionId, DisconnectReason.Timeout);
        }
    }

    public void MarkRoomForDeletion(Guid roomId)
        => destroyOnDropRoomIds.Add(roomId);

    public async Task DisconnectAsync(Guid roomId, Guid connectionId)
    {
        if (!registry.IsConnected(roomId))
        {
            return;
        }

        logger.Log($"Я сам инициирую отключение от комнаты с id {roomId} с соединением {connectionId}");

        // Transport will fire event, where it would cleanup further
        await transport.DisconnectAsync(connectionId, DisconnectReason.Intentionally);
    }

    public async Task ForgetRoomAsync(Guid roomId, Guid connectionId)
    {
        if (!roomStore.GetRoom(roomId).IsOnline)
        {
            await DestroyRoom(roomId, DisconnectReason.LeftRoom);
            return;
        }

        MarkRoomForDeletion(roomId);
        var ackTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        pendingRoomLeaves[roomId] = ackTcs;

        await messageSender.SendAsync(new RoomLeaveMessage()
        {
            SenderId = identityService.SelfParticipant.Id,
        }, roomId, connectionId);

        try
        {
            await ackTcs.Task.WaitAsync(TimeSpan.FromSeconds(RoomLeavingAckTimeoutSeconds));
        }
        catch (TimeoutException)
        {
            logger.Log($"RoomLeaveAck не получен для комнаты {roomId}, отключаюсь всё равно", LogLevel.Warning);
        }
        finally
        {
            pendingRoomLeaves.TryRemove(roomId, out _);
        }

        await transport.DisconnectAsync(connectionId, DisconnectReason.LeftRoom);
    }

    public void CompleteRoomLeaveAck(Guid roomId)
    {
        if (pendingRoomLeaves.TryRemove(roomId, out var tcs))
        {
            tcs?.TrySetResult();
        }
    }

    private async Task DestroyRoom(Guid roomId, DisconnectReason reason)
    {
        roomStore.Remove(roomId);
        roomFactory.DestroyContext(roomId);
        await eventBus.PublishAsync(new RoomWentOfflineEvent()
        {
            RoomId = roomId,
            Reason = reason.GetDescription()
        });
        await eventBus.PublishAsync(new RoomDestroyedEvent()
        {
            RoomId = roomId,
            Reason = reason
        });
    }
}
