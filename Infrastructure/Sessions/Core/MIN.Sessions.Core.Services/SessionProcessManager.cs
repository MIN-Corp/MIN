using System.Diagnostics;
using MIN.Core.Events.Contracts.Interfaces;
using MIN.Core.Identity.Contracts.Interfaces;
using MIN.Core.Services.Contracts.Interfaces.Messaging;
using MIN.Core.SubRooms.Contracts.Interfaces;
using MIN.Helpers.Contracts.Interfaces;
using MIN.Sessions.Core.Events;
using MIN.Sessions.Core.Messaging.OutOfSubRoom;
using MIN.Sessions.Core.Services.Contracts.Interfaces;
using MIN.Sessions.Core.Services.Contracts.Models;
using MIN.Sessions.Core.Transport.Contracts.Enums;
using MIN.Sessions.Core.Transport.Contracts.Interfaces;
using MIN.Sessions.Core.Transport.Contracts.Models;

namespace MIN.Sessions.Core.Services;

/// <inheritdoc cref="ISessionProcessManager"/>
public class SessionProcessManager : ISessionProcessManager
{
    private const int ProcessWaitingTimeOutMs = 30_000;

    private readonly Dictionary<ProcessContext, Process> pendingProcesses = [];
    private readonly Dictionary<ProcessContext, EventHandler> currentExitHandlers = [];
    private readonly Dictionary<ProcessContext, Process> runningProcesses = [];
    private readonly Dictionary<ProcessContext, ISessionProcessTransport> transports = [];
    private readonly IMessageRouter messageRouter;
    private readonly IEventBus eventBus;
    private readonly ISessionProcessBridge processBridge;
    private readonly ISessionTransportFactory transportFactory;
    private readonly ISubRoomManager subRoomManager;
    private readonly IIdentityService identityService;
    private readonly ILoggerProvider logger;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="SessionProcessManager"/>
    /// </summary>
    public SessionProcessManager(IMessageRouter messageRouter,
        IEventBus eventBus,
        ISessionProcessBridge processBridge,
        ISessionTransportFactory transportFactory,
        ISubRoomManager subRoomManager,
        IIdentityService identityService,
        ILoggerProvider logger)
    {
        this.messageRouter = messageRouter;
        this.eventBus = eventBus;
        this.processBridge = processBridge;
        this.transportFactory = transportFactory;
        this.subRoomManager = subRoomManager;
        this.identityService = identityService;
        this.logger = logger;
    }

    async Task<bool> ISessionProcessManager.StartAsync(Session session, ProcessContext context, CancellationToken cancellationToken)
    {
        var fullPath = context.Role == SessionProcessRole.Client
            ? session.GetClientPath()
        : session.GetServerPath();

        logger.Log($"Стартую {session.Name} как {context.Role}");

        if (context.Role == SessionProcessRole.Client)
        {
            await eventBus.PublishAsync(new SessionProcessStartedEvent()
            {
                RoomId = context.RoomId,
                SubRoomId = context.SubRoomId,
                Session = session,
            }, cancellationToken);
        }

        if (!Path.Exists(fullPath))
        {
            return false;
        }

        var processTransport = transportFactory.Create();
        transports[context] = processTransport;
        processBridge.RegisterTransport(context, processTransport);

        await processTransport.StartAsync(context.RoomId, cancellationToken);
        var connectionString = processTransport.GetConnectionString();

        var psi = new ProcessStartInfo
        {
            FileName = fullPath,
            UseShellExecute = false,
        };
        psi.ArgumentList.Add(connectionString);

        if (!OperatingSystem.IsWindows())
        {
            var mode = File.GetUnixFileMode(fullPath);
            if ((mode & UnixFileMode.UserExecute) == 0)
            {
                psi.FileName = "dotnet";
                psi.ArgumentList.Insert(0, fullPath);
            }
        }

        var startedProcess = Process.Start(psi);

        if (startedProcess == null || startedProcess.HasExited)
        {
            processBridge.UnregisterTransport(context);
            return false;
        }

        pendingProcesses[context] = startedProcess;

        var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        await processTransport.WaitForConnectionAsync(context, ProcessWaitingTimeOutMs, connectCts.Token);
        var readySuccess = await processBridge.WaitForReadyMessage(context, ProcessWaitingTimeOutMs, connectCts.Token);
        pendingProcesses.Remove(context);
        if (readySuccess == false)
        {
            processBridge.UnregisterTransport(context);
            startedProcess.Kill();
            return false;
        }

        pendingProcesses.Remove(context);
        runningProcesses[context] = startedProcess;

        startedProcess.EnableRaisingEvents = true;
        currentExitHandlers[context] = async (_, _) => await AnnounceExit(session, context, cancellationToken);
        startedProcess.Exited += currentExitHandlers[context];

        return true;
    }

    private async Task AnnounceExit(Session session, ProcessContext context, CancellationToken cancellationToken)
    {
        if (context.Role == SessionProcessRole.Server)
        {
            if (subRoomManager.GetParticipantCount(context.RoomId, context.SubRoomId) == 0)
            {
                return;
            }

            await messageRouter.RouteAsync(new SessionServerShutdownMessage()
            {
                SubRoomId = context.SubRoomId,
                Reason = $"Сервер сессии {session.Name} был закрыт хостом"
            }, context.RoomId, identityService.SelfParticipant.Id, cancellationToken);
        }
        else
        {
            await eventBus.PublishAsync(new SessionProcessEndedEvent()
            {
                RoomId = context.RoomId,
                SubRoomId = context.SubRoomId,
            }, cancellationToken);

            await messageRouter.RouteAsync(new SessionLeaveMessage()
            {
                SubRoomId = context.SubRoomId,
            }, context.RoomId, identityService.SelfParticipant.Id, cancellationToken);
        }
        runningProcesses.Remove(context);
    }

    bool ISessionProcessManager.SessionClientAppExists(Session session)
        => Path.Exists(session.ClientExecutableFileName);

    async Task ISessionProcessManager.StopAsync(ProcessContext context)
    {
        if (runningProcesses.TryGetValue(context, out var process))
        {
            try
            {
                await StopProcessWithTimeOut(context, process, clearAnnounce: false);
            }
            catch (Exception ex)
            {
                logger.Log($"Произошла ошибка при закрытии сессии {ex.Message}",
                    Helpers.Contracts.Models.Enums.LogLevel.Error);
            }
        }
        runningProcesses.Remove(context);
    }

    async Task ISessionProcessManager.StopForRoomAsync(Guid roomId)
    {
        var roomPendingProcesses = pendingProcesses.Keys.Where(x => x.RoomId == roomId).ToList();
        foreach (var context in roomPendingProcesses)
        {
            try
            {
                await StopProcessWithTimeOut(context, pendingProcesses[context]);
            }
            catch
            {
                continue;
            }
            pendingProcesses.Remove(context);
        }

        var roomRunningProcesses = runningProcesses.Keys.Where(x => x.RoomId == roomId).ToList();
        foreach (var context in roomRunningProcesses)
        {
            try
            {
                await StopProcessWithTimeOut(context, runningProcesses[context]);
                runningProcesses.Remove(context);
            }
            catch
            {
                continue;
            }
        }
    }

    async Task ISessionProcessManager.StopAllAsync()
    {
        foreach (var process in pendingProcesses)
        {
            try
            {
                await StopProcessWithTimeOut(process.Key, process.Value);
            }
            catch
            {
                continue;
            }
        }
        pendingProcesses.Clear();

        foreach (var process in runningProcesses)
        {
            try
            {
                await StopProcessWithTimeOut(process.Key, process.Value);
            }
            catch
            {
                continue;
            }
        }
        runningProcesses.Clear();
    }

    private async Task StopProcessWithTimeOut(ProcessContext context, Process process, bool clearAnnounce = true)
    {
        if (currentExitHandlers[context] != null && clearAnnounce)
        {
            process.Exited -= currentExitHandlers[context];
        }
        else if (!clearAnnounce)
        {
            await eventBus.PublishAsync(new SessionProcessEndedEvent()
            {
                RoomId = context.RoomId,
                SubRoomId = context.SubRoomId,
            });
        }

        await processBridge.SendCloseMessage(context);

        var exitTask = process.WaitForExitAsync(CancellationToken.None);

        var exited = await Task.WhenAny(exitTask, Task.Delay(ProcessWaitingTimeOutMs)) == exitTask;

        if (!exited)
        {
            process.Kill();
            await process.WaitForExitAsync(CancellationToken.None);
        }

        transportFactory.Destroy(transports[context]);
        processBridge.UnregisterTransport(context);
    }
}
