using MIN.Core.Handlers.Contracts.Base;
using MIN.Core.Handlers.Contracts.Models;
using MIN.Core.Messaging.Contracts;
using MIN.Core.Messaging.Contracts.Interfaces;
using MIN.Core.Services.Contracts.Interfaces.Persistence;
using MIN.FileTransfer.Messaging;
using MIN.FileTransfer.Services.Contracts.Interfaces;
using MIN.Helpers.Contracts.Interfaces;

namespace MIN.FileTransfer.Handlers;

internal sealed class FileTransferCompleteHandler : BaseHandler
{
    private readonly IFileTransferService fileTransferService;
    private readonly IRoomPersistenceService persistenceService;

    public FileTransferCompleteHandler(IFileTransferService fileTransferService,
        IRoomPersistenceService persistenceService,
        ILoggerProvider logger) : base(logger)
    {
        this.fileTransferService = fileTransferService;
        this.persistenceService = persistenceService;
    }

    public override IEnumerable<MessageTypeTag> HandledTypes => [MessageTypeTag.FileTransferComplete];

    protected override Task<HandlerResult> HandleAsync(IMessage message, MessageContext context)
    {
        var complete = (FileTransferCompleteMessage)message;
        LogInfo($"Transfer {complete.TransferId} завершён, очищаю информацию");
        fileTransferService.RemoveTransfer(complete.TransferId);
        persistenceService.MarkDirty(context.RoomContext.RoomId);
        return Task.FromResult(HandlerResult.Success());
    }
}
