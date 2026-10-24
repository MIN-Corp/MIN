using MIN.Sessions.Core.Services.Contracts.Models;
using MIN.Sessions.Core.Transport.Contracts.Models;

namespace MIN.Sessions.Core.Services.Contracts.Interfaces;

/// <summary>
/// Фабрика экземпляров сессии
/// </summary>
public interface ISessionInstanceFactory
{
    /// <summary>
    /// Может ли фабрика создать экземпляр для этой сессии
    /// </summary>
    bool CanCreate(Session session);

    /// <summary>
    /// Создать экземпляр сессии
    /// </summary>
    ISessionInstance Create(SessionInstanceContext context);
}
