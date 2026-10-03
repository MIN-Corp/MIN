using MIN.Core.Entities;

namespace MIN.Core.Services.Contracts.Interfaces.Persistence;

/// <summary>
/// Сервис по сохранению комнат в персистентном хранилище
/// </summary>
public interface IRoomPersistenceService
{
    /// <summary>
    /// Запустить сервис
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Пометить комнату изменённой
    /// </summary>
    void MarkDirty(Guid roomId);

    /// <summary>
    /// Комнаты, восстановленные из файлов при старте
    /// </summary>
    Task<IReadOnlyList<Room>> GetLoadedRoomsAsync();

    /// <summary>
    /// Остановить сервис
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);
}
