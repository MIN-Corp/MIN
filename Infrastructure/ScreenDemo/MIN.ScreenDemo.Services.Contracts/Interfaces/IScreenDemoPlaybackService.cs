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
    /// Передать полученный кадр звука демонстрации (s16le стерео 48 кГц, 20 мс).
    /// Канал воспроизведения создаётся при первом пакете; кадры переупорядочиваются
    /// по SequenceNumber, разрывы и отстающие кадры отбрасываются.
    /// TimestampMs зарезервирован для будущей синхронизации A/V и пока не используется
    /// </summary>
    void SubmitAudioSamples(Guid roomId, Guid sharerId, long sequenceNumber, long timestampMs, byte[] data);

    /// <summary>
    /// Изменить громкость звука демонстрации указанного потока (0-100).
    /// Применяется и к уже созданному каналу, и запоминается, если канал ещё не создан
    /// </summary>
    void ChangeStreamVolume(Guid roomId, Guid sharerId, int volume);

    /// <summary>
    /// Передать полученный фрагмент кадра на сборку и декодирование.
    /// Кадр с недостающими фрагментами отбрасывается целиком — ждём следующий ключевой кадр
    /// </summary>
    void SubmitFragment(Guid roomId, Guid sharerId, long frameId, int fragmentIndex,
        int fragmentCount, bool isKeyframe, int width, int height, byte[] data);

    /// <summary>
    /// Очистить все потоки демонстраций (при выходе из звонка или комнаты)
    /// </summary>
    void Clear();
}
