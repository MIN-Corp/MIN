using MIN.ScreenDemo.Services.Contracts.Models;

namespace MIN.ScreenDemo.Services.Contracts.Interfaces;

/// <summary>
/// Захват системного звука (loopback выходного устройства) для демонстрации экрана.
/// Платформно-зависимый: реализуется только в Capture-модулях, на остальных ОС — заглушка.
/// </summary>
public interface ISystemAudioCaptureService : IDisposable
{
    /// <summary>
    /// Начать захват системного звука (выходное устройство по умолчанию)
    /// </summary>
    void Start();

    /// <summary>
    /// Остановить захват
    /// </summary>
    void Stop();

    /// <summary>
    /// Готовый кадр PCM: s16le, стерео, 48 кГц, ровно 20 мс
    /// </summary>
    event Action<ScreenDemoAudioFrame>? FrameCaptured;
}
