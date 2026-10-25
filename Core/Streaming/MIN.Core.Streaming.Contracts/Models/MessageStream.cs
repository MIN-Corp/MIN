using MIN.Core.Headers.Contracts.Enums;
using MIN.Core.Streaming.Contracts.Constants;

namespace MIN.Core.Streaming.Contracts.Models;

/// <summary>
/// Состояние собираемого потока
/// </summary>
public sealed class MessageStream : IDisposable
{
    private readonly byte[]? memoryBuffer;
    private readonly FileStream? fileStream;
    private readonly string? tempFilePath;
    private readonly HashSet<int> receivedIndices = [];
    private readonly object lockObj = new();
    private long totalDataSize;
    private bool disposed;

    /// <summary>
    /// Идентификатор потока
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// Идентификатор комнаты
    /// </summary>
    public Guid RoomId { get; }

    /// <summary>
    /// Идентификатор соединения отправителя
    /// </summary>
    public Guid ConnectionId { get; }

    /// <summary>
    /// Нужно ли отправлять ACK
    /// </summary>
    public bool RequiresAcks { get; }

    /// <summary>
    /// Содержит ли поток сырые байтовые данные (не JSON)
    /// </summary>
    public bool IsRawPayload { get; }

    /// <summary>
    /// Ожидаемое количество пакетов
    /// </summary>
    private int ExpectedChunks { get; }

    /// <summary>
    /// Уже получено пакетов
    /// </summary>
    private int GottenChunks { get; set; }

    /// <summary>
    /// Сколько полезных байт уже записано
    /// </summary>
    public long TotalDataSize
    {
        get
        {
            lock (lockObj)
            {
                return totalDataSize;
            }
        }
    }

    /// <summary>
    /// Время создания потока
    /// </summary>
    public DateTime CreatedAt { get; }

    /// <summary>
    /// Время последнего полученного пакета
    /// </summary>
    public DateTime LastChunkReceivedAt { get; set; }

    /// <summary>
    /// Завершён ли поток
    /// </summary>
    private bool IsComplete { get; set; }

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="MessageStream"/>
    /// </summary>
    public MessageStream(Guid streamId, Guid roomId, Guid connectionId, int expectedChunks, bool requiresAcks, bool isRawPayload = false)
    {
        Id = streamId;
        RoomId = roomId;
        ConnectionId = connectionId;
        ExpectedChunks = expectedChunks;
        RequiresAcks = requiresAcks;
        IsRawPayload = isRawPayload;
        CreatedAt = DateTime.UtcNow;
        LastChunkReceivedAt = CreatedAt;

        if (isRawPayload)
        {
            tempFilePath = Path.Combine(Path.GetTempPath(), $"min_stream_{Guid.NewGuid()}");
            var bufferSize = (long)expectedChunks * StreamingConstants.ChunkDataSize;
            fileStream = new FileStream(tempFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, bufferSize: 81920);
            fileStream.SetLength(bufferSize);
            totalDataSize = 0;
        }
        else
        {
            memoryBuffer = new byte[ExpectedChunks * StreamingConstants.ChunkDataSize];
        }
    }

    /// <summary>
    /// Добавляет пакет в буфер
    /// </summary>
    public byte[]? AddChunk(StreamChunk chunk)
    {
        lock (lockObj)
        {
            if (disposed || IsComplete)
            {
                return null;
            }

            if (!receivedIndices.Add(chunk.Index))
            {
                return null;
            }

            var offset = chunk.Index * StreamingConstants.ChunkDataSize;
            if (IsRawPayload && fileStream != null)
            {
                fileStream.Position = offset;
                fileStream.Write(chunk.Data.Span);
                totalDataSize += chunk.Data.Length;
            }
            else if (memoryBuffer != null)
            {
                chunk.Data.Span.CopyTo(memoryBuffer.AsSpan(offset));
                totalDataSize += chunk.Data.Length;
            }

            LastChunkReceivedAt = DateTime.UtcNow;
            GottenChunks++;

            if (!chunk.Flags.HasFlag(StreamChunkFlags.End))
            {
                return null;
            }

            if (chunk.Index != ExpectedChunks - 1)
            {
                throw new InvalidOperationException(
                    $"Поток {Id} завершён с неверным индексом. Ожидался {ExpectedChunks - 1}, получен {chunk.Index}");
            }

            IsComplete = true;

            if (!IsRawPayload || fileStream == null)
            {
                return memoryBuffer![..(int)totalDataSize].ToArray();
            }

            fileStream.SetLength(totalDataSize);
            fileStream?.Dispose();
            return [];

        }
    }

    /// <summary>
    /// Получить путь к временному файлу (только для raw payload)
    /// </summary>
    public string? GetTempFilePath() => tempFilePath;

    /// <inheritdoc/>
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }
        disposed = true;
        fileStream?.Dispose();

        if (tempFilePath != null && File.Exists(tempFilePath))
        {
            try
            {
                File.Delete(tempFilePath);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
