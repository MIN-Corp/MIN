using MIN.Core.Entities.Contracts.Interfaces;
using MIN.Core.Entities.Contracts.Models;

namespace MIN.Core.Entities.Contracts.Extensions;

/// <summary>
/// Расширения для <see cref="IParticipantData"/>
/// </summary>
public static class ParticipantDataExtensions
{
    /// <summary>
    /// Преобразовать <see cref="IParticipantData"/> в <see cref="ParticipantInfo"/> 
    /// </summary>
    public static ParticipantInfo ToParticipantInfo(this IParticipantData participant)
        => new()
        {
            Id = participant.Id,
            Name = participant.Name,
        };

    /// <summary>
    /// Пользователь не менял свои данные та же
    /// </summary>
    public static bool IsSameProfile(this IParticipantData participant, IParticipantData incoming)
        => participant.Name == incoming.Name;
}
