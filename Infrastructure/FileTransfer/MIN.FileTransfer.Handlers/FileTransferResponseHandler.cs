using MIN.Core.Events.Contracts.Interfaces;
using MIN.Core.Handlers.Contracts.Base;
using MIN.Core.Handlers.Contracts.Models;
using MIN.Core.Messaging.Contracts;
using MIN.Core.Messaging.Contracts.Interfaces;
using MIN.FileTransfer.Events;
using MIN.FileTransfer.Messaging;
using MIN.FileTransfer.Services.Contracts.Interfaces;
using MIN.Helpers.Contracts.Interfaces;

namespace MIN.FileTransfer.Handlers;

internal sealed class FileTransferResponseHandler : BaseHandler
{
    private readonly IEventBus eventBus;
    private readonly IFileTransferService fileTransferService;

    public FileTransferResponseHandler(IEventBus eventBus,
        IFileTransferService fileTransferService,
        ILoggerProvider logger) : base(logger)
    {
        this.eventBus = eventBus;
        this.fileTransferService = fileTransferService;
    }

    public override IEnumerable<MessageTypeTag> HandledTypes => [MessageTypeTag.FileTransferResponse];

    protected override async Task<HandlerResult> HandleAsync(IMessage message, MessageContext context)
    {
        var response = (FileTransferResponseMessage)message;

        LogInfo($"Получен FileTransferResponse: TransferId={response.TransferId}, Success={response.Success}");

        if (!response.Success)
        {
            LogError($"Transfer {response.TransferId} завершился ошибкой: {response.ErrorMessage ?? "Unknown error"}");

            if (fileTransferService.TryGetTransferInfo(response.TransferId, out var info))
            {
                await eventBus.PublishAsync(new FileTransferFailedEvent
                {
                    RoomId = info.RoomId,
                    SenderId = context.SelfId,
                    FileMetadataId = info.FileMetadataId,
                    ErrorMessage = response.ErrorMessage ?? "Unknown error",
                });

                fileTransferService.RemoveTransfer(response.TransferId);
            }

            return HandlerResult.Failure(response.ErrorMessage ?? "File transfer failed", stopPropagation: false, showErrorMessage: false);
        }

        if (!fileTransferService.TryGetTransferInfo(response.TransferId, out _))
        {
            LogInfo($"Не найдена информация о transfer {response.TransferId}, но Response успешен — ожидаю пакеты");
        }
        else
        {
            LogInfo($"Transfer {response.TransferId} подтверждён, ожидаю пакеты файла");
        }

        return HandlerResult.Success(stopPropagation: true);
    }
}
