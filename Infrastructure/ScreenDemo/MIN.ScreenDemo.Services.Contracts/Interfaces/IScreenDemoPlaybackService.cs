using MIN.ScreenDemo.Services.Contracts.Models;

namespace MIN.ScreenDemo.Services.Contracts.Interfaces;

/// <summary>
/// Сервис приёма и воспроизведения демонстрации экрана (сторона зрителя).
/// Собирает фрагменты в кадры, декодирует и отдаёт готовые кадры для отрисовки
/// </summary>
public interface IScreenDemoPlaybackService : IDisposable
{
    /// <summary>
    /// Декодирован новый кадр демонстрации
    /// </summary>
    event EventHandler<ScreenDemoFrameDecodedEventArgs>? FrameDecoded;

    /// <summary>
    /// Начать приём потока от демонстрирующего участника
    /// </summary>
    void AddStream(Guid roomId, Guid sharerId);

    /// <summary>
    /// Прекратить приём потока от демонстрирующего участника
    /// </summary>
    void RemoveStream(Guid roomId, Guid sharerId);

    /// <summary>
    /// Передать полученный фрагмент кадра на сборку и декодирование.
    /// Кадр с недостающими фрагментами отбрасывается целиком — ждём следующий ключевой кадр
    /// </summary>
    void SubmitFragment(Guid roomId, Guid sharerId, long frameId, int fragmentIndex, int fragmentCount, bool isKeyframe, byte[] data);

    /// <summary>
    /// Очистить все потоки демонстраций (при выходе из звонка или комнаты)
    /// </summary>
    void Clear();
}
