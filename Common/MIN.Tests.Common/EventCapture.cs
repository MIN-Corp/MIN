using System.Collections.Concurrent;
using MIN.Core.Events.Contracts.Interfaces;
using MIN.Core.Events.Contracts.Models;

namespace MIN.Tests.Common;

/// <summary>
/// Захват событий шины для проверок в тестах
/// </summary>
public sealed class EventCapture<T> : IDisposable where T : BaseEvent
{
    private readonly ConcurrentQueue<T> events = new();
    private readonly IDisposable subscription;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="EventCapture{T}"/> и подписывается на шину
    /// </summary>
    public EventCapture(IEventBus eventBus)
    {
        subscription = eventBus.Subscribe<T>((e, _) =>
        {
            events.Enqueue(e);
            return Task.CompletedTask;
        });
    }

    /// <summary>
    /// Снимок захваченных событий
    /// </summary>
    public IReadOnlyList<T> Events => events.ToArray();

    /// <summary>
    /// Дождаться накопления нужного числа событий
    /// </summary>
    public Task WaitAsync(int count = 1, int timeoutMs = 5000)
        => TestWait.UntilAsync(() => events.Count >= count, timeoutMs);

    /// <inheritdoc />
    public void Dispose() => subscription.Dispose();
}
