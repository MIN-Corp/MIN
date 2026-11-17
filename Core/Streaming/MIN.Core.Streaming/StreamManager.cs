using System.Buffers;
using System.Collections.Concurrent;
using MIN.Core.Cryptography.Contracts.Constants;
using MIN.Core.Cryptography.Contracts.Interfaces;
using MIN.Core.Headers.Contracts.Constants;
using MIN.Core.Headers.Contracts.Enums;
using MIN.Core.Headers.Contracts.Interfaces;
using MIN.Core.Messaging.Contracts.Enums;
using MIN.Core.Stores.Contracts.Interfaces;
using MIN.Core.Streaming.Contracts.Constants;
using MIN.Core.Streaming.Contracts.Events;
using MIN.Core.Streaming.Contracts.Events.Sending;
using MIN.Core.Streaming.Contracts.Interfaces;
using MIN.Core.Streaming.Contracts.Models;
using MIN.Core.Transport.Contracts.Interfaces;
using MIN.Helpers.Contracts.Interfaces;
using MIN.Helpers.Contracts.Models.Enums;

namespace MIN.Core.Streaming;

/// <inheritdoc cref="IStreamManager"/>
public sealed class StreamManager : IStreamManager, IDisposable
{
    private readonly ITransport transport;
    private readonly IMessageEncryptor encryptor;
    private readonly IHeaderManager headerManager;
    private readonly IRoomFactory roomFactory;
    private readonly ILoggerProvider logger;
    private readonly ConcurrentDictionary<ChunkAckKey, PendingChunk> pendingChunks = new();
    private readonly ConcurrentDictionary<ChunkAckKey, Timer> ackTimers = new();
    private bool disposed;

    /// <inheritdoc />
    public event Func<ChunkSendedEventArgs, CancellationToken, Task>? ChunkSended;

    /// <inheritdoc />
    public event Func<StreamCompletedEventArgs, CancellationToken, Task>? OnStreamCompleted;

    /// <inheritdoc />
    public event Func<StreamFailedEventArgs, CancellationToken, Task>? OnStreamFailed;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="StreamManager"/>
    /// </summary>
    public StreamManager(ITransport transport,
        IMessageEncryptor encryptor,
        IHeaderManager headerManager,
        IRoomFactory roomFactory,
        ILoggerProvider logger)
    {
        this.transport = transport;
        this.encryptor = encryptor;
        this.headerManager = headerManager;
        this.roomFactory = roomFactory;
        this.logger = logger;
    }

    async Task IStreamManager.SendAsync(ReadOnlyMemory<byte> data,
        StreamOptions options,
        Guid roomId,
        Guid recipientConnectionId,
        Guid? serverConnectionId,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(disposed, nameof(StreamManager));

        var streamId = options.StreamId ?? Guid.NewGuid();
        var totalChunks = Math.Max(1, (int)Math.Ceiling((double)data.Length / StreamingConstants.ChunkDataSize));

        logger.Log($"Начало отправки потока {streamId}: {data.Length} байт, {totalChunks} пакетов");

        try
        {
            long sentLength = 0;

            for (var i = 0; i < totalChunks; i++)
            {
                var chunkStart = i * StreamingConstants.ChunkDataSize;
                var chunkLength = Math.Min(StreamingConstants.ChunkDataSize, data.Length - chunkStart);

                var chunk = new StreamChunk
                {
                    StreamId = streamId,
                    Flags = BuildChunkFlags(i, totalChunks, options),
                    Index = i,
                    Total = totalChunks,
                    Data = data.Slice(chunkStart, chunkLength)
                };

                if (options.RequiresAcks)
                {
                    var ackKey = new ChunkAckKey { StreamId = streamId, ChunkIndex = i };
                    pendingChunks.TryAdd(ackKey, new PendingChunk()
                    {
                        LastAcknowledgedIndex = i,
                        TotalChunks = totalChunks,
                    });
                }

                sentLength += chunkLength;
                await SendChunkCoreAsync(chunk, recipientConnectionId, roomId, serverConnectionId, options, sentLength, cancellationToken);
            }

            logger.Log($"Передача пакетов окончена");
            OnStreamCompleted?.Invoke(new StreamCompletedEventArgs()
            {
                RoomId = roomId,
                StreamId = streamId,
                IsRawPayload = false,
            }, cancellationToken);
        }
        catch (OperationCanceledException ex)
        {
            logger.Log($"Передача пакетов была отменена: {ex.Message}", LogLevel.Warning);
            OnStreamFailed?.Invoke(new StreamFailedEventArgs()
            {
                RoomId = roomId,
                StreamId = streamId,
                ErrorMessage = "Передача была отменена"
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.Log($"Передача пакетов провалилась: {ex.Message}", LogLevel.Error);
            OnStreamFailed?.Invoke(new StreamFailedEventArgs()
            {
                RoomId = roomId,
                StreamId = streamId,
                ErrorMessage = "Передача пакетов провалилась"
            }, cancellationToken);
        }
        finally
        {
            CleanForStream(streamId);
        }
    }

    async Task IStreamManager.SendAsync(Stream source,
        StreamOptions options,
        Guid roomId,
        Guid recipientConnectionId,
        Guid? serverConnectionId,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(disposed, nameof(StreamManager));

        var streamId = options.StreamId ?? Guid.NewGuid();
        var totalChunks = (int)Math.Ceiling((double)source.Length / StreamingConstants.ChunkDataSize);
        var readBuffer = ArrayPool<byte>.Shared.Rent(StreamingConstants.ChunkDataSize);

        logger.Log($"Начало отправки потока {streamId}: {source.Length} байт, {totalChunks} пакетов (streaming)");

        try
        {
            var chunkIndex = 0;
            long sentLength = 0;
            while (true)
            {
                // Добираем чанк до полного размера: частичные чтения не должны ломать нумерацию пакетов
                var bytesRead = 0;
                while (bytesRead < StreamingConstants.ChunkDataSize)
                {
                    var read = await source.ReadAsync(readBuffer.AsMemory(bytesRead, StreamingConstants.ChunkDataSize - bytesRead), cancellationToken);
                    if (read == 0)
                    {
                        break;
                    }

                    bytesRead += read;
                }

                if (bytesRead == 0)
                {
                    break;
                }

                sentLength += bytesRead;

                var chunk = new StreamChunk
                {
                    StreamId = streamId,
                    Flags = BuildChunkFlags(chunkIndex, totalChunks, options),
                    Index = chunkIndex,
                    Total = totalChunks,
                    Data = readBuffer.AsMemory(0, bytesRead)
                };

                if (options.RequiresAcks)
                {
                    var ackKey = new ChunkAckKey { StreamId = streamId, ChunkIndex = chunkIndex };
                    pendingChunks.TryAdd(ackKey, new PendingChunk()
                    {
                        LastAcknowledgedIndex = chunkIndex,
                        TotalChunks = totalChunks,
                    });
                }

                await SendChunkCoreAsync(chunk, recipientConnectionId, roomId, serverConnectionId, options, sentLength, cancellationToken);
                chunkIndex++;
            }

            if (chunkIndex == 0)
            {
                var chunk = new StreamChunk
                {
                    StreamId = streamId,
                    Flags = BuildChunkFlags(0, 1, options),
                    Index = 0,
                    Total = 1,
                    Data = ReadOnlyMemory<byte>.Empty
                };
                await SendChunkCoreAsync(chunk, recipientConnectionId, roomId, serverConnectionId, options, 0, cancellationToken);
            }

            logger.Log($"Передача пакетов окончена");
            OnStreamCompleted?.Invoke(new StreamCompletedEventArgs()
            {
                RoomId = roomId,
                StreamId = streamId,
                IsRawPayload = true,
            }, cancellationToken);
        }
        catch (OperationCanceledException ex)
        {
            logger.Log($"Передача пакетов была отменена: {ex.Message}", LogLevel.Warning);
            OnStreamFailed?.Invoke(new StreamFailedEventArgs()
            {
                RoomId = roomId,
                StreamId = streamId,
                ErrorMessage = "Передача была отменена"
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.Log($"Передача пакетов провалилась: {ex.Message}", LogLevel.Warning);
            OnStreamFailed?.Invoke(new StreamFailedEventArgs()
            {
                RoomId = roomId,
                StreamId = streamId,
                ErrorMessage = "Передача пакетов провалилась"
            }, cancellationToken);
        }
        finally
        {
            CleanForStream(streamId);
            ArrayPool<byte>.Shared.Return(readBuffer);
        }
    }

    private void CleanForStream(Guid streamId)
    {
        foreach (var key in pendingChunks.Keys.Where(x => x.StreamId == streamId).ToList())
        {
            pendingChunks.TryRemove(key, out _);
        }

        foreach (var key in ackTimers.Keys.Where(x => x.StreamId == streamId).ToList())
        {
            if (ackTimers.TryRemove(key, out var timer))
            {
                timer.Dispose();
            }
        }
    }

    void IStreamManager.ProcessAck(byte[] data)
    {
        if (!headerManager.IsAck(data))
        {
            return;
        }

        var streamId = new Guid(data.AsSpan(1, 16));
        var chunkIndex = BitConverter.ToInt32(data, 17);
        var ackKey = new ChunkAckKey { StreamId = streamId, ChunkIndex = chunkIndex };

        OnChunkAcknowledged(ackKey);
    }

    private void OnChunkAcknowledged(ChunkAckKey ackKey)
    {
        if (ackTimers.TryRemove(ackKey, out var timer))
        {
            timer.Dispose();
        }

        if (pendingChunks.TryGetValue(ackKey, out var pending))
        {
            pending.LastAcknowledgedIndex = ackKey.ChunkIndex;
        }
    }

    private static StreamChunkFlags BuildChunkFlags(int index, int total, StreamOptions options)
    {
        var flags = StreamChunkFlags.Mid;

        if (index == 0)
        {
            flags |= StreamChunkFlags.Start;
            if (options.RequiresAcks)
            {
                flags |= StreamChunkFlags.RequiresAcks;
            }
            if (options.IsRawPayload)
            {
                flags |= StreamChunkFlags.RawPayload;
            }
        }

        if (index == total - 1)
        {
            flags |= StreamChunkFlags.End;
        }

        return flags;
    }

    private async Task SendChunkCoreAsync(StreamChunk chunk,
        Guid recipientConnectionId,
        Guid roomId,
        Guid? serverConnectionId,
        StreamOptions options,
        long sentBytes,
        CancellationToken cancellationToken)
    {
        var plainLen = HeadersConstants.StreamHeaderSize + chunk.Data.Length;
        var totalLen = options.RequiresEncryption
            ? 1 + CryptographyConstants.IvSize + plainLen + CryptographyConstants.AuthTagSize
            : 1 + plainLen;

        var buffer = ArrayPool<byte>.Shared.Rent(totalLen);
        try
        {
            var span = buffer.AsSpan(0, totalLen);
            span[0] = (byte)(options.RequiresEncryption ? HeaderMessageType.Encrypted : HeaderMessageType.Plain);

            var headerTarget = options.RequiresEncryption
                ? span.Slice(1 + CryptographyConstants.IvSize, plainLen)
                : span.Slice(1, plainLen);

            headerManager.WriteStreamChunkHeader(headerTarget, chunk.Flags, chunk.StreamId, chunk.Index, chunk.Total);
            chunk.Data.Span.CopyTo(headerTarget[HeadersConstants.StreamHeaderSize..]);

            if (options.RequiresEncryption)
            {
                var recipientId = roomFactory.GetOrCreateContext(roomId)
                    .Connections.GetParticipantIdFromConnectionId(recipientConnectionId);
                encryptor.EncryptMessageInPlace(span.Slice(1), recipientId);
            }

            // Secure because of message assembling
            await transport.SendAsync(buffer.AsMemory(0, totalLen), recipientConnectionId, serverConnectionId, MessageChannel.Secure, cancellationToken);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        ChunkSended?.Invoke(new ChunkSendedEventArgs()
        {
            RoomId = roomId,
            StreamId = chunk.StreamId,
            ReceivedBytes = sentBytes,
        }, cancellationToken);
    }

    /// <inheritdoc cref="IDisposable.Dispose"/>
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;

        foreach (var timer in ackTimers.Values)
        {
            timer.Dispose();
        }

        ackTimers.Clear();
        pendingChunks.Clear();
    }
}
