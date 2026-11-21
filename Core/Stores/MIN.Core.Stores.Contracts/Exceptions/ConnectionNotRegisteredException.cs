namespace MIN.Core.Stores.Contracts.Exceptions;

/// <summary>
/// Ошибка "Соединение не зарегистрировано"
/// </summary>
public class ConnectionNotRegisteredException : Exception
{
    /// <summary>
    /// Инициализирует новый экземпляр <see cref="ConnectionNotRegisteredException"/>
    /// </summary>
    public ConnectionNotRegisteredException(string message) : base(message) { }

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="ConnectionNotRegisteredException"/>
    /// </summary>
    public ConnectionNotRegisteredException(Guid connectionId) : base($"Соединение с id {connectionId} не зарегистрировано") { }
}
