using MIN.ScreenDemo.Services.Contracts.Interfaces;
using MIN.ScreenDemo.Services.Contracts.Models;

namespace MIN.ScreenDemo.Services;

/// <inheritdoc cref="IScreenDemoStateService"/>
public class ScreenDemoStateService : IScreenDemoStateService
{
    private sealed class DemoEntry
    {
        public required ScreenDemoSessionInfo Info { get; init; }

        public HashSet<Guid> Viewers { get; } = [];
    }

    private readonly object gate = new();
    private readonly Dictionary<(Guid RoomId, int SubRoomId), DemoEntry> demos = [];

    IReadOnlyList<ScreenDemoSessionInfo> IScreenDemoStateService.GetActiveDemos(Guid roomId)
    {
        lock (gate)
        {
            return demos.Values
                .Where(x => x.Info.RoomId == roomId)
                .Select(ToSessionInfo)
                .ToList();
        }
    }

    ScreenDemoSessionInfo? IScreenDemoStateService.GetDemo(Guid roomId, int subRoomId)
    {
        lock (gate)
        {
            return demos.TryGetValue((roomId, subRoomId), out var entry) ? ToSessionInfo(entry) : null;
        }
    }

    ScreenDemoSessionInfo? IScreenDemoStateService.GetOwnDemo(Guid roomId, Guid selfParticipantId)
    {
        lock (gate)
        {
            return demos.Values
                .Where(x => x.Info.RoomId == roomId && x.Info.Sharer.Id == selfParticipantId)
                .Select(ToSessionInfo)
                .FirstOrDefault();
        }
    }

    void IScreenDemoStateService.AddDemo(ScreenDemoSessionInfo demo)
    {
        lock (gate)
        {
            demos[(demo.RoomId, demo.SubRoomId)] = new DemoEntry { Info = demo };
        }
    }

    void IScreenDemoStateService.AddViewer(Guid roomId, int subRoomId, Guid viewerId)
    {
        lock (gate)
        {
            if (demos.TryGetValue((roomId, subRoomId), out var entry))
            {
                entry.Viewers.Add(viewerId);
            }
        }
    }

    void IScreenDemoStateService.RemoveViewer(Guid roomId, int subRoomId, Guid viewerId)
    {
        lock (gate)
        {
            if (demos.TryGetValue((roomId, subRoomId), out var entry))
            {
                entry.Viewers.Remove(viewerId);
            }
        }
    }

    bool IScreenDemoStateService.IsWatching(Guid roomId, int subRoomId, Guid participantId)
    {
        lock (gate)
        {
            return demos.TryGetValue((roomId, subRoomId), out var entry)
                && entry.Viewers.Contains(participantId);
        }
    }

    void IScreenDemoStateService.RemoveDemo(Guid roomId, int subRoomId)
    {
        lock (gate)
        {
            demos.Remove((roomId, subRoomId));
        }
    }

    void IScreenDemoStateService.Clear(Guid? roomId)
    {
        lock (gate)
        {
            if (roomId == null)
            {
                demos.Clear();
                return;
            }

            var keys = demos.Keys.Where(x => x.RoomId == roomId).ToList();

            foreach (var key in keys)
            {
                demos.Remove(key);
            }
        }
    }

    private static ScreenDemoSessionInfo ToSessionInfo(DemoEntry entry)
        => entry.Info with { ViewerIds = entry.Viewers.ToList() };
}
