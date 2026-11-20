using FluentAssertions;
using MIN.Helpers.Contracts.Interfaces;
using MIN.Helpers.Contracts.Models;
using MIN.Helpers.Contracts.Models.Enums;
using MIN.Helpers.Services;
using Xunit;

namespace MIN.Helpers.Tests;

/// <summary>
/// Тесты на <see cref="LoggerProvider"/>
/// </summary>
public class LoggerProviderTests
{
    /// <summary>
    /// Запись лога содержит уровень, тип и сообщение
    /// </summary>
    [Fact]
    public void LogShouldFormatLevelCallerAndMessage()
    {
        // Arrange
        ILoggerProvider logger = new LoggerProvider();
        LogItem? received = null;
        logger.OnLogReceived += (_, item) => received = item;

        // Act
        logger.Log("тестовое сообщение", LogLevel.Warning, typeof(LoggerProviderTests));

        // Assert
        received.Should().NotBeNull();
        received!.Message.Should().Contain("Warning");
        received.Message.Should().Contain("тестовое сообщение");
        received.Message.Should().Contain(typeof(LoggerProviderTests).FullName!);
        received.LogLevel.Should().Be(LogLevel.Warning);
    }

    /// <summary>
    /// Длительность операции определяет уровень лога
    /// </summary>
    [Theory]
    [InlineData(50, LogLevel.Information)]
    [InlineData(150, LogLevel.Warning)]
    [InlineData(1500, LogLevel.Error)]
    public void DurationShouldDetermineLevel(double durationMs, LogLevel expected)
    {
        // Arrange
        ILoggerProvider logger = new LoggerProvider();

        // Act
        logger.Log("операция", LogLevel.Information, typeof(LoggerProviderTests), durationMs);

        // Assert
        logger.GetRecentLogHistory(null, null).Single().LogLevel.Should().Be(expected);
    }

    /// <summary>
    /// История логов постранично в обратном порядке
    /// </summary>
    [Fact]
    public void GetRecentLogHistoryShouldPaginateReversed()
    {
        // Arrange
        ILoggerProvider logger = new LoggerProvider();
        for (var i = 0; i < 10; i++)
        {
            logger.Log($"сообщение {i}", callerType: typeof(LoggerProviderTests));
        }

        // Act
        var page0 = logger.GetRecentLogHistory(0, 3).ToList();
        var page1 = logger.GetRecentLogHistory(1, 3).ToList();

        // Assert
        page0.Should().HaveCount(3);
        page0[0].Message.Should().Contain("сообщение 9");
        page1[0].Message.Should().Contain("сообщение 6");
    }
}
