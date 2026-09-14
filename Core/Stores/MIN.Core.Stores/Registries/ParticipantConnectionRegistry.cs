using System.Collections.Concurrent;
using MIN.Core.Entities.Contracts.Models;
using MIN.Core.Stores.Contracts.Exceptions;
using MIN.Core.Stores.Contracts.Registries.Interfaces;
using MIN.Core.Stores.Contracts.Registries.Models;

namespace MIN.Core.Stores.Registries;

/// <inheritdoc cref="IParticipantConnectionRegistry"/>
public sealed class ParticipantConnectionRegistry : IParticipantConnectionRegistry
{
    private readonly ConcurrentDictionary<Guid, ParticipantInfo> participantByConnectionId = new();
    private readonly ConcurrentDictionary<Guid, Guid> connectionIdByParticipantId = new();

    void IParticipantConnectionRegistry.Register(Guid connectionId, ParticipantInfo participant)
    {
        participantByConnectionId[connectionId] = participant;
        connectionIdByParticipantId[participant.Id] = connectionId;
    }

    bool IParticipantConnectionRegistry.ConnectionExists(Guid connectionId)
        => participantByConnectionId.ContainsKey(connectionId);

    void IParticipantConnectionRegistry.RegisterLocalParticipant(ParticipantInfo participant)
    {
        participantByConnectionId[CoreRegistryConstants.LocalConnectionId] = participant;
        connectionIdByParticipantId[participant.Id] = CoreRegistryConstants.LocalConnectionId;
    }

    void IParticipantConnectionRegistry.Unregister(Guid connectionId)
    {
        if (participantByConnectionId.TryRemove(connectionId, out var participantInfo)
           && connectionIdByParticipantId.TryGetValue(participantInfo.Id, out var currentConnectionId)
           && currentConnectionId == connectionId)
        {
            connectionIdByParticipantId.TryRemove(participantInfo.Id, out _);
        }
    }

    ParticipantInfo IParticipantConnectionRegistry.GetParticipant(Guid connectionId)
        => participantByConnectionId.TryGetValue(connectionId, out var p) ? p : throw new ConnectionNotRegistredException(connectionId);

    bool IParticipantConnectionRegistry.TryGetParticipantFromConnectionId(Guid connectionId, out ParticipantInfo participant)
        => participantByConnectionId.TryGetValue(connectionId, out participant!);

    Guid IParticipantConnectionRegistry.GetParticipantIdFromConnectionId(Guid connectionId)
        => participantByConnectionId.TryGetValue(connectionId, out var p) ? p.Id : throw new ConnectionNotRegistredException(connectionId);

    Guid IParticipantConnectionRegistry.GetConnectionIdFromParticipantId(Guid participantId)
        => connectionIdByParticipantId.TryGetValue(participantId, out var connectionId) ? connectionId : throw new ParticipantNotRegistredException(connectionId);

    bool IParticipantConnectionRegistry.TryGetConnectionIdFromParticipantId(Guid participantId, out Guid connectionId)
        => connectionIdByParticipantId.TryGetValue(participantId, out connectionId);
}
