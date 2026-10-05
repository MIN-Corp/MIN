using MIN.Common.Core.Contracts.Interfaces;
using MIN.Core.Services.Contracts.Interfaces.Persistence;

namespace MIN.Core.Services.Persistence;

/// <inheritdoc cref="IRoomPersistenceService"/>
public class RoomPersistenceHostedServiceAdapter : IHostedService
{
    private readonly IRoomPersistenceService persistence;

    int IHostedService.Priority => 1;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="RoomPersistenceHostedServiceAdapter"/>
    /// </summary>
    public RoomPersistenceHostedServiceAdapter(IRoomPersistenceService persistence)
    {
        this.persistence = persistence;
    }

    Task IHostedService.StartAsync(CancellationToken cancellationToken)
        => persistence.StartAsync(cancellationToken);

    Task IHostedService.StopAsync(CancellationToken cancellationToken)
        => persistence.StopAsync(cancellationToken);
}
