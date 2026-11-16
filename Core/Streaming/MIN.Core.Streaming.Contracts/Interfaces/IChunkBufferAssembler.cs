using MIN.Core.Streaming.Contracts.Events;
using MIN.Core.Streaming.Contracts.Events.Receiving;

namespace MIN.Core.Streaming.Contracts.Interfaces;

/// <summary>
/// Ассемблер пакетов в цельное сообщение
/// </summary>
public interface IChunkBufferAssembler
{
    /// <summary>
    /// Событие завершения сборки сообщения
    /// </summary>
    event Func<MessageAssembledEventArgs, CancellationToken, Task>? MessageAssembled;

    /// <summary>
    /// Событие запроса на отправку ACK
    /// </summary>
    event Func<ChunkReceivedEventArgs, CancellationToken, Task>? ChunkReceived;

    /// <summary>
    /// Поток провалился
    /// </summary>
    event Func<StreamFailedEventArgs, CancellationToken, Task>? OnStreamFailed;

    /// <summary>
    /// Обрабатывает входящий пакет
    /// </summary>
    Task ProcessStreamChunk(ReadOnlyMemory<byte> data, Guid roomId, Guid connectionId, Guid? serverConnectionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Очистить поток передачи данных
    /// </summary>
    bool TryRemoveStream(Guid streamId);
}
