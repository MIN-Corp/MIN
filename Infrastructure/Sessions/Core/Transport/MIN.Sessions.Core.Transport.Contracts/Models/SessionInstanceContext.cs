using MIN.Sessions.Core.Transport.Contracts.Enums;

namespace MIN.Sessions.Core.Transport.Contracts.Models;

/// <summary>
/// Контекст общения с экземпляром сессии (программа или туннель)
/// </summary>
/// <param name="RoomId">Идентификатор комнаты</param>
/// <param name="SubRoomId">Идентификатор подкомнаты</param>
/// <param name="Role">Роль экземпляра сессии</param>
public record struct SessionInstanceContext(
    Guid RoomId,
    int SubRoomId,
    SessionInstanceRole Role
);
