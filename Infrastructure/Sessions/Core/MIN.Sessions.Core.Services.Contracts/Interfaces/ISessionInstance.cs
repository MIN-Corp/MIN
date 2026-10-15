using MIN.Core.Entities.Contracts.Models;
using MIN.Sessions.Core.Services.Contracts.Models;
using MIN.Sessions.Core.Transport.Contracts.Models;

namespace MIN.Sessions.Core.Services.Contracts.Interfaces;

/// <summary>
/// Экземпляр сессии (программа или туннель)
/// </summary>
public interface ISessionInstance : IAsyncDisposable
{
    /// <summary>
    /// Контекст общения с экземпляром сессии
    /// </summary>
    SessionInstanceContext Context { get; }

    /// <summary>
    /// Запустить экземпляр сессии
    /// </summary>
    Task StartAsync(Session session, CancellationToken ct);      // returns when instance is ready

    /// <summary>
    /// Остановить экземпляр сессии
    /// </summary>
    Task StopAsync(CancellationToken ct);

    /// <summary>
    /// Оповестить приход участника в сессию
    /// </summary>
    void NotifyParticipantJoined(ParticipantInfo participant);   // encapsulates the IPC notify currently in handlers

    /// <summary>
    /// Оповестить уход участника из сессии
    /// </summary>
    void NotifyParticipantLeft(Guid participantId);

    /// <summary>
    /// Событие по окончании сессии
    /// </summary>
    event EventHandler? Ended;
}
