using MIN.ScreenDemo.Services.Contracts.Models;

namespace MIN.ScreenDemo.Services.Contracts.Interfaces;

/// <summary>
/// Сервис захвата экрана
/// </summary>
public interface IScreenCaptureService : IDisposable
{
    /// <summary>
    /// Начать захват указанного источника
    /// </summary>
    void Start(ScreenSourceInfo source);

    /// <summary>
    /// Остановить захват
    /// </summary>
    void Stop();

    /// <summary>
    /// Получен кадр экрана для отправки
    /// </summary>
    event EventHandler<ScreenFrame>? FrameCaptured;
}
