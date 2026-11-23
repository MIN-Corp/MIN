namespace MIN.Core.Entities.Contracts.Models;

/// <summary>
/// Информация о бане участника
/// </summary>
public sealed record BanInfo(Guid ParticipantId, string Name, string Reason, DateTimeOffset? BannedUntil)
{
    /// <summary>
    /// Сообщение для забаненного участника
    /// </summary>
    public string GetBanMessage()
    {
        var untilText = BannedUntil is { } until
            ? $"Вы забанены до {until:dd.MM.yyyy HH:mm}"
            : "Вы забанены навсегда";

        return string.IsNullOrWhiteSpace(Reason) ? untilText : $"{Reason}. {untilText}";
    }
}
