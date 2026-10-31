using MIN.Core.Entities.Contracts.Models;

namespace MIN.ScreenDemo.Services.Contracts.Models;

/// <summary>
/// Информация об активной демонстрации экрана в комнате
/// </summary>
public sealed record ScreenDemoSessionInfo(
    Guid RoomId,
    int SubRoomId,
    ParticipantInfo Sharer,
    IReadOnlyList<Guid> ViewerIds);
