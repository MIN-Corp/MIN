using MIN.Sessions.Core.Transport.Contracts.Models;

namespace MIN.Sessions.Core.Transport.Contracts.Events;

/// <summary>
/// Аргументы событие получения данных от приложения
/// </summary>
public sealed class ProcessTransportMessageEventArgs : EventArgs
{
    /// <summary>
    /// Контекст общения с приложением
    /// </summary>
    public SessionInstanceContext Context { get; init; }

    /// <summary>
    /// Полученные данные
    /// </summary>
    public byte[] Data { get; init; } = null!;

    /// <summary>
    /// Отправитель
    /// </summary>
    public Guid SenderId { get; init; }
}
