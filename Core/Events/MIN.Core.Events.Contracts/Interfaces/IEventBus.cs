using MIN.Core.Events.Contracts.Models;

namespace MIN.Core.Events.Contracts.Interfaces;

/// <summary>
/// Шина событий для внутри процессной коммуникации
/// </summary>
public interface IEventBus
{
    /// <summary>
    /// Публикует событие в шину
    /// </summary>
    Task PublishAsync(BaseEvent eventMessage, CancellationToken cancellationToken = default);

    /// <summary>
    /// Подписывается на события указанного типа
    /// </summary>
    IDisposable Subscribe<T>(Func<T, CancellationToken, Task> handler) where T : BaseEvent;

    /// <summary>
    /// Создать Scope и сгруппировать под события по идентификатору
    /// </summary>
    IEventScope CreateScope(Guid roomId);
}
