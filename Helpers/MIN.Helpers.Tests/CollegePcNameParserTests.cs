using FluentAssertions;
using MIN.Helpers.Services;
using Xunit;

namespace MIN.Helpers.Tests;

/// <summary>
/// Тесты на <see cref="CollegePcNameParser"/>
/// </summary>
public class CollegePcNameParserTests
{
    /// <summary>
    /// Корректные имена компьютеров разбираются на кабинет и номер
    /// </summary>
    [Theory]
    [InlineData("A1015", 101, 5)]
    [InlineData("B20312", 203, 12)]
    [InlineData("C0011", 1, 1)]
    public void ValidNamesShouldParse(string name, int expectedRoom, int expectedPc)
    {
        // Act
        var ok = CollegePcNameParser.TryParseComputerName(name, out var room, out var pc);

        // Assert
        ok.Should().BeTrue();
        room.Should().Be(expectedRoom);
        pc.Should().Be(expectedPc);
    }

    /// <summary>
    /// Некорректные имена отклоняются
    /// </summary>
    [Theory]
    [InlineData("a1015")]
    [InlineData("AB1015")]
    [InlineData("A101")]
    [InlineData("A101555")]
    [InlineData("")]
    [InlineData("1015")]
    public void InvalidNamesShouldFail(string name)
    {
        // Act
        var ok = CollegePcNameParser.TryParseComputerName(name, out _, out _);

        // Assert
        ok.Should().BeFalse();
    }
}
