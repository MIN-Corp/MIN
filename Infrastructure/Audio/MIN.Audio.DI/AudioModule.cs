using Microsoft.Extensions.DependencyInjection;
using MIN.Audio.DI.FeatureCollection;
using MIN.Audio.Services;
using MIN.Common.Mvc;
using MIN.Common.Mvc.Extensions;

namespace MIN.Audio.DI;

/// <summary>
/// Модуль регистрации зависимостей общей аудио-инфраструктуры
/// </summary>
public class AudioModule : Module
{
    /// <inheritdoc />
    protected override void Load(IServiceCollection services)
    {
        services.RegisterAsImplementedInterfaces<AudioDeviceService>(ServiceLifetime.Singleton);
        services.AddSingleton<PlaybackDeviceManager>();
        services.RegisterAsImplementedInterfaces<AudioFeatureCollection>(ServiceLifetime.Singleton);
    }
}
