using Microsoft.Extensions.DependencyInjection;
using MIN.Chat.Handlers;
using MIN.Common.Mvc.Extensions;
using MIN.Core.Cryptography.Contracts.Interfaces;
using MIN.Core.Events.Contracts.Interfaces;
using MIN.Core.Events.Services;
using MIN.Core.Handlers.Contracts;
using MIN.Core.Handlers.Handlers;
using MIN.Core.Identity.Contracts.Interfaces;
using MIN.Core.Messaging.Contracts;
using MIN.Core.Services.Contracts.Interfaces.Lifecycle;
using MIN.Core.Services.Contracts.Interfaces.Messaging;
using MIN.Core.Services.Contracts.Interfaces.Moderation;
using MIN.Core.Services.Contracts.Interfaces.Persistence;
using MIN.Core.Services.Moderation;
using MIN.Core.Stores.Contracts.Interfaces;
using MIN.Core.Stores.Contracts.Registries.Interfaces;
using MIN.Core.Stores.Registries;
using MIN.Core.Stores.Services;
using MIN.Core.Transport.Contracts.Interfaces;
using MIN.Helpers.Contracts.Interfaces;
using MIN.Tests.Common;
using Moq;

namespace MIN.Core.Handlers.Tests;

/// <summary>
/// DI-контейнер с настоящими обработчиками Core и Chat и тестовыми зависимостями
/// </summary>
public sealed class HandlerTestFactory : IDisposable
{
    private readonly ServiceProvider provider;
    private readonly TestRoomFactory rooms = new();

    /// <summary>
    /// Логгер
    /// </summary>
    public TestLoggerProvider Logger { get; } = new();

    /// <summary>
    /// Шина событий
    /// </summary>
    public InMemoryEventBus EventBus { get; }

    /// <summary>
    /// Хранилище комнат
    /// </summary>
    public IRoomStore RoomStore { get; }

    /// <summary>
    /// Транспорт в памяти
    /// </summary>
    public InMemoryTransport Transport { get; } = new();

    /// <summary>
    /// Реестр соединений комнат
    /// </summary>
    public IRoomConnectionRegistry RoomConnections { get; } = new RoomConnectionRegistry();

    /// <summary>
    /// Identity
    /// </summary>
    public TestIdentityService Identity { get; } = new();

    /// <summary>
    /// Мок отправителя сообщений
    /// </summary>
    public Mock<IMessageSender> MessageSender { get; } = new();

    /// <summary>
    /// Мок менеджера жизненного цикла комнат
    /// </summary>
    public Mock<IRoomLifecycleManager> Lifecycle { get; } = new();

    /// <summary>
    /// Мок маршрутизатора
    /// </summary>
    public Mock<IMessageRouter> Router { get; } = new();

    /// <summary>
    /// Мок провайдера версии
    /// </summary>
    public Mock<IVersionProvider> Version { get; } = new();

    /// <summary>
    /// Мок шифратора
    /// </summary>
    public Mock<IMessageEncryptor> Encryptor { get; } = new();

    /// <summary>
    /// Мок сервиса персистентности
    /// </summary>
    public Mock<IRoomPersistenceService> Persistence { get; } = new();

    /// <summary>
    /// Мок обработчика сетевых ошибок
    /// </summary>
    public Mock<INetworkErrorHandler> NetworkErrorHandler { get; } = new();

    /// <summary>
    /// Мок сервиса банов
    /// </summary>
    private IBanService BanService { get; }

    /// <summary>
    /// Мок хешера паролей комнат
    /// </summary>
    public Mock<IPasswordHasher> PasswordHasher { get; } = new();

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="HandlerTestFactory"/>
    /// </summary>
    public HandlerTestFactory()
    {
        EventBus = new InMemoryEventBus(Logger);
        RoomStore = new RoomStore(rooms.Factory);
        BanService = new BanService(RoomStore, Persistence.Object);
        Version.SetupGet(v => v.Version).Returns(new Version(2, 2, 0, 0));
        Version.Setup(v => v.IsVersionCompatible(It.IsAny<Version>())).Returns(true);

        var services = new ServiceCollection();
        services.AddSingleton<ILoggerProvider>(Logger);
        services.AddSingleton<IEventBus>(EventBus);
        services.AddSingleton(RoomStore);
        services.AddSingleton<ITransport>(Transport);
        services.AddSingleton(RoomConnections);
        services.AddSingleton<IIdentityService>(Identity);
        services.AddSingleton(MessageSender.Object);
        services.AddSingleton(Lifecycle.Object);
        services.AddSingleton(Router.Object);
        services.AddSingleton(Version.Object);
        services.AddSingleton(Encryptor.Object);
        services.AddSingleton(Persistence.Object);
        services.AddSingleton(NetworkErrorHandler.Object);
        services.AddSingleton(BanService);
        services.AddSingleton(PasswordHasher.Object);
        services.RegisterMultipleInterfacesAssignableFromAnchor<IMessageHandler, ICoreHandlerAnchor>(ServiceLifetime.Singleton);
        services.RegisterMultipleInterfacesAssignableFromAnchor<IMessageHandler, IChatHandlerAnchor>(ServiceLifetime.Singleton);
        provider = services.BuildServiceProvider();
    }

    /// <summary>
    /// Фабрика контекстов комнат
    /// </summary>
    public IRoomFactory RoomFactory => rooms.Factory;

    /// <summary>
    /// Все обработчики указанного типа сообщения
    /// </summary>
    public IEnumerable<IMessageHandler> GetHandlers(MessageTypeTag tag)
        => provider.GetServices<IMessageHandler>().Where(h => h.HandledTypes.Contains(tag));

    /// <summary>
    /// Первый обработчик указанного типа сообщения
    /// </summary>
    public IMessageHandler GetHandler(MessageTypeTag tag) => GetHandlers(tag).First();

    /// <inheritdoc />
    public void Dispose()
    {
        provider.Dispose();
        rooms.Dispose();
    }
}
