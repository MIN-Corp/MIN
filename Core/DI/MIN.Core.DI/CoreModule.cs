using Microsoft.Extensions.DependencyInjection;
using MIN.Common.Core.Contracts.Interfaces;
using MIN.Common.Mvc;
using MIN.Common.Mvc.Extensions;
using MIN.Core.Cryptography;
using MIN.Core.DI.FeatureCollection;
using MIN.Core.Events.Services;
using MIN.Core.Handlers.Contracts;
using MIN.Core.Handlers.Dispatcher;
using MIN.Core.Handlers.Handlers;
using MIN.Core.Headers.Services;
using MIN.Core.Identity;
using MIN.Core.Messaging;
using MIN.Core.Messaging.Contracts.Interfaces;
using MIN.Core.Protocol.Services;
using MIN.Core.Serialization.Json;
using MIN.Core.Serialization.Json.Services;
using MIN.Core.Services.Lifecycle;
using MIN.Core.Services.Messaging;
using MIN.Core.Services.Moderation;
using MIN.Core.Services.Pipeline;
using MIN.Core.Stores.Factories;
using MIN.Core.Stores.Registries;
using MIN.Core.Stores.Services;
using MIN.Core.Streaming.Services;
using MIN.Core.SubRooms.Services;
using MIN.Core.Transport;

namespace MIN.Core.DI;

/// <summary>
/// Модуль регистрации зависимостей для Core
/// </summary>
public class CoreModule : Module
{
    /// <inheritdoc />
    protected override void Load(IServiceCollection services)
    {
        // Global
        services.RegisterAsImplementedInterfaces<IdentityService>(ServiceLifetime.Singleton);

        services.RegisterAsImplementedInterfaces<JsonMessageSerializer>(ServiceLifetime.Singleton);

        services.AddDataProtection();

        services.RegisterAsImplementedInterfaces<KeyProvider>(ServiceLifetime.Singleton);
        services.RegisterAsImplementedInterfaces<FileSystemKeyStorage>(ServiceLifetime.Singleton);
        services.RegisterAsImplementedInterfaces<MessageEncryptor>(ServiceLifetime.Singleton);

        services.RegisterAsImplementedInterfaces<ChannelTransport>(ServiceLifetime.Singleton);
        services.RegisterAsImplementedInterfaces<ClientHandshakeService>(ServiceLifetime.Singleton);
        services.RegisterAsImplementedInterfaces<HostHandshakeService>(ServiceLifetime.Singleton);

        services.RegisterAsImplementedInterfaces<HeaderManager>(ServiceLifetime.Singleton);

        services.RegisterAsImplementedInterfaces<RoomLifecycleManager>(ServiceLifetime.Singleton);

        services.RegisterAsImplementedInterfaces<RoomFactory>(ServiceLifetime.Singleton);

        services.RegisterAsImplementedInterfaces<RoomStore>(ServiceLifetime.Singleton);

        // Room-scoped
        services.RegisterAsImplementedInterfaces<NetworkErrorHandler>(ServiceLifetime.Singleton);
        services.RegisterAsImplementedInterfaces<RoomConnectionRegistry>(ServiceLifetime.Singleton);

        services.RegisterAsImplementedInterfaces<ParticipantConnectionRegistry>(ServiceLifetime.Transient);
        services.RegisterAsImplementedInterfaces<MessageStore>(ServiceLifetime.Transient);
        services.RegisterAsImplementedInterfaces<ParticipantStore>(ServiceLifetime.Transient);

        services.RegisterAsImplementedInterfaces<SubRoomManager>(ServiceLifetime.Singleton);

        services.RegisterAsImplementedInterfaces<MessageSender>(ServiceLifetime.Singleton);
        services.RegisterAsImplementedInterfaces<MessageRouter>(ServiceLifetime.Singleton);

        services.RegisterAsImplementedInterfaces<ChunkBufferAssembler>(ServiceLifetime.Singleton);
        services.RegisterAsImplementedInterfaces<StreamManager>(ServiceLifetime.Singleton);

        services.RegisterAsImplementedInterfaces<RawMessageHandler>(ServiceLifetime.Singleton);
        services.RegisterAsImplementedInterfaces<AckHandler>(ServiceLifetime.Singleton);
        services.RegisterAsImplementedInterfaces<StreamChunkHandler>(ServiceLifetime.Singleton);

        services.RegisterAsImplementedInterfaces<InMemoryEventBus>(ServiceLifetime.Singleton);
        services.RegisterAsImplementedInterfaces<MessageDispatcher>(ServiceLifetime.Singleton);

        services.RegisterMultipleInterfacesAssignableFromAnchor<IMessageHandler, ICoreHandlerAnchor>(ServiceLifetime.Singleton);
        services.RegisterMultipleInterfacesAssignableFromAnchor<IMessage, ICoreMessagingAnchor>(ServiceLifetime.Singleton);

        services.RegisterMultipleInterfacesAssignableTo<IHostedService, JsonOptionsInitializer>(ServiceLifetime.Singleton);
        services.RegisterMultipleInterfacesAssignableTo<IHostedService, InboundMessagePipeline>(ServiceLifetime.Singleton);

        services.RegisterAsImplementedInterfaces<CoreFeatureCollection>(ServiceLifetime.Singleton);
    }
}
