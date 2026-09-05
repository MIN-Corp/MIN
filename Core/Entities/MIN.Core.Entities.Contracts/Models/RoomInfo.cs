using MIN.Core.Entities.Contracts.Interfaces;

namespace MIN.Core.Entities.Contracts.Models;

/// <summary>
/// Данные комнаты для передачи по сети
/// </summary>
public record RoomInfo : IRoomData
{
    /// <inheritdoc />
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <inheritdoc />
    public string Name { get; set; } = string.Empty;

    /// <inheritdoc />
    public string Cabinet { get; set; } = string.Empty;

    /// <inheritdoc />
    public int PcNumber { get; set; }

    /// <inheritdoc />
    public int ParticipantCount { get; set; }

    /// <inheritdoc />
    public int TotalMessageCount { get; set; }

    /// <inheritdoc />
    public int MaximumParticipants { get; set; }

    /// <inheritdoc />
    public bool IsOnline { get; set; }

    /// <inheritdoc />
    public ParticipantInfo HostParticipant { get; set; } = null!;

    /// <inheritdoc />
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="RoomInfo"/>
    /// </summary>
    public RoomInfo(IRoomData room)
    {
        Id = room.Id;
        Name = room.Name;
        Cabinet = room.Cabinet;
        PcNumber = room.PcNumber;
        HostParticipant = room.HostParticipant;
        ParticipantCount = room.ParticipantCount;
        TotalMessageCount = room.TotalMessageCount;
        MaximumParticipants = room.MaximumParticipants;
        IsOnline = room.IsOnline;
        CreatedAt = room.CreatedAt;
    }

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="RoomInfo"/>
    /// </summary>
    /// <remarks>
    /// Нужен для сериализации
    /// </remarks>
    public RoomInfo() { }
}
