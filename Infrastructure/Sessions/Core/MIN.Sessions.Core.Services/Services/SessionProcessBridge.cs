using System.Text;
using System.Text.Json;
using MIN.Core.Identity.Contracts.Interfaces;
using MIN.Core.Services.Contracts.Interfaces.Messaging;
using MIN.Helpers.Contracts.Interfaces;
using MIN.Helpers.Contracts.Models.Enums;
using MIN.Sessions.Core.Messaging.Contracts.Models;
using MIN.Sessions.Core.Messaging.Ipc;
using MIN.Sessions.Core.Messaging.OutOfSubRoom;
using MIN.Sessions.Core.Serialization.Contracts;
using MIN.Sessions.Core.Services.Contracts.Interfaces;
using MIN.Sessions.Core.Transport.Contracts.Enums;
using MIN.Sessions.Core.Transport.Contracts.Events;
using MIN.Sessions.Core.Transport.Contracts.Interfaces;
using MIN.Sessions.Core.Transport.Contracts.Models;

namespace MIN.Sessions.Core.Services.Services;

/// <summary>
/// Сервис для отслеживания состояния сессий
/// </summary>
public class SessionProcessBridge : ISessionProcessBridge
{
    private readonly Dictionary<SessionInstanceContext, TaskCompletionSource> pendingProcesses = [];
    private readonly Dictionary<SessionInstanceContext, ISessionProcessTransport> transports = [];
    private readonly IMessageRouter messageRouter;
    private readonly IIpcSerializer ipcSerializer;
    private readonly IIdentityService identityService;
    private readonly ILoggerProvider logger;
    private CancellationTokenSource cts = null!;
    private bool disposed;

    /// <summary>
    /// Инициализирует новый экзепмляр <see cref="SessionProcessBridge"/>
    /// </summary>
    public SessionProcessBridge(IMessageRouter messageRouter,
        IIpcSerializer ipcSerializer,
        IIdentityService identityService,
        ILoggerProvider logger)
    {
        this.messageRouter = messageRouter;
        this.ipcSerializer = ipcSerializer;
        this.identityService = identityService;
        this.logger = logger;
    }

    void ISessionProcessBridge.RegisterTransport(SessionInstanceContext context, ISessionProcessTransport transport)
    {
        transports[context] = transport;
        transport.MessageReceived += OnTransportMessage;
    }

    void ISessionProcessBridge.UnregisterTransport(SessionInstanceContext context)
    {
        if (transports.Remove(context, out var transport))
        {
            transport.MessageReceived -= OnTransportMessage;
        }
    }

    Task ISessionProcessBridge.StartListeningAsync(CancellationToken cancellationToken)
    {
        cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        return Task.CompletedTask;
    }

    private async void OnTransportMessage(object? sender, ProcessTransportMessageEventArgs e)
    {
        try
        {
            var envelope = JsonSerializer.Deserialize<IpcProcessMessageEnvelope>(e.Data);
            if (envelope == null)
            {
                return;
            }

            var message = ipcSerializer.Deserialize(envelope.Body);
            await HandleIpcMessage(message, e.Context, envelope);
        }
        catch (Exception ex)
        {
            logger.Log($"Ошибка получения сообщения с сессии {ex.Message}", LogLevel.Error);
        }
    }

    private async Task HandleIpcMessage(IpcMessage message, SessionInstanceContext context, IpcProcessMessageEnvelope envelope)
    {
        switch (message)
        {
            case InSessionMessage inSessionMessage:
                await messageRouter.RouteAsync(new SessionSpecificMessage()
                {
                    SubRoomId = context.SubRoomId,
                    SessionInstanceRole = context.Role,
                    Body = inSessionMessage.Body,
                    Route = envelope.Route,
                    Channel = envelope.Channel,
                    RecipientId = envelope.RecipientId,
                }, context.RoomId, identityService.SelfParticipant.Id, cts.Token, envelope.BroadcastExcludeIds);
                break;

            case ServerShutdownMessage serverShutdownMessage:
                await messageRouter.RouteAsync(new SessionServerShutdownMessage()
                {
                    SubRoomId = context.SubRoomId,
                    Reason = serverShutdownMessage.Reason,
                }, context.RoomId, identityService.SelfParticipant.Id, cts.Token);
                break;

            case ReadyMessage:
                if (pendingProcesses.TryGetValue(context, out var tcs))
                {
                    tcs.TrySetResult();
                }
                break;

            default:
                return;
        }
    }

    async Task<bool> ISessionProcessBridge.WaitForReadyMessage(SessionInstanceContext context, int timeOutMs, CancellationToken cancellationToken)
    {
        pendingProcesses[context] = new TaskCompletionSource();

        try
        {
            await pendingProcesses[context].Task.WaitAsync(TimeSpan.FromMilliseconds(timeOutMs), cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            return false;
        }
        finally
        {
            pendingProcesses.Remove(context);
        }
        return true;
    }

    IEnumerable<SessionInstanceContext> ISessionProcessBridge.GetConnections(Guid roomId, int subRoomId)
    {
        var result = new List<SessionInstanceContext>();

        foreach (SessionInstanceRole role in Enum.GetValues(typeof(SessionInstanceRole)))
        {
            var context = new SessionInstanceContext(roomId, subRoomId, role);
            transports.TryGetValue(context, out var processTransport);
            if (processTransport != null && processTransport.IsConnectionExists(context) && !pendingProcesses.ContainsKey(context))
            {
                result.Add(context);
            }
        }

        return result;
    }

    async Task ISessionProcessBridge.SendCloseMessage(SessionInstanceContext context, CancellationToken cancellationToken)
        => await SendIpcMessage(new CloseMessage(), context, identityService.SelfParticipant.Id, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task SendIpcMessage(IpcMessage message, SessionInstanceContext context, Guid senderId, CancellationToken cancellationToken)
    {
        var messageData = ipcSerializer.Serialize(message);
        var envelope = new IpcMinMessageEnvelope()
        {
            Body = messageData,
            SenderId = senderId,
        };

        var json = JsonSerializer.Serialize(envelope);
        var data = Encoding.UTF8.GetBytes(json);

        await SendData(data, context, cancellationToken).ConfigureAwait(false);
    }

    private async Task SendData(byte[] data, SessionInstanceContext context, CancellationToken cancellationToken)
    {
        if (!transports.TryGetValue(context, out var processTransport))
        {
            return;
        }

        await processTransport.SendAsync(data, context, cancellationToken);
    }

    Task ISessionProcessBridge.StopListeningAsync(CancellationToken cancellationToken)
    {
        if (disposed)
        {
            return Task.CompletedTask;
        }

        disposed = true;
        cts?.Cancel();
        cts?.Dispose();
        return Task.CompletedTask;
    }
}
