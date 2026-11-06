using MIN.ScreenDemo.Services.Contracts.Interfaces;
using MIN.ScreenDemo.Services.Contracts.Models;

namespace MIN.ScreenDemo.Services;

/// <inheritdoc cref="IScreenSourceService"/> для платформ, не способных к демонстрации экрана
public class UnsupportedScreenSourceService : IScreenSourceService
{
    IReadOnlyList<ScreenSourceInfo> IScreenSourceService.GetSources() => [];
}
