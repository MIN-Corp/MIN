using MIN.Core.Entities.Contracts.Enums;
using MIN.Core.Entities.Contracts.Interfaces;

namespace MIN.Core.Entities;

/// <summary>
/// Участник комнаты
/// </summary>
public record Participant : IParticipantData
{
    /// <summary>
    /// Идентификатор участника
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Имя участника
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Статус действия в сети
    /// </summary>
    public OnlineStatus CurrentStatus { get; set; }

    /// <summary>
    /// Последнее время онлайн
    /// </summary>
    public DateTime LastSeenOnline { get; set; }

    /// <summary>
    /// Когда присоединился к комнате
    /// </summary>
    public DateTime JoinedAt { get; set; }

    /// <summary>
    /// Когда участник последний раз менял описание
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="Participant"/>
    /// </summary>
    public Participant(IParticipantData participantData)
    {
        Id = participantData.Id;
        Name = participantData.Name;
        CurrentStatus = OnlineStatus.Online;
        LastSeenOnline = DateTime.Now;
        JoinedAt = DateTime.Now;
    }

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="Participant"/>
    /// </summary>
    /// <remarks>
    /// Нужен для сериализации
    /// </remarks>
    public Participant() { }
}
