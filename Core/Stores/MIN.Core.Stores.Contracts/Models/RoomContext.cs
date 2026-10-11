using Microsoft.Extensions.DependencyInjection;
using MIN.Core.Stores.Contracts.Interfaces;
using MIN.Core.Stores.Contracts.Registries.Interfaces;

namespace MIN.Core.Stores.Contracts.Models;

/// <summary>
/// Контекст комнаты
/// </summary>
public sealed class RoomContext : IDisposable
{
    /// <summary>
    /// Идентфикатор комнаты
    /// </summary>
    public Guid RoomId { get; }

    /// <inheritdoc cref="IMessageStore"/>
    public IMessageStore Messages { get; }

    /// <inheritdoc cref="IParticipantStore"/>
    public IParticipantStore Participants { get; }

    /// <inheritdoc cref="IParticipantConnectionRegistry"/>
    public IParticipantConnectionRegistry Connections { get; }

    /// <inheritdoc cref="ISubRoomManager"/>
    public ISubRoomManager SubRooms { get; }

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="RoomContext"/>
    /// </summary>
    public RoomContext(Guid roomId, IServiceProvider serviceProvider)
    {
        RoomId = roomId;

        Messages = serviceProvider.GetRequiredService<IMessageStore>();
        Participants = serviceProvider.GetRequiredService<IParticipantStore>();
        Connections = serviceProvider.GetRequiredService<IParticipantConnectionRegistry>();
        SubRooms = serviceProvider.GetRequiredService<ISubRoomManager>();
    }

    /// <inheritdoc cref="IDisposable.Dispose"/>
    public void Dispose()
    {
        Messages.ClearMessages();
        Participants.ClearParticipants();
        SubRooms.ClearRoomSubRooms();
    }
}
