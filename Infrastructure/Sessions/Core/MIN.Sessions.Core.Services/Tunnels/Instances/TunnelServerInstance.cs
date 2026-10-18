using System.Net.Sockets;
using MIN.Core.Entities.Contracts.Models;
using MIN.Core.Identity.Contracts.Interfaces;
using MIN.Core.Services.Contracts.Interfaces.Messaging;
using MIN.Helpers.Contracts.Interfaces;
using MIN.Sessions.Core.Messaging.Contracts.Enums;
using MIN.Sessions.Core.Messaging.OutOfSubRoom;
using MIN.Sessions.Core.Services.Contracts.Interfaces;
using MIN.Sessions.Core.Services.Contracts.Models;
using MIN.Sessions.Core.Services.Instances.Tunnels;
using MIN.Sessions.Core.Transport.Contracts.Enums;
using MIN.Sessions.Core.Transport.Contracts.Models;

namespace MIN.Sessions.Core.Services.Tunnels.Instances;

/// <summary>
/// Экземпляр туннеля на стороне хоста: подключается к порту игры при входе участника
/// </summary>
public class TunnelServerInstance : ISessionInstance
{
    private const int GameConnectTimeoutMs = 5_000;

    private readonly IMessageRouter messageRouter;
    private readonly IIdentityService identityService;
    private readonly ILoggerProvider logger;

    private readonly Dictionary<Guid, TunnelServerParticipantConnection> connections = [];
    private readonly CancellationTokenSource cts = new();

    private Session? session;

    /// <inheritdoc />
    public SessionInstanceContext Context { get; }

    /// <inheritdoc />
    public event EventHandler? Ended;

    /// <inheritdoc />
    public bool IsJoinAnnounceDeferred => false;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="TunnelServerInstance"/>
    /// </summary>
    public TunnelServerInstance(SessionInstanceContext context,
        IMessageRouter messageRouter,
        IIdentityService identityService,
        ILoggerProvider logger)
    {
        Context = context;
        this.messageRouter = messageRouter;
        this.identityService = identityService;
        this.logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(Session session, CancellationToken ct)
    {
        this.session = session;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    async Task ISessionInstance.SendDataAsync(byte[] frame, Guid senderId, CancellationToken ct)
    {
        var (type, payload) = TunnelProtocol.Decode(frame);

        switch (type)
        {
            case TunnelFrameType.Hello:
                await HandleHelloAsync(senderId).ConfigureAwait(false);
                break;

            case TunnelFrameType.Data:
                if (connections.TryGetValue(senderId, out var dataConnection))
                {
                    await dataConnection.WriteAsync(payload).ConfigureAwait(false);
                }
                break;

            case TunnelFrameType.Close:
                await RemoveConnectionAsync(senderId, notifyPeer: false).ConfigureAwait(false);
                break;
        }
    }

    /// <inheritdoc />
    public void NotifyParticipantJoined(ParticipantInfo participant)
    {
    }

    /// <inheritdoc />
    public void NotifyParticipantLeft(Guid participantId)
        => _ = RemoveConnectionAsync(participantId, notifyPeer: false);

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken ct)
    {
        cts.Cancel();

        foreach (var participantId in connections.Keys.ToList())
        {
            await RemoveConnectionAsync(participantId, notifyPeer: false).ConfigureAwait(false);
        }

        Ended?.Invoke(this, EventArgs.Empty);
        cts.Dispose();
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

    private async Task HandleHelloAsync(Guid participantId)
    {
        if (session == null)
        {
            return;
        }

        if (connections.ContainsKey(participantId))
        {
            return;
        }

        if (session.MaximumParticipants.HasValue && connections.Count >= session.MaximumParticipants.Value)
        {
            await SendFrameAsync(participantId, TunnelFrameType.HelloFail,
                "Достигнут лимит игроков в сессии"u8.ToArray()).ConfigureAwait(false);
            return;
        }

        var port = session.TunnelPort;
        if (port == null)
        {
            await SendFrameAsync(participantId, TunnelFrameType.HelloFail,
                "Порт игры не указан"u8.ToArray()).ConfigureAwait(false);
            return;
        }

        try
        {
            var client = new TcpClient();
            using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);
            connectCts.CancelAfter(GameConnectTimeoutMs);
            await client.ConnectAsync("127.0.0.1", port.Value, connectCts.Token).ConfigureAwait(false);

            var connection = new TunnelServerParticipantConnection(participantId, client);
            connections[participantId] = connection;
            _ = ReadLoopAsync(connection);

            await SendFrameAsync(participantId, TunnelFrameType.HelloOk).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.Log($"Туннель: не удалось подключиться к порту игры {port}: {ex.Message}",
                Helpers.Contracts.Models.Enums.LogLevel.Error);
            await SendFrameAsync(participantId, TunnelFrameType.HelloFail,
                "Хост ещё не открыл игровой сервер"u8.ToArray()).ConfigureAwait(false);
        }
    }

    private async Task ReadLoopAsync(TunnelServerParticipantConnection connection)
    {
        var buffer = new byte[TunnelProtocol.ChunkSize];

        try
        {
            while (!cts.IsCancellationRequested)
            {
                var read = await connection.Stream.ReadAsync(buffer, cts.Token).ConfigureAwait(false);

                if (read == 0)
                {
                    break;
                }

                var frame = TunnelProtocol.Encode(TunnelFrameType.Data, buffer.AsSpan(0, read));

                await messageRouter.RouteAsync(new SessionSpecificMessage()
                {
                    SubRoomId = Context.SubRoomId,
                    SessionInstanceRole = SessionInstanceRole.Server,
                    Route = SessionMessageRoute.ViaServer,
                    RecipientId = connection.ParticipantId,
                    Body = frame,
                }, Context.RoomId, identityService.SelfParticipant.Id, cts.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            logger.Log($"Туннель: соединение с игрой участника оборвалось: {ex.Message}");
        }
        finally
        {
            await RemoveConnectionAsync(connection.ParticipantId, notifyPeer: true).ConfigureAwait(false);
        }
    }

    private async Task RemoveConnectionAsync(Guid participantId, bool notifyPeer)
    {
        if (!connections.Remove(participantId, out var connection))
        {
            return;
        }

        if (notifyPeer)
        {
            await SendFrameAsync(participantId, TunnelFrameType.Close).ConfigureAwait(false);
        }

        connection.Dispose();
    }

    private async Task SendFrameAsync(Guid participantId, TunnelFrameType type, byte[]? payload = null)
    {
        await messageRouter.RouteAsync(new SessionSpecificMessage()
        {
            SubRoomId = Context.SubRoomId,
            SessionInstanceRole = SessionInstanceRole.Server,
            Route = SessionMessageRoute.ViaServer,
            RecipientId = participantId,
            Body = TunnelProtocol.Encode(type, payload),
        }, Context.RoomId, identityService.SelfParticipant.Id, CancellationToken.None).ConfigureAwait(false);
    }
}
