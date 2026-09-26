using System.Threading;
using System.Threading.Tasks;
using MIN.Desktop.Contracts.Models;

namespace MIN.Desktop.Contracts.Interfaces;

/// <summary>
/// Сервис для управления подключения и создания комнат
/// </summary>
public interface IRoomConnectionUiService
{
    /// <summary>
    /// Присоединиться к комнате
    /// </summary>
    Task<RoomJoinResult> JoinAsync(RoomJoinArgs args, CancellationToken ct);

    /// <summary>
    /// Захостить комнату
    /// </summary>
    Task<RoomHostResult> HostAsync(RoomHostArgs args, CancellationToken ct);
}
