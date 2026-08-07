using System.Collections.Concurrent;
using MIN.Core.Cryptography.Contracts.Interfaces;
using MIN.Core.Headers.Contracts.Enums;
using MIN.Core.Headers.Contracts.Interfaces;
using MIN.Core.Messaging.Contracts.Enums;
using MIN.Core.Stores.Contracts.Interfaces;
using MIN.Core.Streaming.Contracts.Constants;
using MIN.Core.Streaming.Contracts.Interfaces;
using MIN.Core.Streaming.Contracts.Models;
using MIN.Core.Transport.Contracts.Interfaces;
using MIN.Helpers.Contracts.Interfaces;
using MIN.Helpers.Contracts.Models.Enums;

namespace MIN.Core.Streaming.Services;

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
        var totalChunks = (int)Math.Ceiling((double)data.Length / StreamingConstants.ChunkDataSize);

        logger.Log($"Начало отправки потока {streamId}: {data.Length} байт, {totalChunks} пакетов");

        try
        {
            for (var i = 0; i < totalChunks; i++)
            {
                var chunkStart = i * StreamingConstants.ChunkDataSize;
                var chunkLength = Math.Min(StreamingConstants.ChunkDataSize, data.Length - chunkStart);
                var chunkData = data.Slice(chunkStart, chunkLength);

                var flags = StreamChunkFlags.Mid;
                if (i == 0)
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
                if (i == totalChunks - 1)
                {
                    flags |= StreamChunkFlags.End;
                }

                var chunk = new StreamChunk
                {
                    StreamId = streamId,
                    Flags = flags,
                    Index = i,
                    Total = totalChunks,
                    Data = chunkData
                };

                var package = SerializeChunk(chunk);
                var encrypted = EncryptChunkIfNeeded(package, recipientConnectionId, roomId, options);

                if (options.RequiresAcks)
                {
                    var ackKey = new ChunkAckKey { StreamId = streamId, ChunkIndex = i };
                    pendingChunks.TryAdd(ackKey, new PendingChunk()
                    {
                        LastAcknowledgedIndex = i,
                        TotalChunks = totalChunks,
                    });
                }

                // TODO: CHECK CHANNEL
                await transport.SendAsync(encrypted, recipientConnectionId, serverConnectionId, MessageChannel.Secure, cancellationToken);
            }

            logger.Log($"Передача пакетов окончена");
            CleanForStream(streamId);
        }
        catch (TaskCanceledException ex)
        {
            logger.Log($"Передача пакетов была отменена: {ex.Message}", LogLevel.Warning);
            CleanForStream(streamId);
        }
    }

    async Task IStreamManager.SendAsync(
        Stream source,
        StreamOptions options,
        Guid roomId,
        Guid recipientConnectionId,
        Guid? serverConnectionId,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(disposed, nameof(StreamManager));

        var streamId = options.StreamId ?? Guid.NewGuid();
        var totalChunks = (int)Math.Ceiling((double)source.Length / StreamingConstants.ChunkDataSize);
        var buffer = new byte[StreamingConstants.ChunkDataSize];

        logger.Log($"Начало отправки потока {streamId}: {source.Length} байт, {totalChunks} пакетов (streaming)");

        try
        {
            var chunkIndex = 0;
            int bytesRead;

            while ((bytesRead = await source.ReadAsync(buffer, cancellationToken)) > 0)
            {
                var chunkData = buffer.AsMemory(0, bytesRead);

                var flags = StreamChunkFlags.Mid;
                if (chunkIndex == 0)
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
                if (chunkIndex == totalChunks - 1)
                {
                    flags |= StreamChunkFlags.End;
                }

                var chunk = new StreamChunk
                {
                    StreamId = streamId,
                    Flags = flags,
                    Index = chunkIndex,
                    Total = totalChunks,
                    Data = chunkData
                };

                var package = SerializeChunk(chunk);
                var encrypted = EncryptChunkIfNeeded(package, recipientConnectionId, roomId, options);

                if (options.RequiresAcks)
                {
                    var ackKey = new ChunkAckKey { StreamId = streamId, ChunkIndex = chunkIndex };
                    pendingChunks.TryAdd(ackKey, new PendingChunk()
                    {
                        LastAcknowledgedIndex = chunkIndex,
                        TotalChunks = totalChunks,
                    });
                }

                // TODO: CHECK CHANNEL
                await transport.SendAsync(encrypted, recipientConnectionId, serverConnectionId, MessageChannel.Secure, cancellationToken);
                chunkIndex++;
            }

            CleanForStream(streamId);
        }
        catch (TaskCanceledException ex)
        {
            logger.Log($"Передача пакетов была отменена: {ex.Message}");
            CleanForStream(streamId);
        }
    }

    private void CleanForStream(Guid streamId)
    {
        foreach (var key in pendingChunks.Keys.Where(x => x.StreamId == streamId).ToList())
        {
            pendingChunks.TryRemove(key, out var pendingChunk);
        }

        foreach (var key in ackTimers.Keys.Where(x => x.StreamId == streamId).ToList())
        {
            if (ackTimers.TryRemove(key, out var timer))
            {
                timer?.Dispose();
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

    private byte[] SerializeChunk(StreamChunk chunk)
    {
        var header = headerManager.BuildStreamChunkHeader(chunk.Flags, chunk.StreamId, chunk.Index, chunk.Total);
        var result = new byte[header.Length + chunk.Data.Length];
        header.CopyTo(result, 0);
        chunk.Data.CopyTo(result.AsMemory(header.Length));
        return result;
    }

    private byte[] EncryptChunkIfNeeded(byte[] plainData, Guid recipientConnectionId, Guid roomId, StreamOptions options)
    {
        byte[] resultBytes;
        if (options.RequiresEncryption)
        {
            var recipientId = roomFactory.GetOrCreateContext(roomId).Connections.GetParticipantIdFromConnectionId(recipientConnectionId);
            var encrypted = encryptor.EncryptMessage(plainData, recipientId);
            resultBytes = headerManager.AddHeader(encrypted, (byte)HeaderMessageType.Encrypted);
        }
        else
        {
            resultBytes = headerManager.AddHeader(plainData, (byte)HeaderMessageType.Plain);
        }
        return resultBytes;
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
