using MIN.Common.Core.Contracts.Interfaces;
using MIN.Core.Entities;
using MIN.Core.Entities.Contracts.Interfaces;
using MIN.Core.Events.Contracts.Interfaces;
using MIN.Core.Events.Events;
using MIN.Core.Identity.Contracts.Interfaces;
using MIN.Core.Messaging.Contracts.Interfaces;
using MIN.Core.Messaging.Stateless.RoomRelated.Messages;
using MIN.Core.Messaging.Stateless.RoomRelated.ParticipantRelated;
using MIN.Core.Services.Contracts.Interfaces.Messaging;
using MIN.Core.Stores.Contracts.Interfaces;
using MIN.Core.Stores.Contracts.Models;
using MIN.Core.Stores.Contracts.Registries.Interfaces;
using MIN.Helpers.Contracts.Interfaces;
using MIN.Helpers.Contracts.Models.Enums;

namespace MIN.Core.Services.Lifecycle;

/// <summary>
/// Следит за изменением локального участника (имя, аватар)
/// и рассылает обновление во все подключённые комнаты
/// </summary>
public class IdentityMonitor : IHostedService
{
    /// <summary>
    /// Окно объединения всплесков изменений (защита от рассылки спама)
    /// </summary>
    private readonly static TimeSpan broadcastDelay = TimeSpan.FromMilliseconds(500);

    private readonly IIdentityService identityService;
    private readonly IRoomFactory roomFactory;
    private readonly IRoomConnectionRegistry registry;
    private readonly IEventBus eventBus;
    private readonly IMessageRouter messageRouter;
    private readonly ILoggerProvider logger;
    private readonly SemaphoreSlim flushGate = new(1, 1);

    private CancellationTokenSource? appCts;
    private volatile bool pending;
    private IParticipantData? latest;

    int IHostedService.Priority => 0;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="IdentityMonitor"/>
    /// </summary>
    public IdentityMonitor(IIdentityService identityService,
        IRoomFactory roomFactory,
        IRoomConnectionRegistry registry,
        IEventBus eventBus,
        IMessageRouter messageRouter,
        ILoggerProvider logger)
    {
        this.identityService = identityService;
        this.roomFactory = roomFactory;
        this.registry = registry;
        this.eventBus = eventBus;
        this.messageRouter = messageRouter;
        this.logger = logger;
    }

    Task IHostedService.StartAsync(CancellationToken cancellationToken)
    {
        appCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        identityService.SelfParticipantSaved += OnSelfParticipantChanged;
        eventBus.Subscribe<ParticipantUpdatedEvent>(OnParticipantUpdated);
        return Task.CompletedTask;
    }

    private async Task OnParticipantUpdated(ParticipantUpdatedEvent eventMessage, CancellationToken cancellationToken)
    {
        var context = roomFactory.GetOrCreateContext(eventMessage.RoomId);
        var role = registry.GetRole(eventMessage.RoomId);
        var newName = eventMessage.NewParticipant.Name;

        var affectedMessages = context.Messages.GetHistory()
            .Where(m => m.SenderId == eventMessage.NewParticipant.Id)
            .OfType<IAuthoredMessage>()
            .Where(m => m.SenderName != newName)
            .ToList();

        foreach (var authoredMessage in affectedMessages)
        {
            authoredMessage.SenderName = newName;

            await messageRouter.PublishLocally(new MessageUpdateMessage
            {
                SenderId = authoredMessage.SenderId,
                MessageIdToEdit = authoredMessage.Id,
                NewMessage = authoredMessage,
            }, eventMessage.RoomId, role, cancellationToken: cancellationToken);
        }
    }

    private Task OnSelfParticipantChanged(IParticipantData participant)
    {
        latest = participant;
        pending = true;

        if (!flushGate.Wait(0))
        {
            return Task.CompletedTask;
        }

        return FlushPendingAsync();
    }

    private async Task FlushPendingAsync()
    {
        try
        {
            while (pending)
            {
                pending = false;
                await Task.Delay(broadcastDelay, appCts!.Token).ConfigureAwait(false);

                var snapshot = latest!;
                foreach (var context in roomFactory.GetAllContexts())
                {
                    await BroadcastToRoomAsync(context, snapshot).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            flushGate.Release();
        }
    }

    private async Task BroadcastToRoomAsync(RoomContext context, IParticipantData participant)
    {
        var roomId = context.RoomId;

        try
        {
            if (!context.Participants.TryGetParticipantById(participant.Id, out var roomLocalParticipant)
                || roomLocalParticipant!.Name == participant.Name)
            {
                return;
            }

            var updatedParticipant = new Participant
            {
                Id = roomLocalParticipant.Id,
                Name = participant.Name,
                CurrentStatus = roomLocalParticipant.CurrentStatus,
                LastSeenOnline = roomLocalParticipant.LastSeenOnline,
                JoinedAt = roomLocalParticipant.JoinedAt,
            };

            if (registry.IsConnected(roomId) || registry.IsHosting(roomId))
            {
                await messageRouter.RouteAsync(new ParticipantUpdatedMessage
                {
                    NewParticipant = updatedParticipant,
                }, roomId, participant.Id, appCts!.Token).ConfigureAwait(false);
            }
            else
            {
                await messageRouter.PublishLocally(new ParticipantUpdatedMessage
                {
                    NewParticipant = updatedParticipant,
                }, roomId, registry.GetRole(roomId), null, appCts!.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.Log($"Не удалось разослать обновление участника в комнату {roomId}: {exception}", LogLevel.Error, typeof(IdentityMonitor));
        }
    }

    Task IHostedService.StopAsync(CancellationToken cancellationToken)
    {
        identityService.SelfParticipantSaved -= OnSelfParticipantChanged;
        return Task.CompletedTask;
    }
}
