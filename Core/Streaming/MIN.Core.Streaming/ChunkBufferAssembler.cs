using System.Collections.Concurrent;
using MIN.Core.Headers.Contracts.Constants;
using MIN.Core.Headers.Contracts.Enums;
using MIN.Core.Headers.Contracts.Interfaces;
using MIN.Core.Messaging.Contracts.Enums;
using MIN.Core.Streaming.Contracts.Constants;
using MIN.Core.Streaming.Contracts.Events;
using MIN.Core.Streaming.Contracts.Events.Receiving;
using MIN.Core.Streaming.Contracts.Interfaces;
using MIN.Core.Streaming.Contracts.Models;
using MIN.Core.Transport.Contracts.Interfaces;
using MIN.Helpers.Contracts.Interfaces;

namespace MIN.Core.Streaming;

/// <inheritdoc cref="IChunkBufferAssembler"/>
public sealed class ChunkBufferAssembler : IChunkBufferAssembler, IDisposable
{
    private readonly ConcurrentDictionary<Guid, MessageStream> activeStreams = new();
    private readonly ConcurrentDictionary<Guid, Timer> streamTimers = new();
    private readonly ITransport transport;
    private readonly IHeaderManager headerManager;
    private readonly ILoggerProvider logger;
    private bool disposed;

    /// <inheritdoc />
    public event Func<MessageAssembledEventArgs, CancellationToken, Task>? MessageAssembled;

    /// <inheritdoc />
    public event Func<ChunkReceivedEventArgs, CancellationToken, Task>? ChunkReceived;

    /// <inheritdoc />
    public event Func<StreamFailedEventArgs, CancellationToken, Task>? OnStreamFailed;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="ChunkBufferAssembler"/>
    /// </summary>
    public ChunkBufferAssembler(ITransport transport,
        IHeaderManager headerManager,
        ILoggerProvider logger)
    {
        this.transport = transport;
        this.headerManager = headerManager;
        this.logger = logger;
    }

    async Task IChunkBufferAssembler.ProcessStreamChunk(byte[] data, Guid roomId, Guid connectionId, Guid? serverConnectionId, CancellationToken cancellationToken)
    {
        if (disposed)
        {
            return;
        }

        var header = headerManager.ParseStreamChunkHeader(data);
        var chunkData = new ReadOnlyMemory<byte>(data, HeadersConstants.StreamHeaderSize,
            data.Length - HeadersConstants.StreamHeaderSize);

        var chunk = new StreamChunk
        {
            StreamId = header.StreamId,
            Flags = header.Flags,
            Index = header.Index,
            Total = header.Total,
            Data = chunkData
        };

        try
        {
            if (chunk.IsSingle)
            {
                if (chunk.Flags.HasFlag(StreamChunkFlags.RequiresAcks))
                {
                    await SendAck(chunk.StreamId, chunk.Index, connectionId, serverConnectionId, cancellationToken);
                }

                OnMessageAssembled(chunk.StreamId, roomId, connectionId, chunk.Data.ToArray(), null, chunk.Flags.HasFlag(StreamChunkFlags.RawPayload), cancellationToken);
                return;
            }

            var stream = activeStreams.GetOrAdd(chunk.StreamId, _ =>
                CreateMessageStream(chunk, roomId, connectionId, cancellationToken));

            if (stream.ConnectionId != connectionId)
            {
                return;
            }

            ResetStreamTimer(stream);

            if (chunk.Flags.HasFlag(StreamChunkFlags.Start))
            {
                stream.LastChunkReceivedAt = DateTime.UtcNow;
            }

            if (stream.RequiresAcks)
            {
                await SendAck(chunk.StreamId, chunk.Index, connectionId, serverConnectionId, cancellationToken);
            }

            var result = stream.AddChunk(chunk);

            ChunkReceived?.Invoke(new ChunkReceivedEventArgs
            {
                StreamId = stream.Id,
                ReceivedBytes = stream.TotalDataSize,
                RoomId = roomId,
            }, cancellationToken);

            if (result != null)
            {
                logger.Log("Сообщение было собрано из потока");
                var filePath = stream.GetTempFilePath();
                OnMessageAssembled(chunk.StreamId, roomId, connectionId, result, filePath, stream.IsRawPayload, cancellationToken);
                TryRemoveStream(chunk.StreamId);
            }
        }
        catch (Exception ex)
        {
            logger.Log($"Ошибка при добавлении пакета: {ex.Message}");
            OnStreamFailed?.Invoke(new StreamFailedEventArgs()
            {
                RoomId = roomId,
                StreamId = chunk.StreamId,
                ErrorMessage = ex.Message,
            }, cancellationToken);
            TryRemoveStream(chunk.StreamId);
            throw;
        }
    }

    private MessageStream CreateMessageStream(StreamChunk startChunk, Guid roomId, Guid connectionId, CancellationToken cancellationToken)
    {
        var requiresAcks = startChunk.Flags.HasFlag(StreamChunkFlags.RequiresAcks);
        var isRawPayload = startChunk.Flags.HasFlag(StreamChunkFlags.RawPayload);
        var stream = new MessageStream(startChunk.StreamId, roomId, connectionId, startChunk.Total, requiresAcks, isRawPayload);
        StartStreamTimer(stream, cancellationToken);
        return stream;
    }

    private void StartStreamTimer(MessageStream stream, CancellationToken cancellationToken)
    {
        var timer = new Timer(
            OnStreamTimeout,
            (stream.RoomId, stream.Id, cancellationToken),
            stream.CreatedAt.AddMilliseconds(StreamingConstants.DefaultStreamLifetimeMs) - DateTime.UtcNow,
            Timeout.InfiniteTimeSpan);

        streamTimers.TryAdd(stream.Id, timer);
    }

    private void ResetStreamTimer(MessageStream stream)
    {
        if (streamTimers.TryGetValue(stream.Id, out var existingTimer))
        {
            existingTimer.Change(
                stream.LastChunkReceivedAt.AddMilliseconds(StreamingConstants.DefaultChunkTimeoutMs) - DateTime.UtcNow,
                Timeout.InfiniteTimeSpan);
        }
    }

    private void OnStreamTimeout(object? state)
    {
        if (state is (Guid roomId, Guid streamId, CancellationToken cancellationToken))
        {
            logger.Log($"Поток {streamId} превысил время жизни");
            OnStreamFailed?.Invoke(new StreamFailedEventArgs()
            {
                RoomId = roomId,
                StreamId = streamId,
                ErrorMessage = $"Поток превысил время жизни",
            }, cancellationToken);
            TryRemoveStream(streamId);
        }
    }

    private async Task SendAck(Guid streamId, int chunkIndex, Guid connectionId, Guid? serverConnectionid, CancellationToken cancellationToken)
    {
        try
        {
            var ack = new byte[StreamingConstants.ChunkAckSize];
            ack[0] = (byte)HeaderMessageType.Ack;
            streamId.TryWriteBytes(new Span<byte>(ack, 1, 16));
            BitConverter.GetBytes(chunkIndex).CopyTo(ack, 17);

            // Secure because of message assembling
            await transport.SendAsync(ack, connectionId, serverConnectionid, MessageChannel.Secure, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.Log($"Ошибка при отправке ACK: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public bool TryRemoveStream(Guid streamId)
    {
        var found = false;

        if (activeStreams.TryRemove(streamId, out var stream))
        {
            stream.Dispose();
            found = true;
        }

        if (streamTimers.TryRemove(streamId, out var timer))
        {
            timer.Dispose();
        }

        return found;
    }

    private void OnMessageAssembled(Guid streamId, Guid roomId, Guid connectionId,
        byte[] data, string? filePath, bool isRawPayload, CancellationToken cancellationToken)
    {
        var args = new MessageAssembledEventArgs
        {
            StreamId = streamId,
            RoomId = roomId,
            ConnectionId = connectionId,
            Data = data,
            FilePath = filePath,
            IsRawPayload = isRawPayload,
        };

        MessageAssembled?.Invoke(args, cancellationToken);
    }

    /// <inheritdoc cref="IDisposable.Dispose"/>
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;

        foreach (var stream in activeStreams.Values)
        {
            stream.Dispose();
        }
        activeStreams.Clear();

        foreach (var timer in streamTimers.Values)
        {
            timer.Dispose();
        }
        streamTimers.Clear();
    }
}
