using FluentAssertions;
using MIN.Core.Transport.Contracts.Helpers;
using Xunit;

namespace MIN.Core.Transport.Tests;

/// <summary>
/// Тесты на <see cref="ConnectionPreamble"/>
/// </summary>
public class ConnectionPreambleTests
{
    /// <summary>
    /// Преамбула создаётся и разбирается с тем же идентификатором
    /// </summary>
    [Fact]
    public void CreateTryParseShouldRoundTrip()
    {
        // Arrange
        var connectionId = Guid.NewGuid();

        // Act
        var frame = ConnectionPreamble.Create(connectionId);
        var parsed = ConnectionPreamble.TryParse(frame, out var parsedId);

        // Assert
        parsed.Should().BeTrue();
        parsedId.Should().Be(connectionId);
    }

    /// <summary>
    /// Испорченная сигнатура отклоняется
    /// </summary>
    [Fact]
    public void TamperedMagicShouldBeRejected()
    {
        // Arrange
        var frame = ConnectionPreamble.Create(Guid.NewGuid());
        frame[0] ^= 0xFF;

        // Act
        var parsed = ConnectionPreamble.TryParse(frame, out _);

        // Assert
        parsed.Should().BeFalse();
    }

    /// <summary>
    /// Укороченный кадр отклоняется
    /// </summary>
    [Fact]
    public void TruncatedFrameShouldBeRejected()
    {
        // Arrange
        var frame = ConnectionPreamble.Create(Guid.NewGuid());

        // Act
        var parsed = ConnectionPreamble.TryParse(frame[..^1], out _);

        // Assert
        parsed.Should().BeFalse();
    }

    /// <summary>
    /// Пустой идентификатор в преамбуле отклоняется
    /// </summary>
    [Fact]
    public void EmptyGuidShouldBeRejected()
    {
        // Arrange
        var frame = ConnectionPreamble.Create(Guid.Empty);

        // Act
        var parsed = ConnectionPreamble.TryParse(frame, out _);

        // Assert
        parsed.Should().BeFalse();
    }

    /// <summary>
    /// Мусорный кадр отклоняется
    /// </summary>
    [Fact]
    public void GarbageFrameShouldBeRejected()
    {
        // Act
        var parsed = ConnectionPreamble.TryParse(new byte[20], out _);

        // Assert
        parsed.Should().BeFalse();
    }
}
