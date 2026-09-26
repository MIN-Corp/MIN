using MIN.Common.Core.Contracts.Interfaces;
using MIN.Core.Events.Contracts.Interfaces;
using MIN.Core.Events.Events;
using MIN.Core.Services.Contracts.Interfaces.Moderation;
using MIN.Core.Stores.Contracts.Registries.Interfaces;
using MIN.FileTransfer.Events;
using MIN.FileTransfer.Services.Contracts.Interfaces;
using MIN.FileTransfer.Services.Contracts.Models.Enums;
using MIN.Helpers.Contracts.Interfaces;

namespace MIN.FileTransfer.Services;

/// <summary>
/// Сервис по отслеживанию состояния возможности передачи файла
/// </summary>
public sealed class FileMonitorService : IHostedService
{
    private readonly IEventBus eventBus;
    private readonly IRoomConnectionRegistry registry;
    private readonly INetworkErrorHandler errorHandler;
    private readonly IFileTransferService fileTransferService;
    private readonly ILoggerProvider logger;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="FileMonitorService"/>
    /// </summary>
    public FileMonitorService(IEventBus eventBus,
        IRoomConnectionRegistry registry,
        INetworkErrorHandler errorHandler,
        IFileTransferService fileTransferService,
        ILoggerProvider logger)
    {
        this.eventBus = eventBus;
        this.registry = registry;
        this.errorHandler = errorHandler;
        this.fileTransferService = fileTransferService;
        this.logger = logger;
    }

    Task IHostedService.StartAsync(CancellationToken cancellationToken)
    {
        eventBus.Subscribe<ParticipantLeftEvent>(OnParticipantLeft);
        eventBus.Subscribe<RoomWentOfflineEvent>(WentOffline);
        eventBus.Subscribe<MessageDeletedEvent>(OnMessageDeleted);

        return Task.CompletedTask;
    }

    private async Task OnParticipantLeft(ParticipantLeftEvent eventMessage, CancellationToken cancellationToken)
    {
        var activeTransfers = fileTransferService.GetActiveTransfers();
        foreach (var transfer in activeTransfers.Where(x => x.SenderId == eventMessage.Message.Participant.Id))
        {
            var excuse = (eventMessage.Message as IDescribable).GetDescription();

            logger.Log($"Ошибка при передаче файла из потока {transfer.TransferId}: {excuse}");

            await eventBus.PublishAsync(new FileTransferFailedEvent
            {
                RoomId = eventMessage.RoomId,
                SenderId = transfer.SenderId,
                ErrorMessage = excuse,
            }, cancellationToken);

            fileTransferService.RemoveTransfer(transfer.TransferId);
        }
    }

    private async Task WentOffline(RoomWentOfflineEvent eventMessage, CancellationToken cancellationToken)
    {
        var activeTransfers = fileTransferService.GetActiveTransfers();
        foreach (var transfer in activeTransfers.Where(x => x.RoomId == eventMessage.RoomId))
        {
            var excuse = "Комната была закрыта";

            logger.Log($"Ошибка при передаче файла из потока {transfer.TransferId}: {excuse}");

            await eventBus.PublishAsync(new FileTransferFailedEvent
            {
                RoomId = eventMessage.RoomId,
                SenderId = transfer.SenderId,
                ErrorMessage = excuse,
            }, cancellationToken);

            fileTransferService.RemoveTransfer(transfer.TransferId);
        }
    }

    private async Task OnMessageDeleted(MessageDeletedEvent eventMessage, CancellationToken cancellationToken)
    {
        var activeTransfers = fileTransferService.GetActiveTransfers();
        foreach (var transfer in activeTransfers.Where(x => x.FileMetadataId == eventMessage.MessageId))
        {
            var excuse = $"Сообщения файла {transfer.FileName} было удалено";

            if (registry.IsHosting(eventMessage.RoomId) && transfer.Direction == FileTransferDirection.Download)
            {
                await errorHandler.SendErrorAsync(excuse, transfer.SenderId, eventMessage.RoomId);
            }

            logger.Log($"Ошибка при передаче файла из потока {transfer.TransferId}: {excuse}");

            await eventBus.PublishAsync(new FileTransferFailedEvent
            {
                RoomId = eventMessage.RoomId,
                SenderId = transfer.SenderId,
                ErrorMessage = excuse,
            }, cancellationToken);

            fileTransferService.RemoveTransfer(transfer.TransferId);
        }
    }

    Task IHostedService.StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
