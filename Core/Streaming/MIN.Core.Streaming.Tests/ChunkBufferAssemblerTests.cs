using System.Collections.Concurrent;
using FluentAssertions;
using MIN.Core.Headers.Contracts.Enums;
using MIN.Core.Headers.Contracts.Interfaces;
using MIN.Core.Headers.Services;
using MIN.Core.Streaming.Contracts.Constants;
using MIN.Core.Streaming.Contracts.Events;
using MIN.Core.Streaming.Contracts.Events.Receiving;
using MIN.Core.Streaming.Contracts.Interfaces;
using MIN.Tests.Common;
using Xunit;

namespace MIN.Core.Streaming.Tests;

/// <summary>
/// Тесты на <see cref="ChunkBufferAssembler"/>
/// </summary>
public class ChunkBufferAssemblerTests
{
    private readonly TestLoggerProvider logger = new();
    private readonly IHeaderManager headerManager = new HeaderManager();
    private readonly InMemoryTransport transport = new();
    private readonly IChunkBufferAssembler assembler;
    private readonly ConcurrentQueue<MessageAssembledEventArgs> assembled = new();
    private readonly ConcurrentQueue<StreamFailedEventArgs> failed = new();
    private readonly Guid roomId = Guid.NewGuid();
    private readonly Guid connectionId = Guid.NewGuid();

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="ChunkBufferAssemblerTests"/>
    /// </summary>
    public ChunkBufferAssemblerTests()
    {
        var instance = new ChunkBufferAssembler(transport, headerManager, logger);
        instance.MessageAssembled += (e, _) => { assembled.Enqueue(e); return Task.CompletedTask; };
        instance.OnStreamFailed += (e, _) => { failed.Enqueue(e); return Task.CompletedTask; };
        assembler = instance;
    }

    /// <summary>
    /// Собрать байты чанка: заголовок + payload
    /// </summary>
    private byte[] BuildChunk(Guid streamId, StreamChunkFlags flags, int index, int total, byte[] data)
    {
        var header = headerManager.BuildStreamChunkHeader(flags, streamId, index, total);
        return header.Concat(data).ToArray();
    }

    /// <summary>
    /// Одночанковое сообщение собирается сразу
    /// </summary>
    [Fact]
    public async Task SingleChunkShouldAssemble()
    {
        // Arrange
        var streamId = Guid.NewGuid();
        var data = new byte[] { 1, 2, 3 };
        var chunk = BuildChunk(streamId, StreamChunkFlags.Start | StreamChunkFlags.Mid | StreamChunkFlags.End, 0, 1, data);

        // Act
        await assembler.ProcessStreamChunk(chunk, roomId, connectionId, null, CancellationToken.None);

        // Assert
        assembled.Should().HaveCount(1);
        assembled.Single().Data.Should().Equal(data);
        assembled.Single().StreamId.Should().Be(streamId);
        assembled.Single().ConnectionId.Should().Be(connectionId);
        assembled.Single().IsRawPayload.Should().BeFalse();
    }

    /// <summary>
    /// Многочанковое сообщение собирается из чанков не по порядку
    /// </summary>
    [Fact]
    public async Task MultiChunkOutOfOrderShouldAssemble()
    {
        // Arrange
        var streamId = Guid.NewGuid();
        var part0 = Enumerable.Repeat((byte)1, StreamingConstants.ChunkDataSize).ToArray();
        var part1 = Enumerable.Repeat((byte)2, StreamingConstants.ChunkDataSize).ToArray();
        var part2 = new byte[] { 3 };
        var chunk0 = BuildChunk(streamId, StreamChunkFlags.Start | StreamChunkFlags.Mid, 0, 3, part0);
        var chunk1 = BuildChunk(streamId, StreamChunkFlags.Mid, 1, 3, part1);
        var chunk2 = BuildChunk(streamId, StreamChunkFlags.Mid | StreamChunkFlags.End, 2, 3, part2);

        // Act
        await assembler.ProcessStreamChunk(chunk1, roomId, connectionId, null, CancellationToken.None);
        await assembler.ProcessStreamChunk(chunk0, roomId, connectionId, null, CancellationToken.None);
        await assembler.ProcessStreamChunk(chunk2, roomId, connectionId, null, CancellationToken.None);

        // Assert
        assembled.Should().HaveCount(1);
        assembled.Single().Data.Should().Equal(part0.Concat(part1).Concat(part2));
    }

    /// <summary>
    /// Дубликаты чанков не приводят к повторной сборке
    /// </summary>
    [Fact]
    public async Task DuplicateChunksShouldNotDoubleAssemble()
    {
        // Arrange
        var streamId = Guid.NewGuid();
        var part0 = Enumerable.Repeat((byte)1, StreamingConstants.ChunkDataSize).ToArray();
        var chunk0 = BuildChunk(streamId, StreamChunkFlags.Start | StreamChunkFlags.Mid, 0, 2, part0);
        var chunk1 = BuildChunk(streamId, StreamChunkFlags.Mid | StreamChunkFlags.End, 1, 2, [2]);

        // Act
        await assembler.ProcessStreamChunk(chunk0, roomId, connectionId, null, CancellationToken.None);
        await assembler.ProcessStreamChunk(chunk0, roomId, connectionId, null, CancellationToken.None);
        await assembler.ProcessStreamChunk(chunk1, roomId, connectionId, null, CancellationToken.None);
        await assembler.ProcessStreamChunk(chunk1, roomId, connectionId, null, CancellationToken.None);

        // Assert
        assembled.Should().HaveCount(1);
        assembled.Single().Data.Should().Equal(part0.Concat(new byte[] { 2 }));
    }

    /// <summary>
    /// Дубликат одночанкового сообщения не доставляется повторно
    /// </summary>
    [Fact]
    public async Task DuplicateSingleChunkShouldNotDoubleDeliver()
    {
        // Arrange
        var streamId = Guid.NewGuid();
        var chunk = BuildChunk(streamId, StreamChunkFlags.Start | StreamChunkFlags.Mid | StreamChunkFlags.End, 0, 1, [1, 2, 3]);

        // Act
        await assembler.ProcessStreamChunk(chunk, roomId, connectionId, null, CancellationToken.None);
        await assembler.ProcessStreamChunk(chunk, roomId, connectionId, null, CancellationToken.None);

        // Assert
        assembled.Should().HaveCount(1);
    }

    /// <summary>
    /// Дубликат одночанкового сообщения с RequiresAcks повторяет ACK, чтобы отправитель не завис
    /// </summary>
    [Fact]
    public async Task DuplicateSingleChunkWithAcksShouldResendAck()
    {
        // Arrange
        var streamId = Guid.NewGuid();
        var chunk = BuildChunk(streamId,
            StreamChunkFlags.Start | StreamChunkFlags.Mid | StreamChunkFlags.End | StreamChunkFlags.RequiresAcks,
            0, 1, [1, 2, 3]);

        // Act
        await assembler.ProcessStreamChunk(chunk, roomId, connectionId, null, CancellationToken.None);
        await assembler.ProcessStreamChunk(chunk, roomId, connectionId, null, CancellationToken.None);

        // Assert
        assembled.Should().HaveCount(1);
        transport.SentDatagrams.Should().HaveCount(2);
        transport.SentDatagrams.Should().OnlyContain(a =>
            a.Data[0] == (byte)HeaderMessageType.Ack && a.RecipientConnectionId == connectionId);
        transport.SentDatagrams.Select(a => BitConverter.ToInt32(a.Data, 17)).Should().Equal(0, 0);
    }

    /// <summary>
    /// Дубликат финального чанка после сборки повторяет ACK и не доставляет сообщение повторно
    /// </summary>
    [Fact]
    public async Task DuplicateFinalChunkAfterAssemblyShouldResendAck()
    {
        // Arrange
        var streamId = Guid.NewGuid();
        var chunk0 = BuildChunk(streamId, StreamChunkFlags.Start | StreamChunkFlags.Mid | StreamChunkFlags.RequiresAcks, 0, 2, [1]);
        var chunk1 = BuildChunk(streamId, StreamChunkFlags.Mid | StreamChunkFlags.End | StreamChunkFlags.RequiresAcks, 1, 2, [2]);

        // Act
        await assembler.ProcessStreamChunk(chunk0, roomId, connectionId, null, CancellationToken.None);
        await assembler.ProcessStreamChunk(chunk1, roomId, connectionId, null, CancellationToken.None);
        await assembler.ProcessStreamChunk(chunk1, roomId, connectionId, null, CancellationToken.None);

        // Assert
        assembled.Should().HaveCount(1);
        transport.SentDatagrams.Should().HaveCount(3);
        transport.SentDatagrams.Select(a => BitConverter.ToInt32(a.Data, 17)).Should().Equal(0, 1, 1);
    }

    /// <summary>
    /// Чанки от другого соединения для того же потока игнорируются
    /// </summary>
    [Fact]
    public async Task ForeignConnectionChunksShouldBeIgnored()
    {
        // Arrange
        var streamId = Guid.NewGuid();
        var stranger = Guid.NewGuid();
        var part0 = Enumerable.Repeat((byte)1, StreamingConstants.ChunkDataSize).ToArray();
        var part1 = Enumerable.Repeat((byte)2, StreamingConstants.ChunkDataSize).ToArray();
        var foreignPart1 = Enumerable.Repeat((byte)99, StreamingConstants.ChunkDataSize).ToArray();
        var chunk0 = BuildChunk(streamId, StreamChunkFlags.Start | StreamChunkFlags.Mid, 0, 3, part0);
        var foreignChunk1 = BuildChunk(streamId, StreamChunkFlags.Mid, 1, 3, foreignPart1);
        var chunk1 = BuildChunk(streamId, StreamChunkFlags.Mid, 1, 3, part1);
        var chunk2 = BuildChunk(streamId, StreamChunkFlags.Mid | StreamChunkFlags.End, 2, 3, [3]);

        // Act
        await assembler.ProcessStreamChunk(chunk0, roomId, connectionId, null, CancellationToken.None);
        await assembler.ProcessStreamChunk(foreignChunk1, roomId, stranger, null, CancellationToken.None);
        await assembler.ProcessStreamChunk(chunk1, roomId, connectionId, null, CancellationToken.None);
        await assembler.ProcessStreamChunk(chunk2, roomId, connectionId, null, CancellationToken.None);

        // Assert
        assembled.Should().HaveCount(1);
        assembled.Single().Data.Should().Equal(part0.Concat(part1).Concat(new byte[] { 3 }));
    }

    /// <summary>
    /// RequiresAcks порождает ACK на каждый чанк
    /// </summary>
    [Fact]
    public async Task RequiresAcksShouldSendAckPerChunk()
    {
        // Arrange
        var streamId = Guid.NewGuid();
        var chunk0 = BuildChunk(streamId, StreamChunkFlags.Start | StreamChunkFlags.Mid | StreamChunkFlags.RequiresAcks, 0, 2, [1]);
        var chunk1 = BuildChunk(streamId, StreamChunkFlags.Mid | StreamChunkFlags.End, 1, 2, [2]);

        // Act
        await assembler.ProcessStreamChunk(chunk0, roomId, connectionId, null, CancellationToken.None);
        await assembler.ProcessStreamChunk(chunk1, roomId, connectionId, null, CancellationToken.None);

        // Assert
        transport.SentDatagrams.Should().HaveCount(2);
        var acks = transport.SentDatagrams.ToArray();
        acks.Should().OnlyContain(a => a.Data[0] == (byte)HeaderMessageType.Ack && a.RecipientConnectionId == connectionId);
        acks.Select(a => new Guid(a.Data.AsSpan(1, 16))).Should().OnlyContain(id => id == streamId);
        acks.Select(a => BitConverter.ToInt32(a.Data, 17)).Should().Equal(0, 1);
    }

    /// <summary>
    /// End с неверным индексом проваливает поток
    /// </summary>
    [Fact]
    public async Task EndWithWrongIndexShouldFailStream()
    {
        // Arrange
        var streamId = Guid.NewGuid();
        var chunk0 = BuildChunk(streamId, StreamChunkFlags.Start | StreamChunkFlags.Mid, 0, 3, [1]);
        var badEnd = BuildChunk(streamId, StreamChunkFlags.Mid | StreamChunkFlags.End, 1, 3, [2]);

        // Act
        await assembler.ProcessStreamChunk(chunk0, roomId, connectionId, null, CancellationToken.None);
        var act = () => assembler.ProcessStreamChunk(badEnd, roomId, connectionId, null, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        failed.Should().HaveCount(1);
        assembled.Should().BeEmpty();
    }

    /// <summary>
    /// Пакет короче заголовка отклоняется (текущее поведение)
    /// </summary>
    [Fact]
    public async Task ShortPacketShouldThrow()
    {
        // Act
        var act = () => assembler.ProcessStreamChunk(new byte[10], roomId, connectionId, null, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    /// <summary>
    /// RawPayload-флаг доходит до события сборки
    /// </summary>
    [Fact]
    public async Task RawPayloadSingleChunkShouldReportRaw()
    {
        // Arrange
        var streamId = Guid.NewGuid();
        var chunk = BuildChunk(streamId, StreamChunkFlags.Start | StreamChunkFlags.Mid | StreamChunkFlags.End | StreamChunkFlags.RawPayload, 0, 1, [5, 6]);

        // Act
        await assembler.ProcessStreamChunk(chunk, roomId, connectionId, null, CancellationToken.None);

        // Assert
        assembled.Single().IsRawPayload.Should().BeTrue();
        assembled.Single().FilePath.Should().BeNull();
        assembled.Single().Data.Should().Equal([5, 6]);
    }

    /// <summary>
    /// После сборки поток удалён из активных
    /// </summary>
    [Fact]
    public async Task AssembledStreamShouldBeRemoved()
    {
        // Arrange
        var streamId = Guid.NewGuid();
        var chunk = BuildChunk(streamId, StreamChunkFlags.Start | StreamChunkFlags.Mid | StreamChunkFlags.End, 0, 1, [1]);

        // Act
        await assembler.ProcessStreamChunk(chunk, roomId, connectionId, null, CancellationToken.None);
        var removed = assembler.TryRemoveStream(streamId);

        // Assert
        removed.Should().BeFalse();
    }
}
