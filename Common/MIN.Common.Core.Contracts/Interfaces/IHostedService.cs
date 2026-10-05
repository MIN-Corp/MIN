namespace MIN.Common.Core.Contracts.Interfaces;

/// <summary>
/// Сервис, которому нужно ручное управление запуском
/// </summary>
public interface IHostedService
{
    /// <summary>
    /// Приоритет запуска/остановки
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// Запустить сервис
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Остановить сервис
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);
}
