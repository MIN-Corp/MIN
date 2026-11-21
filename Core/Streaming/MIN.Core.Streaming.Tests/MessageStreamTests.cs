using FluentAssertions;
using MIN.Core.Headers.Contracts.Enums;
using MIN.Core.Streaming.Contracts.Constants;
using MIN.Core.Streaming.Contracts.Models;
using Xunit;

namespace MIN.Core.Streaming.Tests;

/// <summary>
/// Тесты на <see cref="MessageStream"/>
/// </summary>
public class MessageStreamTests
{
    private readonly static Guid streamId = Guid.NewGuid();
    private readonly static Guid roomId = Guid.NewGuid();
    private readonly static Guid connectionId = Guid.NewGuid();

    /// <summary>
    /// Создать чанк с полезной нагрузкой
    /// </summary>
    private static StreamChunk Chunk(int index, int total, byte[] data, StreamChunkFlags flags)
        => new() { StreamId = streamId, Index = index, Total = total, Data = data, Flags = flags };

    /// <summary>
    /// Последовательные чанки собираются в исходный payload
    /// </summary>
    [Fact]
    public void InOrderChunksShouldAssemble()
    {
        // Arrange
        using var stream = new MessageStream(streamId, roomId, connectionId, expectedChunks: 3, requiresAcks: false);
        var part0 = Enumerable.Repeat((byte)1, StreamingConstants.ChunkDataSize).ToArray();
        var part1 = Enumerable.Repeat((byte)2, StreamingConstants.ChunkDataSize).ToArray();
        var part2 = new byte[] { 3, 3, 3 };

        // Act
        stream.AddChunk(Chunk(0, 3, part0, StreamChunkFlags.Start | StreamChunkFlags.Mid)).Should().BeNull();
        stream.AddChunk(Chunk(1, 3, part1, StreamChunkFlags.Mid)).Should().BeNull();
        var result = stream.AddChunk(Chunk(2, 3, part2, StreamChunkFlags.Mid | StreamChunkFlags.End));

        // Assert
        result.Should().NotBeNull();
        result.Should().Equal(part0.Concat(part1).Concat(part2));
        stream.TotalDataSize.Should().Be(part0.Length + part1.Length + part2.Length);
    }

    /// <summary>
    /// Чанки не по порядку собираются корректно
    /// </summary>
    [Fact]
    public void OutOfOrderChunksShouldAssemble()
    {
        // Arrange
        using var stream = new MessageStream(streamId, roomId, connectionId, expectedChunks: 3, requiresAcks: false);
        var part0 = Enumerable.Repeat((byte)1, StreamingConstants.ChunkDataSize).ToArray();
        var part1 = Enumerable.Repeat((byte)2, StreamingConstants.ChunkDataSize).ToArray();
        var part2 = new byte[] { 3 };

        // Act
        stream.AddChunk(Chunk(1, 3, part1, StreamChunkFlags.Mid));
        stream.AddChunk(Chunk(0, 3, part0, StreamChunkFlags.Start | StreamChunkFlags.Mid));
        var result = stream.AddChunk(Chunk(2, 3, part2, StreamChunkFlags.Mid | StreamChunkFlags.End));

        // Assert
        result.Should().Equal(part0.Concat(part1).Concat(part2));
    }

    /// <summary>
    /// Дубликаты чанков игнорируются
    /// </summary>
    [Fact]
    public void DuplicateChunksShouldBeIgnored()
    {
        // Arrange
        using var stream = new MessageStream(streamId, roomId, connectionId, expectedChunks: 2, requiresAcks: false);
        var part0 = Enumerable.Repeat((byte)1, StreamingConstants.ChunkDataSize).ToArray();
        var part1 = new byte[] { 2 };

        // Act
        stream.AddChunk(Chunk(0, 2, part0, StreamChunkFlags.Start | StreamChunkFlags.Mid));
        stream.AddChunk(Chunk(0, 2, part0, StreamChunkFlags.Start | StreamChunkFlags.Mid)).Should().BeNull();
        var result = stream.AddChunk(Chunk(1, 2, part1, StreamChunkFlags.Mid | StreamChunkFlags.End));

        // Assert
        result.Should().Equal(part0.Concat(part1));
        stream.TotalDataSize.Should().Be(part0.Length + 1);
    }

    /// <summary>
    /// Одночанковый поток собирается сразу
    /// </summary>
    [Fact]
    public void SingleChunkShouldAssemble()
    {
        // Arrange
        using var stream = new MessageStream(streamId, roomId, connectionId, expectedChunks: 1, requiresAcks: false);
        var data = new byte[] { 1, 2, 3 };

        // Act
        var result = stream.AddChunk(Chunk(0, 1, data, StreamChunkFlags.Start | StreamChunkFlags.Mid | StreamChunkFlags.End));

        // Assert
        result.Should().Equal(data);
    }

    /// <summary>
    /// End с неверным индексом отклоняется
    /// </summary>
    [Fact]
    public void EndWithWrongIndexShouldThrow()
    {
        // Arrange
        using var stream = new MessageStream(streamId, roomId, connectionId, expectedChunks: 3, requiresAcks: false);

        // Act
        var act = () => stream.AddChunk(Chunk(1, 3, [1], StreamChunkFlags.Mid | StreamChunkFlags.End));

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    /// <summary>
    /// Потерянный средний чанк: сборка с End отклоняется, а не возвращает битые данные
    /// </summary>
    [Fact]
    public void MissingMiddleChunkShouldThrowInvalidData()
    {
        // Arrange
        using var stream = new MessageStream(streamId, roomId, connectionId, expectedChunks: 3, requiresAcks: false);
        var part0 = Enumerable.Repeat((byte)1, StreamingConstants.ChunkDataSize).ToArray();
        var part2 = new byte[] { 3 };

        // Act
        stream.AddChunk(Chunk(0, 3, part0, StreamChunkFlags.Start | StreamChunkFlags.Mid));
        var act = () => stream.AddChunk(Chunk(2, 3, part2, StreamChunkFlags.Mid | StreamChunkFlags.End));

        // Assert
        act.Should().Throw<InvalidDataException>();
    }

    /// <summary>
    /// Индекс чанка вне диапазона отклоняется
    /// </summary>
    [Fact]
    public void IndexOutOfRangeShouldThrow()
    {
        // Arrange
        using var stream = new MessageStream(streamId, roomId, connectionId, expectedChunks: 2, requiresAcks: false);

        // Act
        var tooBig = () => stream.AddChunk(Chunk(5, 2, [1], StreamChunkFlags.Mid));
        var negative = () => stream.AddChunk(Chunk(-1, 2, [1], StreamChunkFlags.Mid));

        // Assert
        tooBig.Should().Throw<ArgumentOutOfRangeException>();
        negative.Should().Throw<ArgumentOutOfRangeException>();
    }

    /// <summary>
    /// После завершения новые чанки игнорируются
    /// </summary>
    [Fact]
    public void AddAfterCompleteShouldReturnNull()
    {
        // Arrange
        using var stream = new MessageStream(streamId, roomId, connectionId, expectedChunks: 1, requiresAcks: false);
        stream.AddChunk(Chunk(0, 1, [1], StreamChunkFlags.Start | StreamChunkFlags.Mid | StreamChunkFlags.End));

        // Act
        var result = stream.AddChunk(Chunk(0, 1, [2], StreamChunkFlags.Start | StreamChunkFlags.Mid | StreamChunkFlags.End));

        // Assert
        result.Should().BeNull();
    }

    /// <summary>
    /// Raw payload пишется во временный файл и удаляется при Dispose
    /// </summary>
    [Fact]
    public void RawPayloadShouldUseTempFile()
    {
        // Arrange
        var stream = new MessageStream(streamId, roomId, connectionId, expectedChunks: 2, requiresAcks: false, isRawPayload: true);
        var part0 = Enumerable.Repeat((byte)7, StreamingConstants.ChunkDataSize).ToArray();
        var part1 = new byte[] { 8, 8 };
        var tempPath = stream.GetTempFilePath();

        // Act
        stream.AddChunk(Chunk(0, 2, part0, StreamChunkFlags.Start | StreamChunkFlags.Mid | StreamChunkFlags.RawPayload));
        var result = stream.AddChunk(Chunk(1, 2, part1, StreamChunkFlags.Mid | StreamChunkFlags.End | StreamChunkFlags.RawPayload));
        var fileBytes = File.ReadAllBytes(tempPath!);
        stream.Dispose();

        // Assert
        tempPath.Should().NotBeNull();
        result.Should().BeEmpty();
        fileBytes.Should().Equal(part0.Concat(part1));
        File.Exists(tempPath).Should().BeFalse();
    }
}
