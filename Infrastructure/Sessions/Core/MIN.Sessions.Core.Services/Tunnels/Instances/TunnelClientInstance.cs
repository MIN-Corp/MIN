using System.Net;
using System.Net.Sockets;
using MIN.Core.Entities.Contracts.Extensions;
using MIN.Core.Entities.Contracts.Models;
using MIN.Core.Events.Contracts.Interfaces;
using MIN.Core.Identity.Contracts.Interfaces;
using MIN.Core.Services.Contracts.Interfaces.Messaging;
using MIN.Helpers.Contracts.Interfaces;
using MIN.Sessions.Core.Events;
using MIN.Sessions.Core.Messaging.Contracts.Enums;
using MIN.Sessions.Core.Messaging.OutOfSubRoom;
using MIN.Sessions.Core.Services.Contracts.Interfaces;
using MIN.Sessions.Core.Services.Contracts.Models;
using MIN.Sessions.Core.Transport.Contracts.Enums;
using MIN.Sessions.Core.Transport.Contracts.Models;

namespace MIN.Sessions.Core.Services.Instances.Tunnels;

/// <summary>
/// Экземпляр туннеля на стороне участника: слушает локальный порт для игрового клиента
/// </summary>
public class TunnelClientInstance : ISessionInstance
{
    private const int HelloTimeoutMs = 15_000;

    private readonly IMessageRouter messageRouter;
    private readonly IEventBus eventBus;
    private readonly IIdentityService identityService;
    private readonly ILoggerProvider logger;

    private readonly CancellationTokenSource cts = new();

    private TcpListener? listener;
    private ParticipantConnection? connection;
    private TaskCompletionSource<bool>? helloTcs;
    private bool joinedAnnounced;
    private bool stopped;

    /// <inheritdoc />
    public SessionInstanceContext Context { get; }

    /// <inheritdoc />
    public event EventHandler? Ended;

    /// <inheritdoc />
    public bool IsJoinAnnounceDeferred => true;

    /// <summary>
    /// Локальный порт, на котором слушает туннель
    /// </summary>
    public int LocalPort { get; private set; }

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="TunnelClientInstance"/>
    /// </summary>
    public TunnelClientInstance(SessionInstanceContext context,
        IMessageRouter messageRouter,
        IEventBus eventBus,
        IIdentityService identityService,
        ILoggerProvider logger)
    {
        Context = context;
        this.messageRouter = messageRouter;
        this.eventBus = eventBus;
        this.identityService = identityService;
        this.logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(Session session, CancellationToken ct)
    {
        listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        LocalPort = ((IPEndPoint)listener.LocalEndpoint).Port;

        _ = AcceptLoopAsync();

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    async Task ISessionInstance.SendDataAsync(byte[] frame, Guid senderId, CancellationToken ct)
    {
        var (type, payload) = TunnelProtocol.Decode(frame);

        switch (type)
        {
            case TunnelFrameType.HelloOk:
                helloTcs?.TrySetResult(true);
                break;

            case TunnelFrameType.HelloFail:
                helloTcs?.TrySetResult(false);
                break;

            case TunnelFrameType.Data:
                if (connection != null)
                {
                    await connection.WriteAsync(payload).ConfigureAwait(false);
                }
                break;

            case TunnelFrameType.Close:
                CloseGameConnection();
                break;
        }
    }

    /// <inheritdoc />
    public void NotifyParticipantJoined(ParticipantInfo participant)
    {
    }

    /// <inheritdoc />
    public void NotifyParticipantLeft(Guid participantId)
    {
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken ct)
    {
        if (stopped)
        {
            return;
        }

        stopped = true;
        cts.Cancel();
        listener?.Stop();
        CloseGameConnection();
        cts.Dispose();
        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

    private async Task AcceptLoopAsync()
    {
        try
        {
            while (!cts.IsCancellationRequested)
            {
                var client = await listener!.AcceptTcpClientAsync(cts.Token).ConfigureAwait(false);

                if (connection != null)
                {
                    client.Dispose();
                    continue;
                }

                var stream = client.GetStream();
                helloTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                await SendSelfFrameAsync(TunnelProtocol.Encode(TunnelFrameType.Hello)).ConfigureAwait(false);
                await eventBus.PublishAsync(new TunnelListenerStartedEvent()
                {
                    RoomId = Context.RoomId,
                    SubRoomId = Context.SubRoomId,
                    Port = LocalPort,
                }).ConfigureAwait(false);

                var helloResult = await WaitForHelloAsync().ConfigureAwait(false);

                if (!helloResult)
                {
                    stream.Dispose();
                    client.Dispose();
                    helloTcs = null;

                    await eventBus.PublishAsync(new TunnelGameConnectFailedEvent()
                    {
                        RoomId = Context.RoomId,
                        SubRoomId = Context.SubRoomId,
                    }).ConfigureAwait(false);
                    continue;
                }

                connection = new ParticipantConnection(client);
                joinedAnnounced = false;
                await AnnounceJoinAsync().ConfigureAwait(false);
                _ = ReadGameLoopAsync(connection);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        catch (Exception ex)
        {
            logger.Log($"Туннель: слушатель остановился с ошибкой: {ex.Message}",
                Helpers.Contracts.Models.Enums.LogLevel.Error);
            Ended?.Invoke(this, EventArgs.Empty);
        }
    }

    private async Task<bool> WaitForHelloAsync()
    {
        try
        {
            var completed = await Task.WhenAny(helloTcs!.Task, Task.Delay(HelloTimeoutMs)).ConfigureAwait(false) == helloTcs.Task;
            return completed && helloTcs.Task.Result;
        }
        catch
        {
            return false;
        }
    }

    private async Task ReadGameLoopAsync(ParticipantConnection gameConnection)
    {
        var buffer = new byte[TunnelProtocol.ChunkSize];

        try
        {
            while (!cts.IsCancellationRequested)
            {
                var read = await gameConnection.Stream.ReadAsync(buffer, cts.Token).ConfigureAwait(false);

                if (read == 0)
                {
                    break;
                }

                var frame = TunnelProtocol.Encode(TunnelFrameType.Data, buffer.AsSpan(0, read));
                await SendSelfFrameAsync(frame).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            logger.Log($"Туннель: игровой клиент отключился: {ex.Message}");
        }
        finally
        {
            await SendSelfFrameAsync(TunnelProtocol.Encode(TunnelFrameType.Close)).ConfigureAwait(false);
            CloseGameConnection();
        }
    }

    private async Task AnnounceJoinAsync()
    {
        if (joinedAnnounced)
        {
            return;
        }

        joinedAnnounced = true;
        var selfParticipant = identityService.SelfParticipant.ToParticipantInfo();

        await messageRouter.RouteAsync(new SessionParticipantJoinedMessage()
        {
            SubRoomId = Context.SubRoomId,
            Participant = selfParticipant,
        }, Context.RoomId, selfParticipant.Id, CancellationToken.None).ConfigureAwait(false);
    }

    private async Task SendSelfFrameAsync(byte[] frame)
    {
        await messageRouter.RouteAsync(new SessionSpecificMessage()
        {
            SubRoomId = Context.SubRoomId,
            SessionInstanceRole = SessionInstanceRole.Client,
            Route = SessionMessageRoute.ViaServer,
            Body = frame,
        }, Context.RoomId, identityService.SelfParticipant.Id, CancellationToken.None).ConfigureAwait(false);
    }

    private void CloseGameConnection()
    {
        connection?.Dispose();
        connection = null;
        helloTcs = null;
    }

    private sealed class ParticipantConnection : IDisposable
    {
        private readonly TcpClient client;
        private readonly SemaphoreSlim writeLock = new(1, 1);

        public NetworkStream Stream { get; }

        public ParticipantConnection(TcpClient client)
        {
            this.client = client;
            Stream = client.GetStream();
        }

        public async Task WriteAsync(byte[] payload)
        {
            await writeLock.WaitAsync().ConfigureAwait(false);
            try
            {
                await Stream.WriteAsync(payload).ConfigureAwait(false);
                await Stream.FlushAsync().ConfigureAwait(false);
            }
            finally
            {
                writeLock.Release();
            }
        }

        public void Dispose()
        {
            writeLock.Dispose();
            client.Dispose();
        }
    }
}
