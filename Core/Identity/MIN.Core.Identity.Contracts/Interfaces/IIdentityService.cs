using MIN.Core.Entities.Contracts.Interfaces;

namespace MIN.Core.Identity.Contracts.Interfaces;

/// <summary>
/// Сервис по предоставлению данных о текущем пользователе
/// </summary>
public interface IIdentityService
{
    /// <summary>
    /// Текущий пользователь приложения
    /// </summary>
    IParticipantData SelfParticipant { get; }

    /// <summary>
    /// Установить и сохранить данные пользователя
    /// </summary>
    Task SaveParticipant(IParticipantData participantData);
}
