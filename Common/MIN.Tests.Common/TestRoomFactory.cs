using Microsoft.Extensions.DependencyInjection;
using MIN.Core.Stores.Contracts.Interfaces;
using MIN.Core.Stores.Contracts.Models;
using MIN.Core.Stores.Contracts.Registries.Interfaces;
using MIN.Core.Stores.Factories;
using MIN.Core.Stores.Registries;
using MIN.Core.Stores.Services;

namespace MIN.Tests.Common;

/// <summary>
/// Фабрика комнат с настоящими DI-регистрациями сторов
/// </summary>
public sealed class TestRoomFactory : IDisposable
{
    private readonly ServiceProvider provider;

    /// <summary>
    /// Фабрика контекстов комнат
    /// </summary>
    public IRoomFactory Factory { get; }

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="TestRoomFactory"/>
    /// </summary>
    public TestRoomFactory()
    {
        var services = new ServiceCollection();
        services.AddTransient<IMessageStore, MessageStore>();
        services.AddTransient<IParticipantStore, ParticipantStore>();
        services.AddTransient<IParticipantConnectionRegistry, ParticipantConnectionRegistry>();
        services.AddTransient<ISubRoomManager, SubRoomManager>();
        provider = services.BuildServiceProvider();
        Factory = new RoomFactory(provider);
    }

    /// <summary>
    /// Создать контекст комнаты
    /// </summary>
    public RoomContext CreateContext(Guid? roomId = null)
        => Factory.GetOrCreateContext(roomId ?? Guid.NewGuid());

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var context in Factory.GetAllContexts())
        {
            context.Dispose();
        }

        provider.Dispose();
    }
}
