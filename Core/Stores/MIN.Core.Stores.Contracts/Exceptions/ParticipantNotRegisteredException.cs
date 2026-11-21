namespace MIN.Core.Stores.Contracts.Exceptions;

/// <summary>
/// Ошибка "Участник не зарегистрирован"
/// </summary>
public class ParticipantNotRegisteredException : Exception
{
    /// <summary>
    /// Инициализирует новый экземпляр <see cref="ParticipantNotRegisteredException"/>
    /// </summary>
    public ParticipantNotRegisteredException(string message) : base(message) { }

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="ParticipantNotRegisteredException"/>
    /// </summary>
    public ParticipantNotRegisteredException(Guid connectionId) : base($"Участник с id {connectionId} не зарегистрирован") { }
}
