using MIN.Core.Entities.Contracts.Interfaces;
using MIN.Core.Entities.Contracts.Models;
using MIN.Core.Messaging.Contracts.Interfaces;
using MIN.Core.Transport.Contracts.Interfaces;

namespace MIN.Core.Entities;

/// <summary>
/// Комната
/// </summary>
public class Room : IRoomData
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
    public int MaximumParticipants { get; set; }

    /// <inheritdoc />
    public int ParticipantCount => CurrentParticipants.Count;

    /// <inheritdoc />
    public int TotalMessageCount { get; set; }

    /// <inheritdoc />
    public bool IsOnline { get; set; }

    /// <inheritdoc />
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <inheritdoc />
    public IEnumerable<IEndpoint> ConnectionAddresses { get; set; } = [];

    /// <summary>
    /// Локальные настройки комнаты
    /// </summary>
    /// <remarks>
    /// Для новых участников всегда будет пустым,
    /// Нужен для сохранения комнаты на будующее
    /// </remarks>
    public LocalRoomSettings LocalRoomSettings { get; set; } = new();

    /// <summary>
    /// Хост комнаты
    /// </summary>
    public ParticipantInfo HostParticipant { get; set; } = null!;

    /// <summary>
    /// Текущие участников комнаты
    /// </summary>
    /// <remarks>
    /// Является лишь отображением из ParticipantStore
    /// </remarks>
    public List<Participant> CurrentParticipants { get; set; } = [];

    /// <summary>
    /// История чата
    /// </summary>
    /// <remarks>
    /// Является лишь отображением из MessageStore
    /// </remarks>
    public List<IMessage> ChatHistory { get; set; } = [];

    /// <summary>
    /// Заполнена ли комната
    /// </summary>
    public bool IsFull => CurrentParticipants.Count >= MaximumParticipants;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="Room"/>
    /// </summary>
    /// <remarks>
    /// Нужен для сериализации
    /// </remarks>
    public Room() { }

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="Room"/>
    /// </summary>
    public Room(IRoomData roomData)
    {
        Id = roomData.Id;
        Name = roomData.Name;
        Cabinet = roomData.Cabinet;
        PcNumber = roomData.PcNumber;
        HostParticipant = roomData.HostParticipant;
        MaximumParticipants = roomData.MaximumParticipants;
        IsOnline = roomData.IsOnline;
        CreatedAt = roomData.CreatedAt;
    }

    /// <summary>
    /// Создать клон комнаты
    /// </summary>
    public Room Clone() => new()
    {
        Id = Id,
        Name = Name,
        Cabinet = Cabinet,
        PcNumber = PcNumber,
        MaximumParticipants = MaximumParticipants,
        IsOnline = IsOnline,
        CreatedAt = CreatedAt,
        HostParticipant = HostParticipant,
        CurrentParticipants = CurrentParticipants.ToList(),
        ChatHistory = ChatHistory.ToList(),
        TotalMessageCount = TotalMessageCount,
        ConnectionAddresses = ConnectionAddresses.ToList(),
        LocalRoomSettings = LocalRoomSettings with { },
    };
}
