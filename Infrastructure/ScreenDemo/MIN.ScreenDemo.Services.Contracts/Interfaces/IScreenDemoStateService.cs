using MIN.ScreenDemo.Services.Contracts.Models;

namespace MIN.ScreenDemo.Services.Contracts.Interfaces;

/// <summary>
/// Сервис отслеживания активных демонстраций экрана
/// </summary>
public interface IScreenDemoStateService
{
    /// <summary>
    /// Получить все активные демонстрации в комнате
    /// </summary>
    IReadOnlyList<ScreenDemoSessionInfo> GetActiveDemos(Guid roomId);

    /// <summary>
    /// Получить демонстрацию по подкомнате
    /// </summary>
    ScreenDemoSessionInfo? GetDemo(Guid roomId, int subRoomId);

    /// <summary>
    /// Получить собственную активную демонстрацию (если участник сейчас демонстрирует экран)
    /// </summary>
    ScreenDemoSessionInfo? GetOwnDemo(Guid roomId, Guid selfParticipantId);

    /// <summary>
    /// Зарегистрировать начало демонстрации
    /// </summary>
    void AddDemo(ScreenDemoSessionInfo demo);

    /// <summary>
    /// Добавить зрителя демонстрации
    /// </summary>
    void AddViewer(Guid roomId, int subRoomId, Guid viewerId);

    /// <summary>
    /// Убрать зрителя демонстрации
    /// </summary>
    void RemoveViewer(Guid roomId, int subRoomId, Guid viewerId);

    /// <summary>
    /// Смотрит ли участник данную демонстрацию (или сам её ведёт)
    /// </summary>
    bool IsWatching(Guid roomId, int subRoomId, Guid participantId);

    /// <summary>
    /// Убрать завершённую демонстрацию
    /// </summary>
    void RemoveDemo(Guid roomId, int subRoomId);

    /// <summary>
    /// Убрать все демонстрации комнаты (при выходе из звонка/комнаты).
    /// Если roomId равен null — очистить всё
    /// </summary>
    void Clear(Guid? roomId = null);
}
