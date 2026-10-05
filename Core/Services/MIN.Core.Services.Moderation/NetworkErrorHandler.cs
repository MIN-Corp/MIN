using System.Collections.Concurrent;
using MIN.Core.Events.Contracts.Interfaces;
using MIN.Core.Events.Events;
using MIN.Core.Identity.Contracts.Interfaces;
using MIN.Core.Messaging.Stateless;
using MIN.Core.Messaging.Stateless.RoomRelated.Disconnect;
using MIN.Core.Services.Contracts.Interfaces.Lifecycle;
using MIN.Core.Services.Contracts.Interfaces.Messaging;
using MIN.Core.Services.Contracts.Interfaces.Moderation;
using MIN.Core.Services.Contracts.Models;
using MIN.Core.Transport.Contracts.Enum;

namespace MIN.Core.Services.Moderation;

/// <inheritdoc cref="INetworkErrorHandler"/>
public class NetworkErrorHandler : INetworkErrorHandler
{
    private readonly IRoomLifecycleManager lifecycleManager;
    private readonly IMessageRouter messageRouter;
    private readonly IMessageSender messageSender;
    private readonly IEventBus eventBus;
    private readonly IIdentityService identityService;

    private readonly ConcurrentDictionary<Guid, Timer> rejectParticipantsAckTimers = new(); // participantId / timer
    private readonly ConcurrentDictionary<Guid, Timer> rejectConnectionsAckTimers = new(); // connectionId / timer

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="NetworkErrorHandler"/>
    /// </summary>
    public NetworkErrorHandler(IRoomLifecycleManager lifecycleManager,
        IMessageRouter messageRouter,
        IMessageSender messageSender,
        IEventBus eventBus,
        IIdentityService identityService)
    {
        this.lifecycleManager = lifecycleManager;
        this.messageRouter = messageRouter;
        this.messageSender = messageSender;
        this.eventBus = eventBus;
        this.identityService = identityService;

        SubscribeToEvents();
    }

    private void SubscribeToEvents()
    {
        eventBus.Subscribe<DisconnectAckReceived>(OnDisconnectAckReceived);
    }

    async Task INetworkErrorHandler.SendErrorAsync(string message, Guid recipientId, Guid roomId, bool critical, int timeoutMs)
    {
        var selfId = identityService.SelfParticipant.Id;

        if (!critical)
        {
            await messageRouter.RouteAsync(new ErrorMessage()
            {
                Message = message,
                RecipientId = recipientId,
            }, roomId, selfId, CancellationToken.None);
            return;
        }

        await messageRouter.RouteAsync(new DisconnectMessage()
        {
            Reason = message,
            RecipientId = recipientId,
        }, roomId, selfId, CancellationToken.None,
        broadcastExcludeIds: [selfId]); // Оно не broadcast, но сервер попытается его захендлить, поэтому фильтруем

        var timer = new Timer(OnParticipantRejectAckTimeout,
            new ParticipantContext(roomId, recipientId),
            Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

        if (rejectParticipantsAckTimers.TryRemove(recipientId, out var previousTimer))
        {
            previousTimer.Dispose();
        }

        rejectParticipantsAckTimers[recipientId] = timer;

        timer.Change(TimeSpan.FromMilliseconds(timeoutMs), Timeout.InfiniteTimeSpan);
    }

    async Task INetworkErrorHandler.SendErrorToConnectionAsync(string message, Guid connectionId, Guid roomId, bool critical, int timeoutMs)
    {
        if (!critical)
        {
            await messageSender.SendAsync(new ErrorMessage()
            {
                Message = message,
            }, roomId, connectionId, CancellationToken.None);
            return;
        }

        await messageSender.SendAsync(new DisconnectMessage()
        {
            Reason = message,
        }, roomId, connectionId, CancellationToken.None);

        var timer = new Timer(OnConnectionRejectAckTimeout,
            new ConnectionResult(roomId, connectionId),
            Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

        if (rejectConnectionsAckTimers.TryRemove(connectionId, out var previousTimer))
        {
            previousTimer.Dispose();
        }

        rejectConnectionsAckTimers[connectionId] = timer;

        timer.Change(TimeSpan.FromMilliseconds(timeoutMs), Timeout.InfiniteTimeSpan);
    }

    private async Task OnDisconnectAckReceived(DisconnectAckReceived e, CancellationToken cancellationToken)
    {
        if (ResetParticipantRejectAckTimer(e.ParticipantId))
        {
            await DisconnectClient(e.ParticipantId, e.RoomId, e.ErrorMessage);
        }
        else
        {
            await DisconnectConnection(e.ConnectionId, e.RoomId);
        }
    }

    private async void OnParticipantRejectAckTimeout(object? state)
    {
        if (state is ParticipantContext connection && ResetParticipantRejectAckTimer(connection.ParticipantId))
        {
            await DisconnectClient(connection.ParticipantId, connection.RoomId, string.Empty);
        }
    }

    private async void OnConnectionRejectAckTimeout(object? state)
    {
        if (state is ConnectionResult connection && ResetConnectionRejectAckTimer(connection.ConnectionId))
        {
            await DisconnectConnection(connection.ConnectionId, connection.RoomId);
        }
    }

    private async Task DisconnectClient(Guid participantId, Guid roomId, string message)
        => await lifecycleManager.KickClientAsync(roomId, participantId, DisconnectReason.Kick, message);

    private async Task DisconnectConnection(Guid connectionId, Guid roomId)
        => await lifecycleManager.KickConnectionAsync(roomId, connectionId, DisconnectReason.Kick);

    private bool ResetParticipantRejectAckTimer(Guid participantId)
    {
        if (rejectParticipantsAckTimers.TryRemove(participantId, out var existingTimer))
        {
            existingTimer.Dispose();
            return true;
        }
        return false;
    }

    private bool ResetConnectionRejectAckTimer(Guid participantId)
    {
        if (rejectParticipantsAckTimers.TryRemove(participantId, out var existingTimer))
        {
            existingTimer.Dispose();
            return true;
        }
        return false;
    }
}
