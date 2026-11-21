namespace MIN.Core.Stores.Contracts.Exceptions;

/// <summary>
/// Ошибка "Комната не зарегистрирована"
/// </summary>
public class RoomNotRegisteredException : Exception
{
    /// <summary>
    /// Инициализирует новый экземпляр <see cref="RoomNotRegisteredException"/>
    /// </summary>
    public RoomNotRegisteredException(string message) : base(message) { }

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="RoomNotRegisteredException"/>
    /// </summary>
    public RoomNotRegisteredException(Guid roomId) : base($"Комната с id {roomId} не зарегистрирована") { }
}
