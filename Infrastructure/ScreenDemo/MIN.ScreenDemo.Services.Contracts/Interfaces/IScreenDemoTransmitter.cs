using MIN.Core.Stores.Contracts.Models.SubRooms;
using MIN.ScreenDemo.Services.Contracts.Models;

namespace MIN.ScreenDemo.Services.Contracts.Interfaces;

/// <summary>
/// Сервис передачи демонстрации экрана (сторона демонстрирующего)
/// </summary>
public interface IScreenDemoTransmitter : IDisposable
{
    /// <summary>
    /// Локальный кадр перед кодированием (для предпросмотра).
    /// ВАЖНО: буфер Data принадлежит трансмиттеру и переиспользуется —
    /// подписчик должен использовать кадр синхронно, внутри обработчика события
    /// </summary>
    event EventHandler<ScreenFrame>? FramePreview;

    /// <summary>
    /// Начать демонстрацию: захват и кодирование идут сразу (локальный предпросмотр),
    /// но данные в сеть не отправляются, пока нет ни одного зрителя
    /// </summary>
    void Begin(SubRoomContext subRoomContext, ScreenSourceInfo source);

    /// <summary>
    /// Добавить зрителя — с этого момента начинается отправка данных в сеть
    /// </summary>
    void AddViewer(Guid participantId);

    /// <summary>
    /// Убрать зрителя — когда зрителей не осталось, отправка в сеть прекращается
    /// (захват и предпросмотр продолжаются)
    /// </summary>
    void RemoveViewer(Guid participantId);

    /// <summary>
    /// Остановить демонстрацию полностью
    /// </summary>
    void End();
}
