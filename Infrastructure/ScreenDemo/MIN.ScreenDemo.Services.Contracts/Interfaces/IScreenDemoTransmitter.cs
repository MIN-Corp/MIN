using MIN.Core.Stores.Contracts.Models.SubRooms;

namespace MIN.ScreenDemo.Services.Contracts.Interfaces;

/// <summary>
/// Сервис передачи демонстрации экрана (сторона демонстрирующего)
/// </summary>
public interface IScreenDemoTransmitter : IDisposable
{
    /// <summary>
    /// Начать демонстрацию: захват и кодирование идут сразу (локальный предпросмотр),
    /// но данные в сеть не отправляются, пока нет ни одного зрителя
    /// </summary>
    void Begin(SubRoomContext subRoomContext);

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
