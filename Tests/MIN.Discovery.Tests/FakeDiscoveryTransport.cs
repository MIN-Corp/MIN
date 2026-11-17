using MIN.Discovery.Transport.Contracts;
using MIN.Discovery.Transport.Contracts.Events;

namespace MIN.Discovery.Tests;

/// <summary>
/// Транспорт обнаружения в памяти
/// </summary>
public sealed class FakeDiscoveryTransport : IDiscoveryTransport
{
    /// <inheritdoc />
    public event EventHandler<DiscoveryRawMessageReceivedEventArgs>? MessageReceived;

    /// <summary>
    /// Число запусков прослушивания
    /// </summary>
    public int StartCalls { get; private set; }

    /// <summary>
    /// Число остановок прослушивания
    /// </summary>
    public int StopCalls { get; private set; }

    /// <summary>
    /// Отправленные широковещательные запросы
    /// </summary>
    public List<byte[]> Broadcasts { get; } = [];

    /// <summary>
    /// Обработчик рассылки: позволяет эмулировать ответы других узлов
    /// </summary>
    public Func<byte[], TimeSpan, Task>? BroadcastHandler { get; set; }

    /// <inheritdoc />
    public Task BroadcastAsync(byte[] data, TimeSpan timeout, CancellationToken ct = default)
    {
        Broadcasts.Add(data);
        return BroadcastHandler?.Invoke(data, timeout) ?? Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StartListeningAsync(CancellationToken cancellationToken = default)
    {
        StartCalls++;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopListeningAsync()
    {
        StopCalls++;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Доставить датаграмму так, будто она пришла из сети
    /// </summary>
    public void Deliver(byte[] data, IDiscoveryResponder responder)
        => MessageReceived?.Invoke(this, new DiscoveryRawMessageReceivedEventArgs(data, responder));
}

/// <summary>
/// Ответчик, сохраняющий отправленные ответы
/// </summary>
public sealed class CapturingResponder : IDiscoveryResponder
{
    /// <summary>
    /// Отправленные ответы
    /// </summary>
    public List<byte[]> Responses { get; } = [];

    /// <inheritdoc />
    public Task RespondAsync(byte[] data, CancellationToken ct = default)
    {
        Responses.Add(data);
        return Task.CompletedTask;
    }
}
