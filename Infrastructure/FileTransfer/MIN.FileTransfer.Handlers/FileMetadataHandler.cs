using MIN.Core.Entities.Contracts.Enums;
using MIN.Core.Events.Contracts.Interfaces;
using MIN.Core.Handlers.Contracts.Base;
using MIN.Core.Handlers.Contracts.Models;
using MIN.Core.Messaging.Contracts;
using MIN.Core.Messaging.Contracts.Interfaces;
using MIN.Core.Services.Contracts.Interfaces.Messaging;
using MIN.FileTransfer.Events;
using MIN.FileTransfer.Messaging;
using MIN.FileTransfer.Services.Contracts.Interfaces;
using MIN.FileTransfer.Services.Contracts.Models;
using MIN.FileTransfer.Services.Contracts.Models.Enums;
using MIN.Helpers.Contracts.Interfaces;

namespace MIN.FileTransfer.Handlers;

internal sealed class FileMetadataHandler : BaseHandler
{
    private readonly IFileTransferService fileTransferService;
    private readonly IEventBus eventBus;
    private readonly IMessageRouter messageRouter;

    public FileMetadataHandler(IFileTransferService fileTransferService,
        IEventBus eventBus,
        IMessageRouter messageRouter,
        ILoggerProvider logger) : base(logger)
    {
        this.fileTransferService = fileTransferService;
        this.eventBus = eventBus;
        this.messageRouter = messageRouter;
    }

    public override IEnumerable<MessageTypeTag> HandledTypes => [MessageTypeTag.FileMetadata];

    protected override async Task<HandlerResult> HandleAsync(IMessage message, MessageContext context)
    {
        var metadata = (FileMetadataMessage)message;

        if (!context.RoomContext.Participants.TryGetParticipantById(metadata.SenderId, out var sender))
        {
            LogWarning($"Получил метаданные файла от неизвестного отправителя {metadata.SenderId}");
            return HandlerResult.Failure("Получил метаданные файла от неизвестного отправителя", stopPropagation: false);
        }

        LogInfo($"Получены метаданные файла: {metadata.FileName} ({metadata.FileSize} байт) от {metadata.SenderId}");

        var copy = new FileMetadataMessage(metadata);

        if (!metadata.AsDownloaded && context.Role == Role.Client)
        {
            context.RoomContext.Messages.AddMessage(copy);
        }

        var roomId = context.RoomContext.RoomId;

        var isHosting = context.Role == Role.Host;
        var isSelf = message.SenderId == context.SelfId;
        var hasAccess = isSelf || message.RecipientId == context.SelfId || message.IsPublic;
        var isHostDownload = !isHosting
            || metadata.AsDownloaded
            || (isHosting && isSelf);


        if (hasAccess && isHostDownload)
        {
            await eventBus.PublishAsync(new FileMetaDataMessageReceivedEvent()
            {
                RoomId = roomId,
                Message = copy,
            });

            if (isHosting && metadata.AsDownloaded)
            {
                return HandlerResult.Success();
            }
        }

        fileTransferService.RegisterFileMetadata(message.Id, roomId, metadata.FileName, !metadata.AsDownloaded
            ? copy.FilePath
            : null);

        if (isHosting && isSelf)
        {
            context.RoomContext.Messages.AddMessage(copy);
            return HandlerResult.Success();
        }

        if (!isHosting || isSelf)
        {
            return HandlerResult.Success();
        }

        // Хост получает файл от клиента — запрашиваем загрузку на сервер у клиента
        LogInfo($"Хост: регистрирую загрузку файла {metadata.FileName} (TransferId: {metadata.TransferId})");

        fileTransferService.RegisterPendingMetadata(metadata.TransferId, metadata.FileName);
        eventBus.Subscribe(async (FilePendingMetaDataReceivedEvent e, CancellationToken _) =>
        {
            if (e.TransferId != metadata.TransferId)
            {
                return;
            }

            metadata.AsDownloaded = true;
            metadata.FilePath = e.FilePath;
            fileTransferService.RegisterFileMetadata(message.Id, roomId, metadata.FileName);

            context.RoomContext.Messages.AddMessage(new FileMetadataMessage(metadata));
            await messageRouter.RouteAsync(metadata, roomId, metadata.SenderId, context.CancellationToken);
        });

        fileTransferService.RegisterTransfer(new TransferInfo
        {
            TransferId = metadata.TransferId,
            FileMetadataId = metadata.Id,
            RoomId = roomId,
            SenderId = metadata.SenderId,
            Direction = FileTransferDirection.Upload,
            FileName = metadata.FileName,
        });

        await eventBus.PublishAsync(new FileTransferStartedEvent
        {
            RoomId = roomId,
            FileMetadataId = metadata.Id,
            FileName = metadata.FileName,
            FileSize = metadata.FileSize,
            Sender = metadata.Sender,
            Direction = FileTransferDirection.Upload,
        });

        LogInfo($"Отправляю FileTransferRequest (Upload) участнику {metadata.SenderId} для файла {metadata.FileName}");

        return HandlerResult.WithResponse(new FileTransferRequestMessage
        {
            TransferId = metadata.TransferId,
            RecipientId = metadata.SenderId,
            FileMetadataId = message.Id,
            Direction = FileTransferDirection.Upload,
        }, stopPropagation: true);
    }
}
