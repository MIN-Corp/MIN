using FluentAssertions;
using MIN.Core.Headers.Contracts.Enums;
using MIN.Core.Headers.Contracts.Interfaces;
using MIN.Core.Headers.Services;
using Xunit;

namespace MIN.Core.Transport.Tests;

/// <summary>
/// Тесты на <see cref="HeaderManager"/>
/// </summary>
public class HeaderManagerTests
{
    private readonly IHeaderManager manager = new HeaderManager();

    /// <summary>
    /// AddHeader добавляет байт-маркер, StripTypeHeader снимает его
    /// </summary>
    [Fact]
    public void AddHeaderStripShouldRoundTrip()
    {
        // Arrange
        var data = new byte[] { 1, 2, 3 };

        // Act
        var withHeader = manager.AddHeader(data, 0x42);
        var stripped = manager.StripTypeHeader(withHeader);

        // Assert
        withHeader[0].Should().Be(0x42);
        withHeader.Length.Should().Be(4);
        stripped.ToArray().Should().Equal(data);
    }

    /// <summary>
    /// StripTypeHeader на пустых и однобайтовых данных даёт пустой результат
    /// </summary>
    [Fact]
    public void StripTypeHeaderShortDataShouldBeEmpty()
    {
        // Act
        var empty = manager.StripTypeHeader([]);
        var single = manager.StripTypeHeader([1]);

        // Assert
        empty.IsEmpty.Should().BeTrue();
        single.IsEmpty.Should().BeTrue();
    }

    /// <summary>
    /// Детекторы типа читают биты маркера
    /// </summary>
    [Fact]
    public void TypeDetectorsShouldReadMarkerBits()
    {
        // Act & Assert
        manager.IsEncrypted([1]).Should().BeTrue();
        manager.IsEncrypted([0]).Should().BeFalse();
        manager.IsAck([0x80]).Should().BeTrue();
        manager.IsAck([0]).Should().BeFalse();
        manager.IsStreamChunk([0x10]).Should().BeTrue();
        manager.IsStreamChunk([0]).Should().BeFalse();
        manager.IsEncrypted([]).Should().BeFalse();
        manager.IsAck([]).Should().BeFalse();
        manager.IsStreamChunk([]).Should().BeFalse();
    }

    /// <summary>
    /// Заголовок чанка собирается и разбирается без потери флагов
    /// </summary>
    [Fact]
    public void StreamChunkHeaderShouldRoundTrip()
    {
        // Arrange
        var streamId = Guid.NewGuid();
        const StreamChunkFlags flags = StreamChunkFlags.Start | StreamChunkFlags.End | StreamChunkFlags.RequiresAcks | StreamChunkFlags.RawPayload;

        // Act
        var header = manager.BuildStreamChunkHeader(flags, streamId, index: 3, total: 7);
        var parsed = manager.ParseStreamChunkHeader(header);

        // Assert
        header.Length.Should().Be(25);
        parsed.StreamId.Should().Be(streamId);
        parsed.Index.Should().Be(3);
        parsed.Total.Should().Be(7);
        parsed.Flags.Should().Be(flags);
    }

    /// <summary>
    /// GetStreamFlags на пустых данных даёт None
    /// </summary>
    [Fact]
    public void GetStreamFlagsEmptyShouldBeNone()
    {
        // Act
        var flags = manager.GetStreamFlags([]);

        // Assert
        flags.Should().Be(StreamChunkFlags.None);
    }

    /// <summary>
    /// Разбор заголовка короче 25 байт бросает исключение (текущее поведение)
    /// </summary>
    [Fact]
    public void ParseShortHeaderShouldThrow()
    {
        // Act
        var act = () => manager.ParseStreamChunkHeader(new byte[10]);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
