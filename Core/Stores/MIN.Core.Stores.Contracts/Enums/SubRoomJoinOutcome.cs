namespace MIN.Core.Stores.Contracts.Enums;

/// <summary>
/// Результат входа в подкомнату
/// </summary>
public enum SubRoomJoinOutcome
{
    /// <summary>
    /// Комната не нашлась
    /// </summary>
    RoomNotFound,

    /// <summary>
    /// Подкомната не нашлась
    /// </summary>
    SubRoomNotFound,

    /// <summary>
    /// Участник уже зашёл
    /// </summary>
    AlreadyJoined,

    /// <summary>
    /// Достигнут лимит участников
    /// </summary>
    MaximumParticipants,

    /// <summary>
    /// Успешно вошёл
    /// </summary>
    Success
}
