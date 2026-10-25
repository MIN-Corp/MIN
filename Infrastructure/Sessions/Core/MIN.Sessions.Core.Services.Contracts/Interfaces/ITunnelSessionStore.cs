using MIN.Sessions.Core.Services.Contracts.Models;

namespace MIN.Sessions.Core.Services.Contracts.Interfaces;

/// <summary>
/// Хранилище сохранённых туннельных сессий
/// </summary>
public interface ITunnelSessionStore
{
    /// <summary>
    /// Сохранить туннельную сессию на диск (presenter.json + thumbnail)
    /// </summary>
    Task SaveAsync(Session session, byte[]? thumbnailData, CancellationToken cancellationToken = default);
}
