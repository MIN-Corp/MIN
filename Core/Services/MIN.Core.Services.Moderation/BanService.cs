using MIN.Core.Entities.Contracts.Models;
using MIN.Core.Services.Contracts.Interfaces.Moderation;
using MIN.Core.Services.Contracts.Interfaces.Persistence;
using MIN.Core.Stores.Contracts.Interfaces;

namespace MIN.Core.Services.Moderation;

/// <inheritdoc cref="IBanService"/>
public class BanService : IBanService
{
    private readonly IRoomStore roomStore;
    private readonly IRoomPersistenceService persistenceService;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="BanService"/>
    /// </summary>
    public BanService(IRoomStore roomStore, IRoomPersistenceService persistenceService)
    {
        this.roomStore = roomStore;
        this.persistenceService = persistenceService;
    }

    BanInfo IBanService.Ban(Guid roomId, Guid participantId, string name, string reason, DateTimeOffset? bannedUntil, string? ipAddress, bool isIpBanned)
    {
        var room = roomStore.GetRoom(roomId);

        var ban = new BanInfo(participantId, name, reason, bannedUntil, ipAddress, isIpBanned);
        room.LocalRoomSettings.BannedParticipants[participantId] = ban;

        persistenceService.MarkDirty(roomId);
        return ban;
    }

    bool IBanService.Unban(Guid roomId, Guid participantId)
    {
        var room = roomStore.GetRoom(roomId);

        if (!room.LocalRoomSettings.BannedParticipants.Remove(participantId))
        {
            return false;
        }

        persistenceService.MarkDirty(roomId);
        return true;
    }

    BanInfo? IBanService.UpdateBan(Guid roomId, Guid participantId, string reason, DateTimeOffset? bannedUntil, bool alsoBanIp)
    {
        var room = roomStore.GetRoom(roomId);

        if (!room.LocalRoomSettings.BannedParticipants.TryGetValue(participantId, out var existing))
        {
            return null;
        }

        room.LocalRoomSettings.BannedParticipants[participantId] = existing with
        {
            Reason = reason,
            BannedUntil = bannedUntil,
            IsIpBanned = alsoBanIp
        };

        persistenceService.MarkDirty(roomId);
        return room.LocalRoomSettings.BannedParticipants[participantId];
    }

    bool IBanService.TryGetActiveBan(Guid roomId, Guid participantId, out BanInfo ban)
    {
        ban = null!;

        if (!roomStore.TryGetRoom(roomId, out var room)
            || !room.LocalRoomSettings.BannedParticipants.TryGetValue(participantId, out var stored))
        {
            return false;
        }

        if (IsExpired(stored))
        {
            room.LocalRoomSettings.BannedParticipants.Remove(participantId);
            persistenceService.MarkDirty(roomId);
            return false;
        }

        ban = stored;
        return true;
    }

    IReadOnlyList<BanInfo> IBanService.GetBannedParticipants(Guid roomId)
    {
        var room = roomStore.GetRoom(roomId);
        var bans = room.LocalRoomSettings.BannedParticipants;

        var expired = bans.Values.Where(IsExpired).ToList();

        if (expired.Count <= 0)
        {
            return bans.Values.OrderBy(x => x.Name).ToList();
        }

        foreach (var ban in expired)
        {
            bans.Remove(ban.ParticipantId);
        }

        persistenceService.MarkDirty(roomId);

        return bans.Values.OrderBy(x => x.Name).ToList();
    }

    bool IBanService.TryGetActiveBanByIp(Guid roomId, string ip, out BanInfo ban)
    {
        ban = null!;

        if (!roomStore.TryGetRoom(roomId, out var room))
        {
            return false;
        }

        var stored = room.LocalRoomSettings.BannedParticipants.Values
            .FirstOrDefault(x => x.IsIpBanned && x.IpAddress == ip);
        if (stored == null)
        {
            return false;
        }

        if (IsExpired(stored))
        {
            room.LocalRoomSettings.BannedParticipants.Remove(stored.ParticipantId);
            persistenceService.MarkDirty(roomId);
            return false;
        }

        ban = stored;
        return true;
    }

    private static bool IsExpired(BanInfo ban)
        => ban.BannedUntil is { } until && until <= DateTimeOffset.Now;
}
