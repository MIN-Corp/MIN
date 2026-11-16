using MIN.Chat.Services.Contracts.Interfaces;

namespace MIN.Chat.DI.FeatureCollection;

/// <inheritdoc cref="IChatFeatureCollection"/>
public class ChatFeatureCollection : IChatFeatureCollection
{
    /// <inheritdoc cref="IChatRoomService"/>
    public IChatRoomService ChatRoomService { get; }

    /// <inheritdoc cref="IChatMessageService"/>
    public IChatMessageService ChatMessageService { get; }

    /// <inheritdoc cref="IChatTextService"/>
    public IChatTextService ChatTextService { get; }

    /// <inheritdoc cref="IChatStatusService"/>
    public IChatStatusService ChatStatusService { get; }

    /// <inheritdoc cref="IChatFileService"/>
    public IChatFileService ChatFileService { get; }

    /// <inheritdoc cref="IChatSessionService"/>
    public IChatSessionService ChatSessionService { get; }

    /// <inheritdoc cref="IChatVoiceService"/>
    public IChatVoiceService ChatVoiceService { get; }

    /// <inheritdoc cref="IChatScreenDemoService"/>
    public IChatScreenDemoService ChatScreenDemoService { get; }

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="ChatFeatureCollection"/>
    /// </summary>
    public ChatFeatureCollection(IChatRoomService chatRoomService,
        IChatMessageService chatMessageService,
        IChatTextService chatTextService,
        IChatStatusService chatStatusService,
        IChatFileService chatFileService,
        IChatSessionService chatSessionService,
        IChatVoiceService chatVoiceService,
        IChatScreenDemoService chatScreenDemoService)
    {
        ChatRoomService = chatRoomService;
        ChatMessageService = chatMessageService;
        ChatTextService = chatTextService;
        ChatStatusService = chatStatusService;
        ChatFileService = chatFileService;
        ChatSessionService = chatSessionService;
        ChatVoiceService = chatVoiceService;
        ChatScreenDemoService = chatScreenDemoService;
    }
}
