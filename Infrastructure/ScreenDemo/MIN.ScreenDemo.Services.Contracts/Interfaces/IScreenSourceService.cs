using MIN.ScreenDemo.Services.Contracts.Models;

namespace MIN.ScreenDemo.Services.Contracts.Interfaces;

/// <summary>
/// Сервис получения доступных источников захвата экрана
/// </summary>
public interface IScreenSourceService
{
    /// <summary>
    /// Получить список мониторов и окон, доступных для демонстрации
    /// </summary>
    IReadOnlyList<ScreenSourceInfo> GetSources();
}
