using MIN.ScreenDemo.Services.Contracts.Models;

namespace MIN.Chat.Services.Contracts.Interfaces;

/// <summary>
/// Сервис для управления демонстрацией экрана
/// </summary>
public interface IChatScreenDemoService
{
    /// <summary>
    /// Запросить активные демонстрации в комнате
    /// </summary>
    Task RequestDemoStateAsync(Guid roomId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Начать демонстрацию экрана. При <paramref name="withSound"/> системный звук
    /// записывается и передаётся зрителям (пока они есть)
    /// </summary>
    Task StartDemoAsync(Guid roomId, ScreenSourceInfo source, bool withSound, CancellationToken cancellationToken = default);

    /// <summary>
    /// Присоединиться к демонстрации как зритель
    /// </summary>
    Task JoinDemoAsync(Guid roomId, int subRoomId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Покинуть демонстрацию (для зрителя)
    /// </summary>
    Task LeaveDemoAsync(Guid roomId, int subRoomId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Завершить свою демонстрацию
    /// </summary>
    Task EndDemoAsync(Guid roomId, int subRoomId, CancellationToken cancellationToken = default);
}
