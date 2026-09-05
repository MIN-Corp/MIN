using MIN.Core.Entities;
using MIN.Core.Stores.Contracts.Interfaces;

namespace MIN.Core.Stores.Services;

/// <inheritdoc cref="IParticipantStore"/>
public sealed class ParticipantStore : IParticipantStore
{
    private List<Participant> participants = [];

    void IParticipantStore.Bind(List<Participant> participants)
    {
        lock (this.participants)
        {
            this.participants = participants;
        }
    }

    void IParticipantStore.AddParticipant(Participant participant)
    {
        lock (participants)
        {
            if (!participants.Any(p => p.Id == participant.Id))
            {
                participants.Add(participant);
            }
        }
    }

    void IParticipantStore.UpdateParticipant(Guid id, Participant participant)
    {
        lock (participants)
        {
            var index = participants.FindIndex(p => p.Id == id);
            if (index >= 0)
            {
                participants[index] = participant;
            }
        }
    }

    void IParticipantStore.RemoveParticipant(Guid participantId)
    {
        lock (participants)
        {
            var existing = participants.FirstOrDefault(p => p.Id == participantId);
            if (existing != null)
            {
                participants.Remove(existing);
            }
        }
    }

    Participant IParticipantStore.GetParticipantById(Guid participantId)
    {
        lock (participants)
        {
            return participants.FirstOrDefault(x => x.Id == participantId)
                ?? throw new ArgumentNullException(nameof(participantId));
        }
    }

    IEnumerable<Participant> IParticipantStore.GetParticipantByIds(IEnumerable<Guid> participantIds)
    {
        lock (participants)
        {
            return participants.Where(x => participantIds.Contains(x.Id));
        }
    }

    bool IParticipantStore.TryGetParticipantById(Guid participantId, out Participant? participant)
    {
        lock (participants)
        {
            participant = participants.FirstOrDefault(x => x.Id == participantId);
            return participant != null;
        }
    }

    IEnumerable<Participant> IParticipantStore.GetParticipants()
    {
        lock (participants)
        {
            return participants.ToList();
        }
    }

    void IParticipantStore.ClearParticipants()
    {
        lock (participants)
        {
            participants.Clear();
        }
    }
}
