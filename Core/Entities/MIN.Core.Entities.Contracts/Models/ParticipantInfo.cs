using MIN.Core.Entities.Contracts.Interfaces;

namespace MIN.Core.Entities.Contracts.Models;

/// <summary>
/// Данные участника для передачи по сети
/// </summary>
public record ParticipantInfo : IParticipantData
{
    /// <inheritdoc />
    public Guid Id { get; set; }

    /// <inheritdoc />
    public string Name { get; set; } = string.Empty;
}
