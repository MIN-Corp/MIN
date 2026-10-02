using MIN.Core.Entities.Contracts.Models;
using MIN.Core.Stores.Contracts.Enums;
using MIN.Core.Stores.Contracts.Interfaces;
using MIN.Core.Stores.Contracts.Models.Persistence;
using MIN.Core.Stores.Contracts.Models.SubRooms;

namespace MIN.Core.Stores.Services;

/// <inheritdoc cref="ISubRoomManager"/>
public class SubRoomManager : ISubRoomManager
{
    private SubRoomState state = new();

    SubRoomInfo ISubRoomManager.HostSubRoom(ParticipantInfo creator, SubRoomPurpose purpose, int? maximum)
    {
        SubRoomInfo subRoom;
        lock (state)
        {
            subRoom = new SubRoomInfo
            {
                Id = state.NextId++,
                Purpose = purpose,
                IsActive = true,
                CreatorId = creator.Id,
                Participants = [creator],
                MaximumParticipants = maximum,
                CreatedAt = DateTime.Now
            };
            state.SubRooms[subRoom.Id] = subRoom;
        }

        return subRoom;
    }

    bool ISubRoomManager.ActivateSubRoom(int subRoomId, ParticipantInfo participant)
    {
        lock (state)
        {
            if (!state.SubRooms.TryGetValue(subRoomId, out var subRoom))
            {
                return false;
            }

            if (subRoom.IsActive)
            {
                return false;
            }

            if (subRoom.Participants.Any(p => p.Id == participant.Id))
            {
                return false;
            }

            subRoom.IsActive = true;
            return true;
        }
    }

    SubRoomJoinOutcome ISubRoomManager.TryJoinSubRoom(int subRoomId, ParticipantInfo participant)
    {
        lock (state)
        {
            if (!state.SubRooms.TryGetValue(subRoomId, out var subRoom))
            {
                return SubRoomJoinOutcome.SubRoomNotFound;
            }

            if (subRoom.Participants.Any(p => p.Id == participant.Id))
            {
                return SubRoomJoinOutcome.AlreadyJoined;
            }

            if (subRoom.Participants.Count >= subRoom.MaximumParticipants)
            {
                return SubRoomJoinOutcome.MaximumParticipants;
            }

            subRoom.Participants.Add(participant);
            return SubRoomJoinOutcome.Success;
        }
    }

    bool ISubRoomManager.IsInSubRoom(int subRoomId, Guid participantId)
    {
        lock (state)
        {
            if (!state.SubRooms.TryGetValue(subRoomId, out var subRoom))
            {
                return false;
            }

            return subRoom.Participants.Any(p => p.Id == participantId);
        }
    }

    bool ISubRoomManager.LeaveSubRoom(int subRoomId, Guid participantId)
    {
        lock (state)
        {
            if (!state.SubRooms.TryGetValue(subRoomId, out var subRoom))
            {
                return false;
            }

            subRoom.Participants.RemoveAll(p => p.Id == participantId);

            if (subRoom.Participants.Count == 0)
            {
                subRoom.IsActive = false;
            }

            return subRoom.IsActive;
        }
    }

    bool ISubRoomManager.TryStopSubRoom(int subRoomId, Guid requesterId)
    {
        lock (state)
        {
            if (!state.SubRooms.TryGetValue(subRoomId, out var subRoom))
            {
                return false;
            }

            if (subRoom.CreatorId != requesterId)
            {
                return false;
            }

            subRoom.Participants.Clear();
            subRoom.IsActive = false;
            return true;
        }
    }

    IReadOnlyList<Guid> ISubRoomManager.GetParticipantIds(int subRoomId)
    {
        lock (state)
        {
            if (!state.SubRooms.TryGetValue(subRoomId, out var subRoom))
            {
                return [];
            }

            return subRoom.Participants.Select(x => x.Id).ToList().AsReadOnly();
        }
    }

    int ISubRoomManager.GetParticipantCount(int subRoomId)
    {
        lock (state)
        {
            if (!state.SubRooms.TryGetValue(subRoomId, out var subRoom))
            {
                return 0;
            }

            return subRoom.Participants.Count;
        }
    }

    SubRoomInfo? ISubRoomManager.GetSubRoom(int subRoomId)
    {
        lock (state)
        {
            if (!state.SubRooms.TryGetValue(subRoomId, out var subRoom))
            {
                return null;
            }

            return subRoom with { Participants = subRoom.Participants.ToList() };
        }
    }

    IReadOnlyList<SubRoomInfo> ISubRoomManager.GetRoomSubRooms()
    {
        lock (state)
        {
            return state.SubRooms.Values
                .Select(sr => sr with { Participants = sr.Participants.ToList() })
                .ToList()
                .AsReadOnly();
        }
    }

    void ISubRoomManager.ClearRoomSubRooms()
    {
        lock (state)
        {
            state.SubRooms.Clear();
        }
    }

    SubRoomsSnapshot ISubRoomManager.CreateSnapshot()
    {
        lock (state)
        {
            return new SubRoomsSnapshot
            {
                SubRooms = state.SubRooms.Values
                    .Select(sr => sr with { Participants = sr.Participants.ToList() })
                    .ToList(),
                NextId = state.NextId,
            };
        }
    }

    void ISubRoomManager.RestoreState(SubRoomsSnapshot subRoomsSnapshot)
    {
        lock (state)
        {
            state.SubRooms = subRoomsSnapshot.SubRooms.ToDictionary(x => x.Id);
            state.NextId = subRoomsSnapshot.NextId;
        }
    }
}
