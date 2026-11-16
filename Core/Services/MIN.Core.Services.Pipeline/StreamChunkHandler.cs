using MIN.Core.Headers.Contracts.Interfaces;
using MIN.Core.Services.Contracts.Events;
using MIN.Core.Services.Contracts.Interfaces.Pipeline;
using MIN.Core.Streaming.Contracts.Interfaces;

namespace MIN.Core.Services.Pipeline;

/// <summary>
/// Обработчик пакетов потока
/// </summary>
public sealed class StreamChunkHandler : IStreamChunkHandler
{
    private readonly IHeaderManager headerManager;
    private readonly IChunkBufferAssembler chunkBufferAssembler;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="StreamChunkHandler"/>
    /// </summary>
    public StreamChunkHandler(IHeaderManager headerManager, IChunkBufferAssembler chunkBufferAssembler)
    {
        this.headerManager = headerManager;
        this.chunkBufferAssembler = chunkBufferAssembler;
    }

    bool IStreamChunkHandler.CanHandle(ReadOnlySpan<byte> plainData) => headerManager.IsStreamChunk(plainData);

    Task IStreamChunkHandler.HandleAsync(ReadOnlyMemory<byte> plainData, RoomRawMessageReceivedEventArgs e, CancellationToken cancellationToken)
        => chunkBufferAssembler.ProcessStreamChunk(plainData, e.RoomId, e.ConnectionId, e.ServerConnectionId, cancellationToken);
}
