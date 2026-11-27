using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MIN.Chat.DI;
using MIN.Chat.Services.Contracts.Interfaces;
using MIN.Core.Cryptography.Contracts.Interfaces;
using MIN.Core.Events.Contracts.Interfaces;
using MIN.Core.Events.Services;
using MIN.Core.Handlers.Contracts;
using MIN.Core.Identity.Contracts.Interfaces;
using MIN.Core.Messaging.Contracts.Interfaces;
using MIN.Core.Services.Contracts.Interfaces.Lifecycle;
using MIN.Core.Services.Contracts.Interfaces.Messaging;
using MIN.Core.Services.Contracts.Interfaces.Moderation;
using MIN.Core.Stores.Contracts.Interfaces;
using MIN.Core.Stores.Contracts.Registries.Interfaces;
using MIN.Core.Stores.Registries;
using MIN.Core.Stores.Services;
using MIN.Core.Transport.Contracts.Interfaces;
using MIN.Discovery.Services.Contracts.Interfaces;
using MIN.Helpers.Contracts.Interfaces;
using MIN.Tests.Common;
using Moq;
using Xunit;

namespace MIN.Chat.Tests;

/// <summary>
/// Смоук-тест DI-модуля Chat: регистрации резолвятся с тестовыми зависимостями
/// </summary>
public class ChatModuleSmokeTests : IDisposable
{
    private readonly TestRoomFactory rooms = new();
    private readonly ServiceProvider provider;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="ChatModuleSmokeTests"/>
    /// </summary>
    public ChatModuleSmokeTests()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerProvider>(new TestLoggerProvider());
        services.AddSingleton<IEventBus>(new InMemoryEventBus(new TestLoggerProvider()));
        services.AddSingleton<IIdentityService>(new TestIdentityService());
        services.AddSingleton(rooms.Factory);
        services.AddSingleton<IRoomStore>(new RoomStore(rooms.Factory));
        services.AddSingleton<IRoomConnectionRegistry>(new RoomConnectionRegistry());
        services.AddSingleton(Mock.Of<IMessageRouter>());
        services.AddSingleton(Mock.Of<IMessageSender>());
        services.AddSingleton(Mock.Of<IRoomLifecycleManager>());
        services.AddSingleton(Mock.Of<INetworkErrorHandler>());
        services.AddSingleton(Mock.Of<IBanService>());
        services.AddSingleton(Mock.Of<IDiscoveryService>());
        services.AddSingleton(Mock.Of<IMessageEncryptor>());
        services.AddSingleton(Mock.Of<ITransport>());
        new ChatModule().Configure(services);
        provider = services.BuildServiceProvider();
    }

    /// <summary>
    /// Сервисы Chat резолвятся
    /// </summary>
    [Fact]
    public void ChatServicesShouldResolve()
    {
        // Act
        var text = provider.GetRequiredService<IChatTextService>();
        var messages = provider.GetRequiredService<IChatMessageService>();
        var status = provider.GetRequiredService<IChatStatusService>();
        var room = provider.GetRequiredService<IChatRoomService>();

        // Assert
        text.Should().NotBeNull();
        messages.Should().NotBeNull();
        status.Should().NotBeNull();
        room.Should().NotBeNull();
    }

    /// <summary>
    /// Обработчики Chat регистрируются через якорь
    /// </summary>
    [Fact]
    public void ChatHandlersShouldResolve()
    {
        // Act
        var handlers = provider.GetServices<IMessageHandler>().ToList();

        // Assert
        handlers.Should().HaveCountGreaterThanOrEqualTo(2);
        handlers.Should().Contain(h => h.HandledTypes.Contains(Core.Messaging.Contracts.MessageTypeTag.ChatTextMessage));
        handlers.Should().Contain(h => h.HandledTypes.Contains(Core.Messaging.Contracts.MessageTypeTag.OnlineStatusChanged));
    }

    /// <summary>
    /// Сообщения Chat регистрируются через якорь
    /// </summary>
    [Fact]
    public void ChatMessagesShouldResolve()
    {
        // Act
        var messages = provider.GetServices<IMessage>().ToList();

        // Assert
        messages.Should().Contain(m => m is Messaging.ChatTextMessage);
        messages.Should().Contain(m => m is Messaging.OnlineStatusChangedMessage);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        provider.Dispose();
        rooms.Dispose();
    }
}
