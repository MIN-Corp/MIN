using MIN.Helpers.Contracts.Interfaces;
using MIN.Helpers.Contracts.Models.Enums;
using MIN.Sessions.Core.Services.Contracts.Enums;
using MIN.Sessions.Core.Services.Contracts.Interfaces;
using MIN.Sessions.Core.Services.Contracts.Models;
using MIN.Sessions.Core.Transport.Contracts.Models;

namespace MIN.Sessions.Core.Services.Services;

/// <summary>
/// Менеджер экземпляров сессий
/// </summary>
public class SessionInstanceManager : ISessionInstanceManager
{
    private readonly Dictionary<SessionInstanceContext, ISessionInstance> instances = [];
    private readonly IReadOnlyList<ISessionInstanceFactory> factories;
    private readonly ILoggerProvider logger;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="SessionInstanceManager"/>
    /// </summary>
    public SessionInstanceManager(IEnumerable<ISessionInstanceFactory> factories,
        ILoggerProvider logger)
    {
        this.factories = factories.ToList();
        this.logger = logger;
    }

    async Task<ISessionInstance?> ISessionInstanceManager.StartAsync(Session session, SessionInstanceContext context, CancellationToken ct)
    {
        if (instances.ContainsKey(context))
        {
            logger.Log($"Экземпляр сессии {session.Name} для {context.RoomId}/{context.SubRoomId}/{context.Role} уже запущен", LogLevel.Warning);
            return null;
        }

        var factory = factories.FirstOrDefault(x => x.CanCreate(session));

        if (factory == null)
        {
            logger.Log($"Не найдена фабрика для сессии {session.Name} (вид {session.Kind})", LogLevel.Error);
            return null;
        }

        var instance = factory.Create(context);
        instances[context] = instance;
        instance.Ended += async (_, _) =>
        {
            instances.Remove(context);
            await instance.DisposeAsync().ConfigureAwait(false);
        };

        try
        {
            await instance.StartAsync(session, ct).ConfigureAwait(false);
            return instance;
        }
        catch (Exception ex)
        {
            logger.Log($"Не удалось запустить экземпляр сессии {session.Name}: {ex.Message}", LogLevel.Error);
            instances.Remove(context);
            await instance.StopAsync(CancellationToken.None).ConfigureAwait(false);
            await instance.DisposeAsync().ConfigureAwait(false);
            return null;
        }
    }

    async Task ISessionInstanceManager.StopAsync(SessionInstanceContext context)
    {
        if (!instances.Remove(context, out var instance))
        {
            return;
        }

        await instance.StopAsync(CancellationToken.None).ConfigureAwait(false);
        await instance.DisposeAsync().ConfigureAwait(false);
    }

    async Task ISessionInstanceManager.StopForRoomAsync(Guid roomId)
    {
        foreach (var context in instances.Keys.Where(x => x.RoomId == roomId).ToList())
        {
            await ((ISessionInstanceManager)this).StopAsync(context).ConfigureAwait(false);
        }
    }

    async Task ISessionInstanceManager.StopAllAsync()
    {
        foreach (var context in instances.Keys.ToList())
        {
            await ((ISessionInstanceManager)this).StopAsync(context).ConfigureAwait(false);
        }
    }

    bool ISessionInstanceManager.SessionClientAppExists(Session session)
        => session.Kind == SessionKind.PortTunnel || Path.Exists(session.GetClientPath());

    IEnumerable<ISessionInstance> ISessionInstanceManager.GetInstances(Guid roomId, int subRoomId)
        => instances.Keys
            .Where(x => x.RoomId == roomId && x.SubRoomId == subRoomId)
            .Select(x => instances[x])
            .ToList();
}
