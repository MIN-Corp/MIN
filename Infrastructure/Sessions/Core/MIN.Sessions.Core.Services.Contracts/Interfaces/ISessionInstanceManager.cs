using MIN.Sessions.Core.Services.Contracts.Models;
using MIN.Sessions.Core.Transport.Contracts.Models;

namespace MIN.Sessions.Core.Services.Contracts.Interfaces;

/// <summary>
/// Менедер экземпляров сессии (программа или туннель)
/// </summary>
public interface ISessionInstanceManager
{
    /// <summary>
    /// Запустить экземпляр сессии
    /// </summary>
    Task<ISessionInstance?> StartAsync(Session session, SessionInstanceContext context, CancellationToken ct = default);

    /// <summary>
    /// Остановить экземпляр сессии
    /// </summary>
    Task StopAsync(SessionInstanceContext context);

    /// <summary>
    /// Остановить экземпляр сессии для комнаты
    /// </summary>
    Task StopForRoomAsync(Guid roomId);

    /// <summary>
    /// Остановить все экземпляры сессии
    /// </summary>
    Task StopAllAsync();

    /// <summary>
    /// Существует ли клиент экземпляра сессии
    /// </summary>
    bool SessionClientAppExists(Session session);

    /// <summary>
    /// Получить все экземпляры сессии для подкомнаты
    /// </summary>
    IEnumerable<ISessionInstance> GetInstances(Guid roomId, int subRoomId);
}
