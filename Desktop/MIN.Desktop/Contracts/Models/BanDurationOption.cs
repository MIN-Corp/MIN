using System.Collections.Generic;
using MIN.Core.Entities.Contracts.Enums;

namespace MIN.Desktop.Contracts.Models;

/// <summary>
/// Вариант длительности бана для выбора в интерфейсе
/// </summary>
public sealed class BanDurationOption
{
    /// <summary>
    /// Значение длительности
    /// </summary>
    public BanDuration Value { get; }

    /// <summary>
    /// Отображаемое название
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="BanDurationOption"/>
    /// </summary>
    public BanDurationOption(BanDuration value, string title)
    {
        Value = value;
        Title = title;
    }

    /// <inheritdoc />
    public override string ToString() => Title;

    /// <summary>
    /// Все доступные варианты длительности
    /// </summary>
    public static IReadOnlyList<BanDurationOption> All { get; } =
    [
        new(BanDuration.Day, "1 день"),
        new(BanDuration.Week, "1 неделя"),
        new(BanDuration.TwoWeeks, "2 недели"),
        new(BanDuration.Month, "1 месяц"),
        new(BanDuration.ThreeMonths, "3 месяца"),
        new(BanDuration.Year, "1 год"),
        new(BanDuration.Permanent, "Навсегда"),
    ];
}
