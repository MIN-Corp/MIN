using MIN.Chat.Messaging;
using MIN.Core.Messaging;
using MIN.Core.Messaging.Contracts.Interfaces;
using MIN.Core.Serialization.Contracts.Interfaces;
using MIN.Core.Serialization.Json;
using MIN.Discovery.Messaging;
using MIN.FileTransfer.Messaging;
using MIN.ScreenDemo.Messaging;
using MIN.Sessions.Core.Messaging;
using MIN.Voice.Messaging;

namespace MIN.Tests.Common;

/// <summary>
/// Фабрика сериализаторов сообщений
/// </summary>
public static class TestSerializer
{
    private readonly static Lazy<IReadOnlyList<IMessage>> allMessages = new(CollectAllMessages);

    /// <summary>
    /// Экземпляры всех типов сообщений из всех модулей (по якорям сборок)
    /// </summary>
    public static IReadOnlyList<IMessage> AllMessages => allMessages.Value;

    /// <summary>
    /// Создать сериализатор: с указанными типами сообщений или со всеми известными
    /// </summary>
    public static IMessageSerializer Create(params IMessage[] messageTypes)
        => new JsonMessageSerializer(messageTypes.Length > 0 ? messageTypes : AllMessages);

    private static List<IMessage> CollectAllMessages()
    {
        var anchors = new[]
        {
            typeof(ICoreMessagingAnchor).Assembly,
            typeof(IChatMessagingAnchor).Assembly,
            typeof(IDiscoveryMessagingAnchor).Assembly,
            typeof(IFileTransferMessagingAnchor).Assembly,
            typeof(ISessionsMessagingAnchor).Assembly,
            typeof(IVoiceMessagingAnchor).Assembly,
            typeof(IScreenDemoMessagingAnchor).Assembly,
        };

        return anchors
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => typeof(IMessage).IsAssignableFrom(type)
                && type.IsClass
                && !type.IsAbstract
                && type.GetConstructor(Type.EmptyTypes) != null)
            .Select(type => (IMessage)Activator.CreateInstance(type)!)
            .ToList();
    }
}
