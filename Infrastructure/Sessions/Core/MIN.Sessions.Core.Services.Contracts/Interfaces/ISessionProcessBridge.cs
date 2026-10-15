using MIN.Sessions.Core.Messaging.Contracts.Models;
using MIN.Sessions.Core.Transport.Contracts.Interfaces;
using MIN.Sessions.Core.Transport.Contracts.Models;

namespace MIN.Sessions.Core.Services.Contracts.Interfaces;

/// <summary>
/// Мост для общение с приложениями
/// </summary>
public interface ISessionProcessBridge
{
    /// <summary>
    /// Зарегистрировать транспорт на контекст
    /// </summary>
    void RegisterTransport(SessionInstanceContext context, ISessionProcessTransport transport);

    /// <summary>
    /// Отрегестрировать транспорт с контекста
    /// </summary>
    void UnregisterTransport(SessionInstanceContext context);

    /// <summary>
    /// Запустить сервис, слушая все сообщения с приложениями
    /// </summary>
    Task StartListeningAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Дождаться получения сообщения о готовности
    /// </summary>
    Task<bool> WaitForReadyMessage(SessionInstanceContext context, int timeOutMs, CancellationToken cancellationToken);

    /// <summary>
    /// Отправить запрос на закрытие приложения
    /// </summary>
    Task SendCloseMessage(SessionInstanceContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Отправить междупроцессорное сообщение
    /// </summary>
    Task SendIpcMessage(IpcMessage message, SessionInstanceContext context, Guid senderId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Получить список соединений с приложением  
    /// </summary>
    IEnumerable<SessionInstanceContext> GetConnections(Guid roomId, int subRoomId);

    /// <summary>
    /// Остановить сервис
    /// </summary>
    Task StopListeningAsync(CancellationToken cancellationToken = default);
}
