using System.ComponentModel;
using FluentAssertions;
using MIN.Common.Core.Extensions;
using Xunit;

namespace MIN.Common.Tests;

/// <summary>
/// Тесты расширений Common: enum, списки, TimeSpan, Task
/// </summary>
public class ExtensionsTests
{
    private enum TestEnum
    {
        [Description("Описание значения")]
        WithDescription,
        Plain,
    }

    /// <summary>
    /// GetDescription возвращает описание или имя значения
    /// </summary>
    [Fact]
    public void EnumGetDescriptionShouldWork()
    {
        // Act & Assert
        TestEnum.WithDescription.GetDescription().Should().Be("Описание значения");
        TestEnum.Plain.GetDescription().Should().Be("Plain");
    }

    /// <summary>
    /// RemoveByPredicate удаляет все подходящие элементы
    /// </summary>
    [Fact]
    public void RemoveByPredicateShouldRemoveMatching()
    {
        // Arrange
        var list = new List<int> { 1, 2, 3, 4, 5, 6 };

        // Act
        list.RemoveByPredicate(2, (item, divisor) => item % divisor == 0);

        // Assert
        list.Should().BeEquivalentTo([1, 3, 5]);
    }

    /// <summary>
    /// GetIndex находит элемент или возвращает -1
    /// </summary>
    [Fact]
    public void GetIndexShouldFindOrReturnMinusOne()
    {
        // Arrange
        var array = new[] { "a", "b", "c" };

        // Act & Assert
        array.GetIndex("b").Should().Be(1);
        array.GetIndex("z").Should().Be(-1);
    }

    /// <summary>
    /// TimeSpan форматируется по-русски с правильным склонением
    /// </summary>
    [Theory]
    [InlineData(0.5, "менее секунды")]
    [InlineData(1, "1 секунда")]
    [InlineData(2, "2 секунды")]
    [InlineData(5, "5 секунд")]
    [InlineData(11, "11 секунд")]
    [InlineData(21, "21 секунда")]
    [InlineData(60, "1 минута")]
    [InlineData(120, "2 минуты")]
    [InlineData(300, "5 минут")]
    [InlineData(3600, "1 час")]
    [InlineData(7200, "2 часа")]
    [InlineData(18000, "5 часов")]
    [InlineData(86400, "1 день")]
    [InlineData(172800, "2 дня")]
    [InlineData(432000, "5 дней")]
    public void TimeSpanToFriendlyStringShouldDecline(double seconds, string expected)
    {
        // Act
        var result = TimeSpan.FromSeconds(seconds).ToFriendlyString();

        // Assert
        result.Should().Be(expected);
    }

    /// <summary>
    /// OrThrowIfNull бросает на null и пропускает значение
    /// </summary>
    [Fact]
    public async Task OrThrowIfNullShouldWork()
    {
        // Arrange
        var nullTask = Task.FromResult<string?>(null);
        var valueTask = Task.FromResult<string?>("значение");

        // Act
        var act = () => nullTask.OrThrowIfNull(() => new InvalidOperationException("null"));
        var result = await valueTask.OrThrowIfNull(() => new InvalidOperationException("null"));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("null");
        result.Should().Be("значение");
    }

    /// <summary>
    /// AndThrowIfTrue бросает на true
    /// </summary>
    [Fact]
    public async Task AndThrowIfTrueShouldWork()
    {
        // Act
        var trueAct = () => Task.FromResult(true).AndThrowIfTrue(() => new InvalidOperationException("true"));
        var falseAct = () => Task.FromResult(false).AndThrowIfTrue(() => new InvalidOperationException("true"));

        // Assert
        await trueAct.Should().ThrowAsync<InvalidOperationException>();
        await falseAct.Should().NotThrowAsync();
    }

    /// <summary>
    /// OrThrowIf бросает по условию
    /// </summary>
    [Fact]
    public async Task OrThrowIfShouldWork()
    {
        // Act
        var badAct = () => Task.FromResult(5).OrThrowIf(x => x > 3, () => new InvalidOperationException("большое"));
        var good = await Task.FromResult(2).OrThrowIf(x => x > 3, () => new InvalidOperationException("большое"));

        // Assert
        await badAct.Should().ThrowAsync<InvalidOperationException>();
        good.Should().Be(2);
    }
}
