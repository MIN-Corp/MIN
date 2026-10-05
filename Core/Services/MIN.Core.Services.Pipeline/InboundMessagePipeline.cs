using MIN.Common.Core.Contracts.Interfaces;
using MIN.Core.Events.Contracts.Interfaces;
using MIN.Core.Handlers.Contracts.Dispatcher;
using MIN.Core.Handlers.Contracts.Models;
using MIN.Core.Identity.Contracts.Interfaces;
using MIN.Core.Services.Contracts.Events;
using MIN.Core.Services.Contracts.Interfaces.Pipeline;
using MIN.Core.Stores.Contracts.Interfaces;
using MIN.Core.Stores.Contracts.Registries.Models;
using MIN.Core.Streaming.Contracts.Events.Receiving;
using MIN.Core.Streaming.Contracts.Interfaces;
using MIN.Helpers.Contracts.Interfaces;
using MIN.Helpers.Contracts.Models.Enums;

namespace MIN.Core.Services.Pipeline;

/// <summary>
/// Конвейер обработки входящих по сети сообщений
/// </summary>
public sealed class InboundMessagePipeline : IHostedService, IAsyncDisposable
{
    private readonly IChunkBufferAssembler chunkBufferAssembler;
    private readonly IEventBus eventBus;
    private readonly IRoomFactory roomFactory;
    private readonly IMessageDispatcher dispatcher;
    private readonly IAckHandler ackHandler;
    private readonly IStreamChunkHandler streamChunkHandler;
    private readonly IRawMessageHandler messageHandler;
    private readonly IIdentityService identityService;
    private readonly ILoggerProvider logger;

    private CancellationTokenSource cts = null!;
    private IDisposable localRawMessageToken = null!;
    private IDisposable localMessageToken = null!;
    private bool disposed;

    int IHostedService.Priority => 0;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="InboundMessagePipeline"/>
    /// </summary>
    public InboundMessagePipeline(IChunkBufferAssembler chunkBufferAssembler,
        IEventBus eventBus,
        IRoomFactory roomFactory,
        IMessageDispatcher dispatcher,
        IAckHandler ackHandler,
        IStreamChunkHandler streamChunkHandler,
        IRawMessageHandler messageHandler,
        IIdentityService identityService,
        ILoggerProvider logger)
    {
        this.chunkBufferAssembler = chunkBufferAssembler;
        this.eventBus = eventBus;
        this.roomFactory = roomFactory;
        this.dispatcher = dispatcher;
        this.ackHandler = ackHandler;
        this.streamChunkHandler = streamChunkHandler;
        this.messageHandler = messageHandler;
        this.identityService = identityService;
        this.logger = logger;
    }

    Task IHostedService.StartAsync(CancellationToken cancellationToken)
    {
        cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        localRawMessageToken = eventBus.Subscribe<RoomRawMessageReceivedEvent>(OnRoomRawMessageReceived);
        localMessageToken = eventBus.Subscribe<LocalMessageReceivedEvent>(OnLocalMessageRecieved);
        chunkBufferAssembler.MessageAssembled += OnMessageAssembled;
        return Task.CompletedTask;
    }

    private async Task OnRoomRawMessageReceived(RoomRawMessageReceivedEvent eventMessage, CancellationToken cancellationToken)
    {
        var e = eventMessage.EventArgs;

        try
        {
            if (ackHandler.CanHandle(e.Data))
            {
                ackHandler.Handle(e.Data);
                return;
            }

            var plainData = messageHandler.TryDecrypt(e);
            if (plainData == null)
            {
                return;
            }

            if (streamChunkHandler.CanHandle(plainData))
            {
                await streamChunkHandler.HandleAsync(plainData, e, cancellationToken);
                return;
            }

            await messageHandler.HandleRawAsync(e, plainData, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.Log($"Произошла ошибка во время обработки raw message: {ex.Message}", LogLevel.Error);
        }
    }

    private async Task OnLocalMessageRecieved(LocalMessageReceivedEvent e, CancellationToken cancellationToken)
    {
        var context = roomFactory.GetOrCreateContext(e.RoomId);
        await dispatcher.DispatchAsync(e.Message,
            new MessageContext(context, identityService.SelfParticipant.Id,
            CoreRegistryConstants.LocalConnectionId, e.Role, cancellationToken),
            e.BroadcastExcludeIds);
    }

    private async Task OnMessageAssembled(MessageAssembledEventArgs e, CancellationToken cancellationToken)
    {
        try
        {
            if (e.IsRawPayload)
            {
                return;
            }

            await messageHandler.HandleAssembledAsync(e, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.Log($"Ошибка при обработке собранного с потока сообщения: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        chunkBufferAssembler.MessageAssembled -= OnMessageAssembled;

        if (disposed)
        {
            return Task.CompletedTask;
        }

        localRawMessageToken.Dispose();
        localMessageToken.Dispose();
        disposed = true;
        cts?.Cancel();
        cts?.Dispose();
        return Task.CompletedTask;
    }

    /// <inheritdoc cref="IAsyncDisposable.DisposeAsync"/>
    public async ValueTask DisposeAsync() => await StopAsync();
}
