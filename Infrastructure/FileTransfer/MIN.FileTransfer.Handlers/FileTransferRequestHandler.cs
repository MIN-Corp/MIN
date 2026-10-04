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

internal sealed class FileTransferRequestHandler : BaseHandler
{
    private readonly IFileTransferService fileTransferService;
    private readonly IFileStorageService fileStorageService;
    private readonly IMessageSender messageSender;
    private readonly IEventBus eventBus;

    public FileTransferRequestHandler(IFileTransferService fileTransferService,
        IFileStorageService fileStorageService,
        IMessageSender messageSender,
        IEventBus eventBus,
        ILoggerProvider logger) : base(logger)
    {
        this.fileTransferService = fileTransferService;
        this.fileStorageService = fileStorageService;
        this.messageSender = messageSender;
        this.eventBus = eventBus;
    }

    public override IEnumerable<MessageTypeTag> HandledTypes => [MessageTypeTag.FileTransferRequest];

    protected override async Task<HandlerResult> HandleAsync(IMessage message, MessageContext context)
    {
        var request = (FileTransferRequestMessage)message;

        LogInfo($"Получен FileTransferRequest: TransferId={request.TransferId}, Direction={request.Direction}, FileName={request.FileName}");

        if (message.SenderId == context.SelfId && context.Role == Role.Host)
        {
            if (!fileTransferService.TryGetTransferInfo(request.TransferId, out var info))
            {
                LogError($"Не найдена информация о transfer {request.TransferId}");
                return HandlerResult.Failure($"Не найдена информация о transfer {request.TransferId}", stopPropagation: false);
            }

            var filePath = ResolveFilePath(context, request.FileMetadataId, context.RoomContext.RoomId);
            if (filePath != null)
            {
                await eventBus.PublishAsync(new FileTransferCompletedEvent
                {
                    RoomId = info.RoomId,
                    FilePath = filePath,
                    FileMetadataId = info.FileMetadataId,
                    SenderId = info.SenderId,
                });

                return HandlerResult.Success(stopPropagation: true);
            }

            await eventBus.PublishAsync(new FileTransferFailedEvent
            {
                RoomId = info.RoomId,
                SenderId = message.SenderId,
                FileMetadataId = info.FileMetadataId,
                ErrorMessage = "Файл был утерян",
            });

            return HandlerResult.Failure("Файл был утерян", stopPropagation: true, showErrorMessage: false);
        }

        if (request.Direction == FileTransferDirection.Upload)
        {
            return await HandleUpload(request, context);
        }
        else
        {
            return await HandleDownload(request, context);
        }
    }

    private async Task<HandlerResult> HandleUpload(FileTransferRequestMessage request, MessageContext context)
    {
        var roomId = context.RoomContext.RoomId;

        if (request.RecipientId != context.SelfId)
        {
            LogInfo($"Запрос на загрузку файла адресован не мне (мне: {context.SelfId}, запрос: {request.RecipientId})");
            return HandlerResult.Failure("Запрос на загрузку файла адресован не мне", stopPropagation: false);
        }

        if (!fileTransferService.TryGetTransferInfo(request.TransferId, out var info))
        {
            LogError($"Не найдена информация о transfer {request.TransferId}");
            return HandlerResult.Failure($"Не найдена информация о transfer {request.TransferId}", stopPropagation: false);
        }

        var filePath = ResolveFilePath(context, request.FileMetadataId, roomId);
        if (filePath == null)
        {
            LogError($"Файл не найден для upload: {info.FileName} (TransferId: {request.TransferId})");
            var errorResponse = new FileTransferResponseMessage
            {
                TransferId = request.TransferId,
                Success = false,
                ErrorMessage = "Файл не найден во время загрузки",
            };

            await messageSender.SendAsync(errorResponse, roomId, context.ConnectionId, context.CancellationToken);
            return HandlerResult.Failure("Файл не найден во время загрузки", stopPropagation: true);
        }

        LogInfo($"Начинаю загрузку файла {info.FileName} на сервер из: {filePath}");

        var response = new FileTransferResponseMessage
        {
            TransferId = request.TransferId,
            Success = true,
        };

        await messageSender.SendAsync(response, roomId, context.ConnectionId, context.CancellationToken);

        await StreamFileAsync(filePath, request, context);

        return HandlerResult.Success(stopPropagation: true);
    }

    private async Task<HandlerResult> HandleDownload(FileTransferRequestMessage request, MessageContext context)
    {
        var roomId = context.RoomContext.RoomId;

        if (!fileTransferService.TryGetTransferInfo(request.TransferId, out _))
        {
            LogInfo($"Регистрирую новый download transfer для файла {request.FileName} (TransferId: {request.TransferId})");

            var info = new TransferInfo
            {
                TransferId = request.TransferId,
                FileMetadataId = request.FileMetadataId,
                RoomId = roomId,
                SenderId = request.SenderId,
                Direction = FileTransferDirection.Download,
                FileName = request.FileName,
            };

            fileTransferService.RegisterTransfer(info);
        }
        else
        {
            LogInfo($"Transfer {request.TransferId} уже зарегистрирован, начинаю download");
        }

        var filePath = ResolveFilePath(context, request.FileMetadataId, roomId);
        if (filePath == null)
        {
            LogError($"Файл не найден для download: {request.FileName} (TransferId: {request.TransferId})");
            var errorResponse = new FileTransferResponseMessage
            {
                TransferId = request.TransferId,
                Success = false,
                ErrorMessage = "Файл не найден у хоста",
            };

            await messageSender.SendAsync(errorResponse, roomId, context.ConnectionId, context.CancellationToken);
            return HandlerResult.Failure("Файл не найден у хоста", stopPropagation: false, showErrorMessage: false);
        }

        LogInfo($"Начинаю download файла {request.FileName} из: {filePath}");

        var response = new FileTransferResponseMessage
        {
            TransferId = request.TransferId,
            Success = true,
        };

        await messageSender.SendAsync(response, roomId, context.ConnectionId, context.CancellationToken);

        await StreamFileAsync(filePath, request, context);

        return HandlerResult.Success(stopPropagation: true);
    }

    private string? ResolveFilePath(MessageContext context, Guid fileMetadataId, Guid roomId)
    {
        if (fileTransferService.TryGetFileMetadata(fileMetadataId, out var info))
        {
            return ResolveFromInfo(info);
        }

        if (context.RoomContext.Messages.GetMessageById(fileMetadataId) is not FileMetadataMessage metadata)
        {
            return null;
        }

        if (metadata.AsDownloaded)
        {
            return fileStorageService.GetFilePath(roomId, metadata.FileName);
        }

        return metadata.FilePath != null && File.Exists(metadata.FilePath) ? metadata.FilePath : null;
    }

    private string? ResolveFromInfo(FileMetadataInfo info)
    {
        if (info.IsStoredOnServer)
        {
            var filePath = fileStorageService.GetFilePath(info.RoomId, info.FileName);
            if (filePath != null)
            {
                LogInfo($"Файл найден в хранилище: {filePath}");
                return filePath;
            }
        }

        var originalPath = info.OriginalPath;

        if (originalPath != null && File.Exists(originalPath))
        {
            LogInfo($"Файл найден по оригинальному пути: {originalPath}");
            return originalPath;
        }

        return null;
    }

    private async Task StreamFileAsync(string filePath, FileTransferRequestMessage request, MessageContext context)
    {
        var roomId = context.RoomContext.RoomId;

        if (!fileTransferService.TryGetTransferInfo(request.TransferId, out var info))
        {
            LogWarning($"Не найдена информация о передаче {request.TransferId} во время стриминга файла");
            return;
        }

        info.CancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);

        var fileSize = new FileInfo(filePath).Length;
        LogInfo($"Начинаю стриминг файла {Path.GetFileName(filePath)} ({fileSize} байт)");

        try
        {
            await using var fileStream = File.OpenRead(filePath);
            await messageSender.SendStreamAsync(fileStream, request.TransferId, roomId, context.ConnectionId, info.CancellationTokenSource.Token);
        }
        catch (Exception ex)
        {
            LogError($"Произошла ошибка во время самой передачи файла с id {request.TransferId}");

            await eventBus.PublishAsync(new FileTransferFailedEvent
            {
                RoomId = info.RoomId,
                SenderId = context.SelfId,
                FileMetadataId = info.FileMetadataId,
                ErrorMessage = ex.Message,
            });

            fileTransferService.RemoveTransfer(info.TransferId);
        }
    }
}
