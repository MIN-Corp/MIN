using MIN.Core.Stores.Contracts.Models.SubRooms;

namespace MIN.Voice.Services.Contacts.Interfaces;

/// <summary>
/// Сервис передачи звука
/// </summary>
public interface IVoiceDataTransmitter : IDisposable
{
    /// <summary>
    /// Начать передачу звука
    /// </summary>
    void Begin(SubRoomContext subRoomContext);

    /// <summary>
    /// Остановить передачу звука
    /// </summary>
    void End();
}
