using MIN.Core.Entities.Contracts.Models;
using MIN.Core.Stores.Contracts.Enums;

namespace MIN.Core.Stores.Contracts.Models.SubRooms;

/// <summary>
/// Инфорация о подкомнате
/// </summary>
public record SubRoomInfo
{
    /// <summary>
    /// Идентификатор подкомнаты
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    /// Цель подкомнаты
    /// </summary>
    public SubRoomPurpose Purpose { get; init; }

    /// <summary>
    /// Активна ли сейчас подкомната
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Идентификатора запросидшего создания подкомнаты
    /// </summary>
    public Guid CreatorId { get; init; }

    /// <summary>
    /// Участники подкомнаты
    /// </summary>
    public List<ParticipantInfo> Participants { get; init; } = [];

    /// <summary>
    /// Максимальное количество участников
    /// </summary>
    public int? MaximumParticipants { get; init; }

    /// <summary>
    /// Дата создания
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.Now;
}
