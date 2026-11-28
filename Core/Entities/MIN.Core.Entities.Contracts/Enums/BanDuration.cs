namespace MIN.Core.Entities.Contracts.Enums;

/// <summary>
/// Длительность бана участника
/// </summary>
public enum BanDuration
{
    /// <summary>
    /// Один день
    /// </summary>
    Day = 1,

    /// <summary>
    /// Одна неделя
    /// </summary>
    Week = 2,

    /// <summary>
    /// Две недели
    /// </summary>
    TwoWeeks = 3,

    /// <summary>
    /// Один месяц
    /// </summary>
    Month = 4,

    /// <summary>
    /// Три месяца
    /// </summary>
    ThreeMonths = 5,

    /// <summary>
    /// Один год
    /// </summary>
    Year = 6,

    /// <summary>
    /// Навсегда (без даты разбана)
    /// </summary>
    Permanent = 7
}
