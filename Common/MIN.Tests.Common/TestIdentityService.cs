using MIN.Core.Entities;
using MIN.Core.Entities.Contracts.Interfaces;
using MIN.Core.Identity.Contracts.Interfaces;

namespace MIN.Tests.Common;

/// <summary>
/// Тестовый сервис identity с настраиваемым локальным участником
/// </summary>
public sealed class TestIdentityService : IIdentityService
{
    /// <inheritdoc />
    public IParticipantData SelfParticipant { get; set; } = new Participant { Name = "TestUser" };

    /// <inheritdoc />
    public Func<IParticipantData, Task>? SelfParticipantSaved { get; set; }

    /// <inheritdoc />
    public async Task SaveParticipant(IParticipantData participantData)
    {
        SelfParticipant = participantData;
        if (SelfParticipantSaved != null)
        {
            await SelfParticipantSaved(participantData);
        }
    }
}
