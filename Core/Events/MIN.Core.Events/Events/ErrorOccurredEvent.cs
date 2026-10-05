using MIN.Core.Events.Contracts.Models;

namespace MIN.Core.Events.Events;

/// <summary>
/// Событие, возникающее при ошибке в работе комнаты или соединения
/// </summary>
public sealed record ErrorOccurredEvent : BaseEvent
{
    /// <summary>
    /// Идентификатор комнаты (если применимо)
    /// </summary>
    public Guid? RoomId { get; init; }

    /// <summary>
    /// Нужно ли отключиться от комнаты
    /// </summary>
    public bool NeedToDisconnect { get; init; }

    /// <summary>
    /// Нужно ли забыть комнату
    /// </summary>
    public bool NeedToDestroy { get; init; }

    /// <summary>
    /// Сообщение об ошибке
    /// </summary>
    public string ErrorMessage { get; init; } = string.Empty;
}
