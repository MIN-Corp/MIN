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
    /// Получен кадр экрана для отправки.
    /// ВАЖНО: буфер Data выдан из ArrayPool и переходит во владение подписчику —
    /// подписчик обязан вернуть его через ArrayPool, когда кадр больше не нужен.
    /// Подписчик должен быть один (трансмиттер); предпросмотр подписывается на IScreenDemoTransmitter.FramePreview
    /// </summary>
    event EventHandler<ScreenFrame>? FrameCaptured;
}
