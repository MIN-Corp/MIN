using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MIN.Core.Events.Contracts.Interfaces;
using MIN.Core.Events.Services;
using MIN.Core.Stores.Contracts.Interfaces;
using MIN.Core.Stores.Services;
using MIN.Discovery.DI;
using MIN.Discovery.Services.Contracts.Interfaces;
using MIN.Discovery.Transport.Contracts;
using MIN.Helpers.Contracts.Interfaces;
using MIN.Helpers.Contracts.Interfaces.SettingsServices;
using MIN.Helpers.Contracts.Models;
using MIN.Tests.Common;
using Moq;
using Xunit;

namespace MIN.Discovery.Tests;

/// <summary>
/// Смоук-тест DI-модуля Discovery
/// </summary>
public class DiscoveryModuleSmokeTests : IAsyncDisposable
{
    private readonly TestRoomFactory rooms = new();
    private readonly ServiceProvider provider;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="DiscoveryModuleSmokeTests"/>
    /// </summary>
    public DiscoveryModuleSmokeTests()
    {
        var settings = new Mock<ISettingsProvider>();
        settings.Setup(s => s.GetSettings()).Returns(new Settings());
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerProvider>(new TestLoggerProvider());
        services.AddSingleton<IEventBus>(new InMemoryEventBus(new TestLoggerProvider()));
        services.AddSingleton<IRoomStore>(new RoomStore(rooms.Factory));
        services.AddSingleton(TestSerializer.Create());
        services.AddSingleton(settings.Object);
        services.AddSingleton<IAppDataProvider>(new TestAppDataProvider());
        new DiscoveryModule().Configure(services);
        provider = services.BuildServiceProvider();
    }

    /// <summary>
    /// Сервис и транспорт обнаружения резолвятся
    /// </summary>
    [Fact]
    public void DiscoveryServicesShouldResolve()
    {
        // Act
        var service = provider.GetRequiredService<IDiscoveryService>();
        var transport = provider.GetRequiredService<IDiscoveryTransport>();

        // Assert
        service.Should().NotBeNull();
        transport.Should().NotBeNull();
        service.DiscoveryMethod.Should().Be(Services.Contracts.Enums.DiscoveryMethod.Local);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await provider.DisposeAsync();
        rooms.Dispose();
    }
}
