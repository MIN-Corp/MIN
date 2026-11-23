using MIN.Core.Entities.Contracts.Enums;

namespace MIN.Core.Entities.Contracts.Extensions;

/// <summary>
/// Расширения для <see cref="BanDuration"/>
/// </summary>
public static class BanDurationExtensions
{
    /// <summary>
    /// Вычислить дату разбана от текущего момента
    /// </summary>
    /// <returns>
    /// Null - если бан навсегда
    /// </returns>
    public static DateTimeOffset? ToUnbanDate(this BanDuration duration) => duration switch
    {
        BanDuration.Day => DateTimeOffset.Now.AddDays(1),
        BanDuration.Week => DateTimeOffset.Now.AddDays(7),
        BanDuration.TwoWeeks => DateTimeOffset.Now.AddDays(14),
        BanDuration.Month => DateTimeOffset.Now.AddMonths(1),
        BanDuration.ThreeMonths => DateTimeOffset.Now.AddMonths(3),
        BanDuration.Year => DateTimeOffset.Now.AddYears(1),
        BanDuration.Permanent => null,
        _ => throw new ArgumentOutOfRangeException(nameof(duration), duration, "Неизвестная длительность бана")
    };
}
