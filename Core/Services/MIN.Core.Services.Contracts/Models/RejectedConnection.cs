namespace MIN.Core.Services.Contracts.Models;

/// <summary>
/// Соединение, ожидающее подтверждения отклонения
/// </summary>
public sealed record RejectedConnection(Guid RoomId, Guid ConnectionId, bool KickParticipant);
