namespace MIN.Core.Entities.Contracts.Models;

/// <summary>
/// Информация о бане участника
/// </summary>
/// <param name="ParticipantId">Идентификатор забаненного участника</param>
/// <param name="Name">Имя участника на момент бана</param>
/// <param name="Reason">Причина бана</param>
/// <param name="BannedUntil">Дата разбана</param>
/// <param name="IpAddress">IP-адрес участника на момент бана (null - если неизвестен)</param>
/// <param name="IsIpBanned">Действует ли дополнительно бан по <paramref name="IpAddress"/></param>
public sealed record BanInfo(Guid ParticipantId, string Name, string Reason, DateTimeOffset? BannedUntil, string? IpAddress = null, bool IsIpBanned = false)
{
    /// <summary>
    /// Сообщение для забаненного участника
    /// </summary>
    public string GetBanMessage()
    {
        var untilText = $"Вы забанены {FormatUnbanDate()}";
        return string.IsNullOrWhiteSpace(Reason) ? untilText : $"{Reason}. {untilText}";
    }

    /// <summary>
    /// Отформатировать текст даты окончания бана
    /// </summary>
    public string FormatUnbanDate()
        => BannedUntil is { } until
            ? $"до {until:dd.MM.yyyy HH:mm}"
            : "навсегда";
}
