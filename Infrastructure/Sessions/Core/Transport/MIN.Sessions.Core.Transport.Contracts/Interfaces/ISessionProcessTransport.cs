using MIN.Sessions.Core.Transport.Contracts.Events;
using MIN.Sessions.Core.Transport.Contracts.Models;

namespace MIN.Sessions.Core.Transport.Contracts.Interfaces;

/// <summary>
/// Транспорт для междупроцессорного общения
/// </summary>
public interface ISessionProcessTransport : IAsyncDisposable
{
    /// <summary>
    /// Запустить серверную часть транспорта
    /// </summary>
    Task StartAsync(Guid roomId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Событие: получены сырые данные от подключения
    /// </summary>
    event EventHandler<ProcessTransportMessageEventArgs>? MessageReceived;

    /// <summary>
    /// Параметр подключения для игрового процесса
    /// </summary>
    /// <remarks>
    /// Например: {"pipe":"MIN_{roomId}_{sessionType}"} {"tcp":"127.0.0.1:54321"}
    /// </remarks>
    string GetConnectionString();

    /// <summary>
    /// Соединено ли приложение
    /// </summary>
    bool IsConnectionExists(SessionInstanceContext context);

    /// <summary>
    /// Ждать подключения одного процесса (server или client)
    /// </summary>
    Task WaitForConnectionAsync(SessionInstanceContext context, int timeOutMs, CancellationToken cancellationToken = default);

    /// <summary>
    /// Отправить данные приложению
    /// </summary>
    Task SendAsync(byte[] data, SessionInstanceContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Закрыть конкретное подключение
    /// </summary>
    Task DisconnectAsync(SessionInstanceContext context);

    /// <summary>
    /// Остановить транспорт, разорвать все соединения
    /// </summary>
    Task StopAsync();
}
