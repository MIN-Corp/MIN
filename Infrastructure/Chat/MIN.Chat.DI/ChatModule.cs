using Microsoft.Extensions.DependencyInjection;
using MIN.Chat.DI.FeatureCollection;
using MIN.Chat.Handlers;
using MIN.Chat.Messaging;
using MIN.Chat.Messaging.Migrations;
using MIN.Chat.Services;
using MIN.Common.Mvc;
using MIN.Common.Mvc.Extensions;
using MIN.Core.Handlers.Contracts;
using MIN.Core.Messaging.Contracts.Interfaces;
using MIN.Core.Stores.Contracts.Interfaces;

namespace MIN.Chat.DI;

/// <summary>
/// Модуль регистрации зависимостей для Chat
/// </summary>
public class ChatModule : Module
{
    /// <inheritdoc />
    protected override void Load(IServiceCollection services)
    {
        services.RegisterMultipleInterfacesAssignableFromAnchor<IMessage, IChatMessagingAnchor>(ServiceLifetime.Singleton);
        services.RegisterAsImplementedInterfaces<ChatRoomService>(ServiceLifetime.Singleton);
        services.RegisterAsImplementedInterfaces<ChatMessageService>(ServiceLifetime.Singleton);
        services.RegisterAsImplementedInterfaces<ChatTextService>(ServiceLifetime.Singleton);
        services.RegisterAsImplementedInterfaces<ChatStatusService>(ServiceLifetime.Singleton);
        services.RegisterAsImplementedInterfaces<ChatFileService>(ServiceLifetime.Singleton);
        services.RegisterAsImplementedInterfaces<ChatSessionService>(ServiceLifetime.Singleton);
        services.RegisterAsImplementedInterfaces<ChatVoiceService>(ServiceLifetime.Singleton);
        services.RegisterMultipleInterfacesAssignableFromAnchor<IMessageHandler, IChatHandlerAnchor>(ServiceLifetime.Singleton);
        services.RegisterMultipleInterfacesAssignableTo<IMessageMigrator, ChatTextMessageMigrator>(ServiceLifetime.Singleton);
        services.RegisterAsImplementedInterfaces<ChatFeatureCollection>(ServiceLifetime.Singleton);
    }
}
