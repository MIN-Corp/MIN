using MIN.Core.Events.Contracts.Interfaces;
using MIN.Core.Events.Contracts.Models;

namespace MIN.FileTransfer.Events;

/// <summary>
/// Файл получен полностью и собирается (перенос из временного хранилища)
/// </summary>
public sealed record FileTransferAssemblingEvent : BaseEvent, IRoomScopedEvent
{
    /// <inheritdoc />
    public Guid RoomId { get; init; }

    /// <summary>
    /// Идентификатор сообщения метаданных
    /// </summary>
    public required Guid FileMetadataId { get; set; }

    /// <summary>
    /// Идентификатор инициатора файла
    /// </summary>
    public required Guid SenderId { get; set; }
}
