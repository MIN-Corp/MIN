using MIN.Core.Stores.Contracts.Models.SubRooms;

namespace MIN.Voice.Services.Contacts.Interfaces;

/// <summary>
/// Сервис, отслеживающий состояния присутствия в звонках
/// </summary>
public interface IVoiceCallStateService
{
    /// <summary>
    /// Зарегистрировать заинтерисованность в получении звука
    /// </summary>
    void RegisterVoiceCall(Guid roomId, int subRoomId);

    /// <summary>
    /// Получить контекст звонка в комнате
    /// </summary>
    SubRoomContext? GetRoomVoiceCallContext();

    /// <summary>
    /// Отписаться от заинтерисованности в получении звука
    /// </summary>
    void UnregisterVoiceCall();

    /// <summary>
    /// Состою ли я в звонке
    /// </summary>
    bool IsInVoiceCall(Guid? roomId = null, int? subRoomId = null);
}
