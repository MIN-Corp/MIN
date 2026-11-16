using MIN.Core.Entities.Contracts.Models;

namespace MIN.ScreenDemo.Services.Contracts.Models;

/// <summary>
/// Информация об активной демонстрации экрана (для синхронизации состояния)
/// </summary>
public sealed record ScreenDemoStateEntry(
    int SubRoomId,
    ParticipantInfo Sharer,
    List<Guid> ViewerIds);
