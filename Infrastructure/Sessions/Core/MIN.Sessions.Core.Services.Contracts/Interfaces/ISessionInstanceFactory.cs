using MIN.Sessions.Core.Services.Contracts.Models;

namespace MIN.Sessions.Core.Services.Contracts.Interfaces;

/// <summary>
/// Фабрика по созданию экземпляров сессий
/// </summary>
public interface ISessionInstanceFactory
{
    /// <summary>
    /// Можно ли создать экземпляр сессии
    /// </summary>
    bool CanCreate(Session s);

    /// <summary>
    /// Создать экземпляр сессии
    /// </summary>
    ISessionInstance Create(CancellationToken cancellationToken);
}
