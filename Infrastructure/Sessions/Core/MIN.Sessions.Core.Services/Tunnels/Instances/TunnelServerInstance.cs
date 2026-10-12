using System.Diagnostics;
using MIN.Core.Entities.Contracts.Models;
using MIN.Core.Events.Contracts.Interfaces;
using MIN.Core.Identity.Contracts.Interfaces;
using MIN.Core.Services.Contracts.Interfaces.Messaging;
using MIN.Core.Stores.Contracts.Interfaces;
using MIN.Helpers.Contracts.Interfaces;
using MIN.Helpers.Contracts.Models.Enums;
using MIN.Sessions.Core.Events;
using MIN.Sessions.Core.Messaging.Ipc;
using MIN.Sessions.Core.Messaging.OutOfSubRoom;
using MIN.Sessions.Core.Services.Contracts.Interfaces;
using MIN.Sessions.Core.Services.Contracts.Models;
using MIN.Sessions.Core.Transport.Contracts.Enums;
using MIN.Sessions.Core.Transport.Contracts.Interfaces;
using MIN.Sessions.Core.Transport.Contracts.Models;

namespace MIN.Sessions.Core.Services.Tunnels.Instances;

/// <summary>
/// Экземпляр сессии, работающий во внешнем процессе (server.exe / client.exe)
/// </summary>
public class TunnelServerInstance : ISessionInstance
{
    private const int ProcessWaitingTimeOutMs = 30_000;

    private readonly ISessionProcessBridge processBridge;
    private readonly ISessionTransportFactory transportFactory;
    private readonly IMessageRouter messageRouter;
    private readonly IEventBus eventBus;
    private readonly IRoomFactory roomFactory;
    private readonly IIdentityService identityService;
    private readonly ILoggerProvider logger;

    private Process? process;
    private ISessionProcessTransport? transport;
    private EventHandler? exitHandler;
    private Session? session;
    private bool started;
    private bool stopped;

    /// <inheritdoc />
    public SessionInstanceContext Context { get; }

    /// <inheritdoc />
    public event EventHandler? Ended;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="TunnelServerInstance"/>
    /// </summary>
    public TunnelServerInstance(SessionInstanceContext context,
        ISessionProcessBridge processBridge,
        ISessionTransportFactory transportFactory,
        IMessageRouter messageRouter,
        IEventBus eventBus,
        IRoomFactory roomFactory,
        IIdentityService identityService,
        ILoggerProvider logger)
    {
        Context = context;
        this.processBridge = processBridge;
        this.transportFactory = transportFactory;
        this.messageRouter = messageRouter;
        this.eventBus = eventBus;
        this.roomFactory = roomFactory;
        this.identityService = identityService;
        this.logger = logger;
    }

    /// <inheritdoc />
    public async Task StartAsync(Session session, CancellationToken ct)
    {
        this.session = session;

        var fullPath = Context.Role == SessionInstanceRole.Client
            ? session.GetClientPath()
            : session.GetServerPath();

        logger.Log($"Стартую {session.Name} как {Context.Role}");

        if (Context.Role == SessionInstanceRole.Client)
        {
            await eventBus.PublishAsync(new SessionProcessStartedEvent()
            {
                RoomId = Context.RoomId,
                SubRoomId = Context.SubRoomId,
                Session = session,
            }, ct).ConfigureAwait(false);
        }

        if (!Path.Exists(fullPath))
        {
            throw new FileNotFoundException($"Не найден исполняемый файл сессии: {fullPath}");
        }

        transport = transportFactory.Create();
        processBridge.RegisterTransport(Context, transport);

        await transport.StartAsync(Context.RoomId, ct).ConfigureAwait(false);

        var psi = new ProcessStartInfo
        {
            FileName = fullPath,
            UseShellExecute = false,
        };
        psi.ArgumentList.Add(transport.GetConnectionString());

        if (!OperatingSystem.IsWindows())
        {
            var mode = File.GetUnixFileMode(fullPath);
            if ((mode & UnixFileMode.UserExecute) == 0)
            {
                psi.FileName = "dotnet";
                psi.ArgumentList.Insert(0, fullPath);
            }
        }

        process = Process.Start(psi);

        if (process == null || process.HasExited)
        {
            await CleanupTransportAsync().ConfigureAwait(false);
            throw new InvalidOperationException($"Не удалось запустить процесс сессии {session.Name}");
        }

        await transport.WaitForConnectionAsync(Context, ProcessWaitingTimeOutMs, ct).ConfigureAwait(false);

        var readySuccess = await processBridge.WaitForReadyMessage(Context, ProcessWaitingTimeOutMs, ct).ConfigureAwait(false);

        if (!readySuccess)
        {
            await CleanupTransportAsync().ConfigureAwait(false);
            process.Kill();
            throw new TimeoutException($"Сессия {session.Name} не прислала сообщение о готовности");
        }

        started = true;
        process.EnableRaisingEvents = true;
        exitHandler = async (_, _) => await OnProcessExitedAsync().ConfigureAwait(false);
        process.Exited += exitHandler;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken ct)
    {
        if (stopped)
        {
            return;
        }

        stopped = true;

        if (exitHandler != null && process != null)
        {
            process.Exited -= exitHandler;
        }

        await eventBus.PublishAsync(new SessionProcessEndedEvent()
        {
            RoomId = Context.RoomId,
            SubRoomId = Context.SubRoomId,
        }).ConfigureAwait(false);

        try
        {
            if (started)
            {
                await processBridge.SendCloseMessage(Context).ConfigureAwait(false);
            }

            if (process != null)
            {
                if (!started)
                {
                    process.Kill();
                    await CleanupTransportAsync().ConfigureAwait(false);
                    return;
                }

                var exitTask = process.WaitForExitAsync(CancellationToken.None);
                var exited = await Task.WhenAny(exitTask, Task.Delay(ProcessWaitingTimeOutMs)).ConfigureAwait(false) == exitTask;

                if (!exited)
                {
                    process.Kill();
                    await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
                }
            }
        }
        catch (Exception ex)
        {
            logger.Log($"Произошла ошибка при закрытии сессии {ex.Message}", LogLevel.Error);
        }

        await CleanupTransportAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void NotifyParticipantJoined(ParticipantInfo participant)
    {
        if (!started)
        {
            return;
        }

        _ = processBridge.SendIpcMessage(
            new ParticipantConnectedMessage(participant.Id.ToString(), participant.Name),
            Context, participant.Id, CancellationToken.None);
    }

    /// <inheritdoc />
    public void NotifyParticipantLeft(Guid participantId)
    {
        if (!started)
        {
            return;
        }

        _ = processBridge.SendIpcMessage(
            new ParticipantDisconnectedMessage(participantId.ToString()),
            Context, participantId, CancellationToken.None);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None).ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    private async Task OnProcessExitedAsync()
    {
        try
        {
            if (session == null)
            {
                return;
            }

            if (Context.Role == SessionInstanceRole.Server)
            {
                var roomContext = roomFactory.GetOrCreateContext(Context.RoomId);

                if (roomContext.SubRooms.GetParticipantCount(Context.SubRoomId) == 0)
                {
                    return;
                }

                await messageRouter.RouteAsync(new SessionServerShutdownMessage()
                {
                    SubRoomId = Context.SubRoomId,
                    Reason = $"Сервер сессии {session.Name} был закрыт хостом",
                }, Context.RoomId, identityService.SelfParticipant.Id, CancellationToken.None).ConfigureAwait(false);
            }
            else
            {
                await eventBus.PublishAsync(new SessionProcessEndedEvent()
                {
                    RoomId = Context.RoomId,
                    SubRoomId = Context.SubRoomId,
                }).ConfigureAwait(false);

                await messageRouter.RouteAsync(new SessionLeaveMessage()
                {
                    SubRoomId = Context.SubRoomId,
                }, Context.RoomId, identityService.SelfParticipant.Id, CancellationToken.None).ConfigureAwait(false);
            }
        }
        finally
        {
            await CleanupTransportAsync().ConfigureAwait(false);
            Ended?.Invoke(this, EventArgs.Empty);
        }
    }

    private async Task CleanupTransportAsync()
    {
        if (transport != null)
        {
            transportFactory.Destroy(transport);
            processBridge.UnregisterTransport(Context);
            transport = null;
        }
        await Task.CompletedTask;
    }
}
